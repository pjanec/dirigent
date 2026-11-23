using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.Net;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// The master port with security on: TLS, and the proof of the site secret both ways before any client is let in.
	/// </summary>
	[TestClass()]
	public class SecureMasterTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 30 );

		static string NewSecret() => Guid.NewGuid().ToString( "N" ) + Guid.NewGuid().ToString( "N" );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		static Scenario World() => Scenario.TwoMachinesWithIdlers();

		static TestBedOptions Secured( string? secret = null, TimeSpan? handshakeTimeout = null )
		{
			var settings = SecuritySettings.WithSecret( secret ?? NewSecret() );
			if( handshakeTimeout is not null ) settings = settings.WithHandshakeTimeout( handshakeTimeout.Value );
			return new TestBedOptions() { Scenario = World(), Security = settings };
		}

		/// <summary>Lines in the log (of the master and of the clients, which share this process) that contain the text.</summary>
		static int Count( string text ) => Diagnostics.LogMessages().Count( x => x.Contains( text ) );

		static Task Wait( TestBed.TestBed bed, Func<bool> condition, string because ) => bed.WaitUntilAsync( condition, Timeout, because );

		static async Task<bool> IsClient( TestBed.TestBed bed, string name )
			=> ( await bed.Operator.GetAllClientsStateAsync() ).Any( c => c.Key == name && c.Value.Connected );

		// ---- the happy path

		[TestMethod()]
		public async Task ASecuredSiteRunsOverTlsAndAnAppStartsThroughIt()
		{
			using var bed = await TestBed.TestBed.StartAsync( Secured() );

			Assert.IsTrue( bed.Security.Secured );
			Assert.AreEqual( 1, Count( "The master port is secured" ) );

			var idler = bed.App( "m1", "idler" );
			await bed.Operator.StartAppAsync( idler );
			await bed.WaitUntilAsync( async () => ( await bed.Operator.GetAppStateAsync( idler ) )?.Running == true, Timeout, "the app is running, started over TLS" );

			Assert.AreEqual( 0, Count( "wrong site secret" ) );
			Assert.AreEqual( 0, Count( "did not prove" ) );
		}

		[TestMethod()]
		public async Task TheOtherSecretMeansFailureOnBothSidesAndAClientThatKnowsItGetsIn()
		{
			// two servers, one secret each: the same client code gets in or not by what it holds
			var port = Isolation.FreeTcpPort();
			var secret = NewSecret();
			using var server = new Server( port, new TrafficStats(), SecuritySettings.WithSecret( secret ) );

			using var good = new Net.Client( new ClientIdent( "good", EMsgRecipCateg.Gui ), "127.0.0.1", port, autoConn: true, security: SecuritySettings.WithSecret( secret ) );
			await Until( () => { server.Tick(); good.Tick(); return server.Clients.Any( c => c.Name == "good" ); }, "the client that knows the secret is identified" );

			// and a fake master holding another secret is not believed by a client that holds ours
			var fakePort = Isolation.FreeTcpPort();
			using var fake = new Server( fakePort, new TrafficStats(), SecuritySettings.WithSecret( NewSecret() ) );
			using var victim = new Net.Client( new ClientIdent( "victim", EMsgRecipCateg.Gui ), "127.0.0.1", fakePort, autoConn: true, security: SecuritySettings.WithSecret( secret ) );

			string? reason = null;
			victim.Rejected = r => reason = r;
			await Until( () => { fake.Tick(); victim.Tick(); return reason is not null; }, "the client refuses the fake master" );

			StringAssert.Contains( reason, "the master at 127.0.0.1 did not prove the site secret - wrong secret, or someone in between" );
			Assert.AreEqual( 1, Diagnostics.LogMessages().Count( x => x == reason ), "and says so in its log" );
			Assert.IsFalse( victim.IsConnected );
			Assert.IsFalse( fake.Clients.Any(), "the fake master never identified it: the client did not go on to say who it is" );
		}

		static async Task Until( Func<bool> condition, string because )
		{
			var end = DateTime.UtcNow + Timeout;
			while( DateTime.UtcNow < end )
			{
				if( condition() ) return;
				await Task.Delay( 25 );
			}
			Assert.Fail( "timed out waiting for: " + because );
		}

		// ---- refusals

		[TestMethod()]
		public async Task AClientWithAWrongSecretIsRefusedAndRetriesSlowly()
		{
			using var bed = await TestBed.TestBed.StartAsync( Secured() );

			var ident = new ClientIdent( "intruder", EMsgRecipCateg.Gui );
			using var client = new Net.Client( ident, "127.0.0.1", bed.MasterPort, autoConn: true, security: SecuritySettings.WithSecret( NewSecret() ) );
			string? reason = null;
			client.Rejected = r => reason = r;

			await Wait( bed, () => { client.Tick(); return reason is not null; }, "the client refuses a master that does not know its secret" );
			StringAssert.Contains( reason, "did not prove the site secret" );

			// the master sees a client that looked at its proof and walked away: a failed proof
			await Wait( bed, () => Count( "127.0.0.1: wrong site secret (or not the same Dirigent)" ) >= 1, "the master logs it" );
			Assert.IsFalse( await IsClient( bed, "intruder" ), "it never became a client" );
			Assert.IsFalse( client.IsConnected );

			// and it asks again no faster than every 30 s, instead of every second
			await Task.Delay( TimeSpan.FromSeconds( 4 ) );
			Assert.AreEqual( 1, Count( "127.0.0.1: wrong site secret (or not the same Dirigent)" ), "no second attempt within a few seconds" );
		}

		[TestMethod()]
		public async Task AClientThatSendsAWrongProofIsClosedAndTheMasterSaysSo()
		{
			using var bed = await TestBed.TestBed.StartAsync( Secured() );

			// a client that does not check the master's proof and answers with rubbish
			using var peer = await RawPeer.ConnectTlsAsync( bed.MasterPort );
			peer.Send( new HelloMessage( SiteProof.NewNonce() ) );
			var challenge = await peer.ReadAsync();
			Assert.IsInstanceOfType( challenge, typeof( ChallengeMessage ), "the master answers the Hello" );
			peer.Send( new ProofMessage( SiteProof.NewNonce() ) );

			Assert.IsTrue( await peer.IsClosedByPeerAsync( TimeSpan.FromSeconds( 10 ) ), "the master closes it" );
			Assert.AreEqual( 1, Count( "127.0.0.1: wrong site secret (or not the same Dirigent)" ) );
		}

		[TestMethod()]
		public async Task AnythingButTheExpectedMessageClosesTheConnection()
		{
			using var bed = await TestBed.TestBed.StartAsync( Secured() );

			// a ClientIdent before any proof: what a client that skipped the handshake would send
			using var early = await RawPeer.ConnectTlsAsync( bed.MasterPort );
			early.Send( new ClientIdent( "early", EMsgRecipCateg.Agent ) );
			Assert.IsTrue( await early.IsClosedByPeerAsync( TimeSpan.FromSeconds( 10 ) ), "a ClientIdent is not accepted before the proof" );
			Assert.IsFalse( await IsClient( bed, "early" ) );

			// a Proof without a Hello
			using var proofFirst = await RawPeer.ConnectTlsAsync( bed.MasterPort );
			proofFirst.Send( new ProofMessage( SiteProof.NewNonce() ) );
			Assert.IsTrue( await proofFirst.IsClosedByPeerAsync( TimeSpan.FromSeconds( 10 ) ) );
		}

		[TestMethod()]
		public async Task APlainClientToASecuredMasterIsRefusedWithTheTlsLine()
		{
			using var bed = await TestBed.TestBed.StartAsync( Secured() );

			// a client of security off: it says who it is in plain bytes to a port that wants a TLS handshake
			using var plain = new Net.Client( new ClientIdent( "plain", EMsgRecipCateg.Gui ), "127.0.0.1", bed.MasterPort, autoConn: true );
			await Wait( bed, () => Count( "127.0.0.1: TLS handshake failed - is security on and the same version on that machine?" ) >= 1, "the master says that the handshake failed" );
			Assert.IsFalse( await IsClient( bed, "plain" ) );

			// a second try from the same address is closed as well, and not said again
			using var again = new Net.Client( new ClientIdent( "plain2", EMsgRecipCateg.Gui ), "127.0.0.1", bed.MasterPort, autoConn: true );
			await Task.Delay( TimeSpan.FromSeconds( 3 ) );
			Assert.AreEqual( 1, Count( "TLS handshake failed" ), "once per address per ten minutes" );
			Assert.IsFalse( await IsClient( bed, "plain2" ) );
		}

		[TestMethod()]
		public async Task ASecuredClientToAPlainMasterSaysSoAndWaitsBeforeTryingAgain()
		{
			var options = new TestBedOptions() { Scenario = World(), SecurityOff = true };
			using var bed = await TestBed.TestBed.StartAsync( options );

			var settings = SecuritySettings.WithSecret( NewSecret() ).WithHandshakeTimeout( TimeSpan.FromSeconds( 1.5 ) );
			using var client = new Net.Client( new ClientIdent( "secure", EMsgRecipCateg.Gui ), "127.0.0.1", bed.MasterPort, autoConn: true, security: settings );
			string? reason = null;
			client.Rejected = r => reason = r;

			await Wait( bed, () => { client.Tick(); return reason is not null; }, "the client finds that the master does not speak TLS" );
			StringAssert.Contains( reason, "the master at 127.0.0.1" );
			StringAssert.Contains( reason, "is security on there too" );
			Assert.IsFalse( client.IsConnected );
			Assert.IsFalse( await IsClient( bed, "secure" ) );
		}

		[TestMethod()]
		public async Task AConnectionThatSaysNothingIsClosedWhenTheHandshakeTimeIsUp()
		{
			using var bed = await TestBed.TestBed.StartAsync( Secured( handshakeTimeout: TimeSpan.FromSeconds( 1.5 ) ) );

			// TCP only: no TLS, no word
			using var tcp = new TcpClient();
			await tcp.ConnectAsync( "127.0.0.1", bed.MasterPort );
			var stream = tcp.GetStream();

			var watch = System.Diagnostics.Stopwatch.StartNew();
			var read = stream.ReadAsync( new byte[16] ).AsTask();
			var done = await Task.WhenAny( read, Task.Delay( TimeSpan.FromSeconds( 15 ) ) );
			Assert.AreSame( read, done, "the master closes the connection" );
			int n; try { n = await read; } catch( IOException ) { n = 0; }
			Assert.AreEqual( 0, n );
			Assert.IsTrue( watch.Elapsed < TimeSpan.FromSeconds( 8 ), $"closed after {watch.Elapsed.TotalSeconds:0.0} s, not left hanging" );
			Assert.AreEqual( 1, Count( "the handshake was not completed within" ) );
		}

		[TestMethod()]
		public async Task TooManyWrongProofsFromOneAddressShutThatAddressOutForAWhile()
		{
			using var bed = await TestBed.TestBed.StartAsync( Secured() );

			for( int i = 0; i < 6; i++ )
			{
				using var peer = await RawPeer.ConnectTlsAsync( bed.MasterPort );
				peer.Send( new HelloMessage( SiteProof.NewNonce() ) );
				Assert.IsInstanceOfType( await peer.ReadAsync(), typeof( ChallengeMessage ) );
				peer.Send( new ProofMessage( SiteProof.NewNonce() ) );
				Assert.IsTrue( await peer.IsClosedByPeerAsync( TimeSpan.FromSeconds( 10 ) ), $"attempt {i + 1}" );
			}

			await Wait( bed, () => Count( "more than 5 failed proofs within 60 s" ) == 1, "the master says that it shuts the address out" );
			Assert.AreEqual( 6, Count( "wrong site secret" ) );

			// now it is closed on the spot: not even a TLS handshake is done
			var refused = false;
			try
			{
				using var peer = await RawPeer.ConnectTlsAsync( bed.MasterPort );
				refused = await peer.IsClosedByPeerAsync( TimeSpan.FromSeconds( 5 ) );
			}
			catch( Exception ex ) when( ex is IOException or AuthenticationException or SocketException ) { refused = true; }
			Assert.IsTrue( refused, "a connection from the blocked address is closed at once" );
			Assert.AreEqual( 6, Count( "wrong site secret" ), "and no proof is even looked at" );
			Assert.AreEqual( 1, Count( "more than 5 failed proofs" ), "the block is logged once" );
		}

		// ---- the man in the middle

		[TestMethod()]
		public async Task ARelayWithACertificateOfItsOwnBetweenClientAndMasterIsDetected()
		{
			var options = Secured();
			using var bed = await TestBed.TestBed.StartAsync( options );

			// a relay that terminates TLS with a certificate of its own and passes the decrypted bytes both ways, untouched
			using var relay = new TlsRelay( bed.MasterPort );

			using var client = new Net.Client( new ClientIdent( "relayed", EMsgRecipCateg.Gui ), "127.0.0.1", relay.Port, autoConn: true, security: bed.Security );
			string? reason = null;
			client.Rejected = r => reason = r;

			await Wait( bed, () => { client.Tick(); return reason is not null; }, "the client detects the relay" );
			StringAssert.Contains( reason, "did not prove the site secret - wrong secret, or someone in between" );

			Assert.IsTrue( relay.BytesToMaster > 0 && relay.BytesToClient > 0, "the relay really was in the middle: the Hello went up and the Challenge came down" );
			Assert.IsFalse( client.IsConnected, "the connection is closed" );

			await Task.Delay( 1500 );
			Assert.IsFalse( await IsClient( bed, "relayed" ), "the master never identified it" );
		}

		// ---- security off

		[TestMethod()]
		public async Task TheEnvironmentSwitchSecuresEveryBedThatDoesNotOptOut()
		{
			// DIRIGENT_TEST_SECURITY=1 runs the whole suite over TLS; this is what shows that it really does
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World() } );
			Assert.AreEqual( TestBed.TestBed.SecurityRequestedByEnvironment, bed.Security.Secured );
			Assert.AreEqual( TestBed.TestBed.SecurityRequestedByEnvironment ? 1 : 0, Count( "The master port is secured" ) );

			using var optedOut = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World(), SecurityOff = true } );
			Assert.IsFalse( optedOut.Security.Secured );
		}

		[TestMethod()]
		public async Task WithSecurityOffThePortIsPlainAsEver()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World(), SecurityOff = true } );

			Assert.IsFalse( bed.Security.Secured );
			Assert.AreEqual( 1, Count( "The master port is not secured" ) );

			// a client of 3.1.20 that knows of no security gets in without any handshake
			using var client = new Net.Client( new ClientIdent( "plainone", EMsgRecipCateg.Gui ), "127.0.0.1", bed.MasterPort, autoConn: true );
			await Wait( bed, () => { client.Tick(); return client.IsConnected; }, "it connects" );
			await Wait( bed, () => IsClient( bed, "plainone" ).GetAwaiter().GetResult(), "and is a client" );
			Assert.AreEqual( 0, Count( "TLS" ), "TLS is nowhere in sight" );
		}

		// ---- test plumbing

		/// <summary>A client of the master written by hand, to say what a real one never would.</summary>
		sealed class RawPeer : IDisposable
		{
			readonly TcpClient _tcp;
			readonly Stream _stream;
			public byte[]? CertHash;

			RawPeer( TcpClient tcp, Stream stream ) { _tcp = tcp; _stream = stream; }

			public static async Task<RawPeer> ConnectTlsAsync( int port )
			{
				var tcp = new TcpClient();
				await tcp.ConnectAsync( "127.0.0.1", port );
				byte[]? hash = null;
				var ssl = new SslStream( tcp.GetStream(), false, ( s, cert, chain, errors ) => { hash = cert is null ? null : SiteProof.CertHash( cert ); return true; } );
				try
				{
					await ssl.AuthenticateAsClientAsync( "127.0.0.1" ).WaitAsync( TimeSpan.FromSeconds( 10 ) );
				}
				catch
				{
					tcp.Dispose();
					throw;
				}
				return new RawPeer( tcp, ssl ) { CertHash = hash };
			}

			public void Send( Net.Message msg )
			{
				var ms = new MemoryStream();
				MsgPackCodec.Serialize<Net.Message>( ms, msg );
				_stream.Write( ms.GetBuffer(), 0, (int) ms.Position );
				_stream.Flush();
			}

			async Task<byte[]?> ReadExactAsync( int count, CancellationToken ct )
			{
				var buf = new byte[count];
				int got = 0;
				while( got < count )
				{
					int n = await _stream.ReadAsync( buf.AsMemory( got, count - got ), ct );
					if( n == 0 ) return null;
					got += n;
				}
				return buf;
			}

			public async Task<Net.Message?> ReadAsync()
			{
				using var cts = new CancellationTokenSource( TimeSpan.FromSeconds( 10 ) );
				var header = await ReadExactAsync( 4, cts.Token );
				if( header is null ) return null;
				var size = (int) BitConverter.ToUInt32( header, 0 );
				var body = await ReadExactAsync( size, cts.Token );
				return body is null ? null : MsgPackCodec.Deserialize<Net.Message>( body, 0, size );
			}

			public async Task<bool> IsClosedByPeerAsync( TimeSpan within )
			{
				using var cts = new CancellationTokenSource( within );
				try
				{
					var buf = new byte[256];
					while( true )
					{
						int n = await _stream.ReadAsync( buf, cts.Token );
						if( n == 0 ) return true;
					}
				}
				catch( OperationCanceledException ) { return false; }
				catch( Exception ex ) when( ex is IOException or SocketException or ObjectDisposedException ) { return true; }
			}

			public void Dispose() { try { _tcp.Dispose(); } catch { } }
		}

		/// <summary>A man in the middle: TLS to the client with a certificate of its own, TLS to the master, and the plain bytes in between untouched.</summary>
		sealed class TlsRelay : IDisposable
		{
			readonly TcpListener _listener;
			readonly X509Certificate2 _cert = MasterCertificate.Create();
			readonly int _masterPort;
			readonly CancellationTokenSource _stop = new();
			long _toMaster, _toClient;

			public int Port { get; }
			public long BytesToMaster => Interlocked.Read( ref _toMaster );
			public long BytesToClient => Interlocked.Read( ref _toClient );

			public TlsRelay( int masterPort )
			{
				_masterPort = masterPort;
				_listener = new TcpListener( IPAddress.Loopback, 0 );
				_listener.Start();
				Port = ( (IPEndPoint) _listener.LocalEndpoint ).Port;
				_ = Task.Run( AcceptLoop );
			}

			async Task AcceptLoop()
			{
				try
				{
					while( !_stop.IsCancellationRequested )
					{
						var client = await _listener.AcceptTcpClientAsync( _stop.Token );
						_ = Task.Run( () => Serve( client ) );
					}
				}
				catch { /* stopped */ }
			}

			async Task Serve( TcpClient client )
			{
				try
				{
					using( client )
					{
						var down = new SslStream( client.GetStream(), false );
						await down.AuthenticateAsServerAsync( _cert, false, SslProtocols.Tls12 | SslProtocols.Tls13, false );

						using var master = new TcpClient();
						await master.ConnectAsync( IPAddress.Loopback, _masterPort );
						var up = new SslStream( master.GetStream(), false, ( s, c, ch, e ) => true );
						await up.AuthenticateAsClientAsync( "127.0.0.1" );

						await Task.WhenAny( Pump( down, up, towardsMaster: true ), Pump( up, down, towardsMaster: false ) );
					}
				}
				catch { /* a connection that ends */ }
			}

			async Task Pump( Stream from, Stream to, bool towardsMaster )
			{
				var buf = new byte[8192];
				while( true )
				{
					int n = await from.ReadAsync( buf, _stop.Token );
					if( n == 0 ) return;
					await to.WriteAsync( buf.AsMemory( 0, n ), _stop.Token );
					await to.FlushAsync( _stop.Token );
					if( towardsMaster ) Interlocked.Add( ref _toMaster, n ); else Interlocked.Add( ref _toClient, n );
				}
			}

			public void Dispose()
			{
				_stop.Cancel();
				_listener.Stop();
				_cert.Dispose();
			}
		}
	}
}
