using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.Net;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Dirigent.Tests
{
	/// <summary>
	/// Security settings and the secret files, the proof of the site secret, the limits, the CLIPort setting. Nothing here opens a socket.
	/// </summary>
	[TestClass()]
	public class SecurityTests
	{
		static readonly string Good = new string( 'k', 40 );

		/// <summary>Three folders: an explicit one, the LocalConfig's, the executable's.</summary>
		sealed class Places : IDisposable
		{
			public string Root = Path.Combine( Path.GetTempPath(), "dirigent-sec-" + Guid.NewGuid().ToString( "N" ) );
			public string Explicit => Path.Combine( Root, "explicit" );
			public string Local => Path.Combine( Root, "local" );
			public string Exe => Path.Combine( Root, "exe" );
			public string LocalConfigFile => Path.Combine( Local, "LocalConfig.xml" );

			public Places()
			{
				Directory.CreateDirectory( Explicit );
				Directory.CreateDirectory( Local );
				Directory.CreateDirectory( Exe );
			}

			public void Put( string folder, string content, string name = SecuritySettings.SiteSecretFileName )
				=> File.WriteAllText( Path.Combine( folder, name ), content );

			public void Dispose() { try { Directory.Delete( Root, true ); } catch { } }
		}

		static SecuritySettings Load( Places p, string? explicitFile = null, ESecurityMode mode = ESecurityMode.On, string? explicitKey = null )
			=> SecuritySettings.Load( mode, explicitFile, explicitKey, p.LocalConfigFile, p.Exe );

		// ---- where the secret is looked for

		[TestMethod]
		public void TheSecretIsFoundInTheFolderOfTheExecutableLast()
		{
			using var p = new Places();
			p.Put( p.Exe, Good );
			var s = Load( p );
			Assert.AreEqual( Path.Combine( p.Exe, "site.secret" ), s.SiteSecretSource );
			CollectionAssert.AreEqual( Encoding.UTF8.GetBytes( Good ), s.SiteSecret );
		}

		[TestMethod]
		public void TheFolderOfTheLocalConfigComesBeforeTheExecutablesFolder()
		{
			using var p = new Places();
			p.Put( p.Exe, new string( 'e', 40 ) );
			p.Put( p.Local, new string( 'l', 40 ) );
			Assert.AreEqual( Path.Combine( p.Local, "site.secret" ), Load( p ).SiteSecretSource );
		}

		[TestMethod]
		public void AnExplicitPathComesBeforeBothAndIsTheOnlyPlaceLookedAt()
		{
			using var p = new Places();
			p.Put( p.Exe, new string( 'e', 40 ) );
			p.Put( p.Local, new string( 'l', 40 ) );
			p.Put( p.Explicit, new string( 'x', 40 ), "mine.key" );
			Assert.AreEqual( Path.Combine( p.Explicit, "mine.key" ), Load( p, Path.Combine( p.Explicit, "mine.key" ) ).SiteSecretSource );

			// named but not there: no falling back to the secret of another place
			var ex = Assert.ThrowsException<SecurityConfigException>( () => Load( p, Path.Combine( p.Explicit, "nope.key" ) ) );
			StringAssert.Contains( ex.Message, "nope.key" );
			Assert.IsFalse( ex.Message.Contains( p.Exe ), "an explicit path is the only place looked at" );
		}

		[TestMethod]
		public void AMissingSecretIsAnErrorThatNamesTheFileAndEveryPlaceItLooked()
		{
			using var p = new Places();
			var ex = Assert.ThrowsException<SecurityConfigException>( () => Load( p ) );
			StringAssert.Contains( ex.Message, "site.secret" );
			StringAssert.Contains( ex.Message, Path.Combine( p.Local, "site.secret" ) );
			StringAssert.Contains( ex.Message, Path.Combine( p.Exe, "site.secret" ) );
			StringAssert.Contains( ex.Message, "LocalConfig.xml" );
			StringAssert.Contains( ex.Message, "--siteSecretFile" );
		}

		[TestMethod]
		public void WithoutALocalConfigTheErrorSaysThatItsFolderWasNotSearched()
		{
			using var p = new Places();
			var ex = Assert.ThrowsException<SecurityConfigException>( () => SecuritySettings.Load( ESecurityMode.On, null, null, "", p.Exe ) );
			StringAssert.Contains( ex.Message, Path.Combine( p.Exe, "site.secret" ) );
			StringAssert.Contains( ex.Message, "no LocalConfig.xml" );
		}

		[TestMethod]
		public void AuditNeedsTheSecretToo()
		{
			using var p = new Places();
			Assert.ThrowsException<SecurityConfigException>( () => Load( p, mode: ESecurityMode.Audit ) );
			p.Put( p.Exe, Good );
			Assert.IsTrue( Load( p, mode: ESecurityMode.Audit ).Secured );
		}

		[TestMethod]
		public void WithSecurityOffNothingIsRead()
		{
			using var p = new Places();
			var s = Load( p, mode: ESecurityMode.Off );
			Assert.IsFalse( s.Secured );
			Assert.AreEqual( 0, s.SiteSecret.Length );
		}

		// ---- what a secret may be

		[TestMethod]
		public void ASecretShorterThan32CharactersIsRefusedAndTheErrorNamesTheFile()
		{
			using var p = new Places();
			p.Put( p.Exe, new string( 'a', 31 ) );
			var ex = Assert.ThrowsException<SecurityConfigException>( () => Load( p ) );
			StringAssert.Contains( ex.Message, Path.Combine( p.Exe, "site.secret" ) );
			StringAssert.Contains( ex.Message, "31" );
			Assert.IsFalse( ex.Message.Contains( new string( 'a', 31 ) ), "the secret must not be repeated in a message" );

			p.Put( p.Exe, "   \r\n\t " );
			Assert.ThrowsException<SecurityConfigException>( () => Load( p ), "nothing but white space" );
		}

		[TestMethod]
		public void TheContentIsTrimmedAndExactly32CharactersDo()
		{
			using var p = new Places();
			var key = new string( 'z', 32 );
			p.Put( p.Exe, "\r\n  " + key + "  \r\n" );
			CollectionAssert.AreEqual( Encoding.UTF8.GetBytes( key ), Load( p ).SiteSecret );

			// the spaces count for nothing: 30 characters in the middle of spaces are 30
			p.Put( p.Exe, "          " + new string( 'z', 30 ) + "          " );
			Assert.ThrowsException<SecurityConfigException>( () => Load( p ) );
		}

		[TestMethod]
		public void AByteOrderMarkIsNotPartOfTheSecret()
		{
			using var p = new Places();
			File.WriteAllText( Path.Combine( p.Exe, "site.secret" ), Good, new UTF8Encoding( encoderShouldEmitUTF8Identifier: true ) );
			CollectionAssert.AreEqual( Encoding.UTF8.GetBytes( Good ), Load( p ).SiteSecret );
		}

		[TestMethod]
		public void TheTokenKeyIsOnlyLocatedAndOnlyAMissingNamedOneIsAnError()
		{
			using var p = new Places();
			p.Put( p.Exe, Good );
			Assert.IsNull( Load( p ).TokenPublicKeyPath, "not there: not an error yet, a later step needs it" );

			p.Put( p.Local, "key", SecuritySettings.TokenPublicKeyFileName );
			Assert.AreEqual( Path.Combine( p.Local, "remoting-public.pem" ), Load( p ).TokenPublicKeyPath );

			Assert.ThrowsException<SecurityConfigException>( () => Load( p, explicitKey: Path.Combine( p.Explicit, "none.pem" ) ) );
		}

		[TestMethod]
		public void AMasterWithAWebServerMustHaveTheTokenKeyAndItIsReadAndChecked()
		{
			using var p = new Places();
			p.Put( p.Exe, Good );

			var ex = Assert.ThrowsException<SecurityConfigException>( () => SecuritySettings.Load( ESecurityMode.On, null, null, p.LocalConfigFile, p.Exe, needTokenKey: true ) );
			StringAssert.Contains( ex.Message, "remoting-public.pem" );
			StringAssert.Contains( ex.Message, Path.Combine( p.Local, "remoting-public.pem" ) );
			StringAssert.Contains( ex.Message, Path.Combine( p.Exe, "remoting-public.pem" ) );

			// not a key
			p.Put( p.Local, "this is not a key", "remoting-public.pem" );
			var bad = Assert.ThrowsException<SecurityConfigException>( () => SecuritySettings.Load( ESecurityMode.On, null, null, p.LocalConfigFile, p.Exe, needTokenKey: true ) );
			StringAssert.Contains( bad.Message, Path.Combine( p.Local, "remoting-public.pem" ) );

			// a key, found like the secret; the audience goes with it
			using var mint = new Dirigent.TestBed.TokenMint();
			p.Put( p.Local, mint.PublicKeyPem, "remoting-public.pem" );
			var s = SecuritySettings.Load( ESecurityMode.On, null, null, p.LocalConfigFile, p.Exe, needTokenKey: true, tokenAudience: "lab" );
			Assert.IsNotNull( s.Tokens );
			Assert.AreEqual( "lab", s.TokenAudience );
			Assert.IsTrue( s.Tokens!.Validate( mint.Mint( "read", "x", audience: "lab" ) ).Ok );

			// not needed (no web server, or an agent): not required
			Assert.IsNull( SecuritySettings.Load( ESecurityMode.On, null, null, p.LocalConfigFile, p.Exe ).Tokens );
			Assert.IsNull( SecuritySettings.Load( ESecurityMode.Off, null, null, p.LocalConfigFile, p.Exe, needTokenKey: true ).Tokens, "security off: nothing is read" );
		}

		[TestMethod]
		public void TheTokenAudienceAndRunsMasterComeFromTheConfig()
		{
			Assert.IsTrue( new AppConfig() { Mode = "master" }.RunsMaster );
			Assert.IsTrue( new AppConfig() { IsMaster = "1" }.RunsMaster );
			Assert.IsFalse( new AppConfig() { Mode = "daemon", IsMaster = "0" }.RunsMaster );
		}

		[TestMethod]
		public void TheModeIsReadStrictly()
		{
			Assert.AreEqual( ESecurityMode.Off, SecuritySettings.ParseMode( "" ) );
			Assert.AreEqual( ESecurityMode.Off, SecuritySettings.ParseMode( "off" ) );
			Assert.AreEqual( ESecurityMode.Audit, SecuritySettings.ParseMode( "Audit" ) );
			Assert.AreEqual( ESecurityMode.On, SecuritySettings.ParseMode( " ON " ) );
			Assert.ThrowsException<SecurityConfigException>( () => SecuritySettings.ParseMode( "yes" ), "a typo must not mean off" );
			Assert.ThrowsException<SecurityConfigException>( () => SecuritySettings.ParseMode( "1" ) );
		}

		[TestMethod]
		public void ASecretInMemoryIsValidatedToo()
		{
			Assert.ThrowsException<SecurityConfigException>( () => SecuritySettings.WithSecret( "short" ) );
			Assert.IsTrue( SecuritySettings.WithSecret( Good ).Secured );
			Assert.IsFalse( SecuritySettings.WithSecret( Good, ESecurityMode.Off ).Secured );
		}

		// ---- the proof

		static readonly byte[] Secret = Encoding.UTF8.GetBytes( Good );
		static byte[] Bytes( byte b ) => Enumerable.Repeat( b, 32 ).ToArray();

		[TestMethod]
		public void TheProofsAreTheHmacOfLabelNoncesAndCertificateHash()
		{
			var cn = Bytes( 1 ); var mn = Bytes( 2 ); var hash = Bytes( 3 );

			byte[] Expected( string label )
			{
				using var h = new HMACSHA256( Secret );
				return h.ComputeHash( Encoding.ASCII.GetBytes( label ).Concat( cn ).Concat( mn ).Concat( hash ).ToArray() );
			}

			CollectionAssert.AreEqual( Expected( "dirigent-master" ), SiteProof.MasterProof( Secret, cn, mn, hash ) );
			CollectionAssert.AreEqual( Expected( "dirigent-client" ), SiteProof.ClientProof( Secret, cn, mn, hash ) );
		}

		[TestMethod]
		public void AMastersProofCannotBeReplayedAsAClientsProof()
		{
			var cn = Bytes( 1 ); var mn = Bytes( 2 ); var hash = Bytes( 3 );
			Assert.IsFalse( SiteProof.Matches( SiteProof.MasterProof( Secret, cn, mn, hash ), SiteProof.ClientProof( Secret, cn, mn, hash ) ),
				"the same inputs, the other label: the two proofs differ, so one is no use as the other" );
		}

		[TestMethod]
		public void EveryInputChangesTheProof()
		{
			var cn = Bytes( 1 ); var mn = Bytes( 2 ); var hash = Bytes( 3 );
			var proof = SiteProof.ClientProof( Secret, cn, mn, hash );

			Assert.IsFalse( SiteProof.Matches( proof, SiteProof.ClientProof( Encoding.UTF8.GetBytes( new string( 'q', 40 ) ), cn, mn, hash ) ), "the secret" );
			Assert.IsFalse( SiteProof.Matches( proof, SiteProof.ClientProof( Secret, Bytes( 9 ), mn, hash ) ), "the client's nonce" );
			Assert.IsFalse( SiteProof.Matches( proof, SiteProof.ClientProof( Secret, cn, Bytes( 9 ), hash ) ), "the master's nonce" );
			Assert.IsFalse( SiteProof.Matches( proof, SiteProof.ClientProof( Secret, cn, mn, Bytes( 9 ) ) ), "the certificate: what binds the proof to the TLS session" );
			Assert.IsFalse( SiteProof.Matches( proof, SiteProof.ClientProof( Secret, mn, cn, hash ) ), "the nonces in the other order" );
			Assert.IsTrue( SiteProof.Matches( proof, SiteProof.ClientProof( Secret, cn, mn, hash ) ) );
		}

		[TestMethod]
		public void ABadLengthIsRefusedAndMatchingNeedsBoth()
		{
			Assert.ThrowsException<ArgumentException>( () => SiteProof.MasterProof( Secret, new byte[16], Bytes( 2 ), Bytes( 3 ) ) );
			Assert.ThrowsException<ArgumentException>( () => SiteProof.MasterProof( Secret, Bytes( 1 ), Bytes( 2 ), new byte[31] ) );
			Assert.ThrowsException<ArgumentException>( () => SiteProof.MasterProof( Array.Empty<byte>(), Bytes( 1 ), Bytes( 2 ), Bytes( 3 ) ) );
			Assert.IsFalse( SiteProof.Matches( null, Bytes( 1 ) ) );
			Assert.IsFalse( SiteProof.Matches( Bytes( 1 ), null ) );
			Assert.IsFalse( SiteProof.Matches( Bytes( 1 ), new byte[5] ), "a proof of another length" );
		}

		[TestMethod]
		public void NoncesAreFreshAndTheCertificateIsAnEcdsaP256One()
		{
			Assert.AreEqual( 32, SiteProof.NewNonce().Length );
			CollectionAssert.AreNotEqual( SiteProof.NewNonce(), SiteProof.NewNonce() );

			using var cert = MasterCertificate.Create();
			Assert.IsTrue( cert.HasPrivateKey );
			using var key = cert.GetECDsaPrivateKey();
			Assert.IsNotNull( key );
			Assert.AreEqual( 256, key!.KeySize );
			Assert.IsTrue( cert.NotAfter > DateTime.Now.AddYears( 9 ), "valid for ten years" );
			Assert.IsTrue( cert.NotBefore < DateTime.Now );

			using var other = MasterCertificate.Create();
			CollectionAssert.AreNotEqual( SiteProof.CertHash( cert ), SiteProof.CertHash( other ), "a new certificate at every start" );
			Assert.AreEqual( 32, SiteProof.CertHash( cert ).Length );
		}

		// ---- the limits

		[TestMethod]
		public void TheSixthWrongProofWithinAMinuteBlocksTheAddressForAMinute()
		{
			var now = new DateTime( 2026, 11, 24, 10, 0, 0, DateTimeKind.Utc );
			var limiter = new ProofFailureLimiter( () => now );

			for( int i = 0; i < 5; i++ )
			{
				Assert.IsFalse( limiter.Record( "10.0.0.5", out var blocked ), $"failure {i + 1}" );
				Assert.IsFalse( blocked );
				now = now.AddSeconds( 5 );
			}
			Assert.IsFalse( limiter.IsBlocked( "10.0.0.5" ) );

			Assert.IsTrue( limiter.Record( "10.0.0.5", out var justBlocked ), "more than five" );
			Assert.IsTrue( justBlocked, "this is the failure that blocked it" );
			Assert.IsTrue( limiter.IsBlocked( "10.0.0.5" ) );
			Assert.IsFalse( limiter.IsBlocked( "10.0.0.6" ), "another address is not affected" );

			now = now.AddSeconds( 59 );
			Assert.IsTrue( limiter.IsBlocked( "10.0.0.5" ) );
			now = now.AddSeconds( 2 );
			Assert.IsFalse( limiter.IsBlocked( "10.0.0.5" ), "after a minute it is let in again" );
		}

		[TestMethod]
		public void FailuresThatAreSpreadOutDoNotAddUp()
		{
			var now = new DateTime( 2026, 11, 24, 10, 0, 0, DateTimeKind.Utc );
			var limiter = new ProofFailureLimiter( () => now );
			for( int i = 0; i < 20; i++ )
			{
				Assert.IsFalse( limiter.Record( "10.0.0.5", out _ ), $"failure {i}" );
				now = now.AddSeconds( 15 ); // four in a minute at most
			}
			Assert.IsFalse( limiter.IsBlocked( "10.0.0.5" ) );
		}

		[TestMethod]
		public void ALogLineIsSaidOncePerAddressAndInterval()
		{
			var now = new DateTime( 2026, 11, 24, 10, 0, 0, DateTimeKind.Utc );
			var limiter = new AddressLogLimiter( TimeSpan.FromMinutes( 10 ), () => now );
			Assert.IsTrue( limiter.ShouldLog( "a" ) );
			Assert.IsFalse( limiter.ShouldLog( "a" ) );
			Assert.IsTrue( limiter.ShouldLog( "b" ) );
			now = now.AddMinutes( 9 );
			Assert.IsFalse( limiter.ShouldLog( "a" ) );
			now = now.AddMinutes( 2 );
			Assert.IsTrue( limiter.ShouldLog( "a" ) );
		}

		// ---- the CLIPort setting

		[TestMethod]
		public void CliPortTakesNumbersOffAndNothing()
		{
			void Check( string? text, bool ok, CliPortSetting.EKind kind, int port )
			{
				Assert.AreEqual( ok, CliPortSetting.TryParse( text, out var k, out var p ), $"'{text}'" );
				if( !ok ) return;
				Assert.AreEqual( kind, k, $"'{text}'" );
				Assert.AreEqual( port, p, $"'{text}'" );
			}

			Check( null, true, CliPortSetting.EKind.Default, 0 );
			Check( "", true, CliPortSetting.EKind.Default, 0 );
			Check( "  ", true, CliPortSetting.EKind.Default, 0 );
			Check( "0", true, CliPortSetting.EKind.Default, 0 );        // as before: 0 = the default
			Check( "5050", true, CliPortSetting.EKind.Port, 5050 );
			Check( " 6000 ", true, CliPortSetting.EKind.Port, 6000 );
			Check( "65535", true, CliPortSetting.EKind.Port, 65535 );
			Check( "off", true, CliPortSetting.EKind.Off, 0 );
			Check( "OFF", true, CliPortSetting.EKind.Off, 0 );
			Check( "65536", false, default, 0 );
			Check( "-1", false, default, 0 );
			Check( "on", false, default, 0 );
			Check( "50x0", false, default, 0 );
			Check( "5.5", false, default, 0 );
		}

		[TestMethod]
		public void TheCliRunsByDefaultOffWhenAskedAndOffWithSecurityUnlessAPortIsNamed()
		{
			var plain = new AppConfig();
			Assert.IsTrue( plain.CliEnabled, "security off, nothing said: as always" );

			plain.CliPortExplicit = true;
			Assert.IsTrue( plain.CliEnabled );
			Assert.IsFalse( plain.CliUnauthenticatedWhileSecured );

			plain.CliOff = true;
			Assert.IsFalse( plain.CliEnabled, "CLIPort=off" );

			var secured = new AppConfig() { Security = "on" };
			Assert.IsFalse( secured.CliEnabled, "security on and no port named: no CLI" );

			secured.CliPortExplicit = true;
			Assert.IsTrue( secured.CliEnabled, "named: it runs" );
			Assert.IsTrue( secured.CliUnauthenticatedWhileSecured, "and it is said that it asks nobody" );

			secured.CliOff = true;
			Assert.IsFalse( secured.CliEnabled );

			var audit = new AppConfig() { Security = "audit" };
			Assert.IsFalse( audit.CliEnabled, "audit secures the master port as well" );

			Assert.IsFalse( new AppConfig() { Security = "nonsense" }.CliEnabled, "a bad value is read cautiously until the start-up check refuses it" );
		}
	}
}
