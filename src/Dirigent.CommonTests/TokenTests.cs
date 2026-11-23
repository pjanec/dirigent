using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.TestBed;
using Dirigent.Web;
using EmbedIO;
using EmbedIO.Routing;
using EmbedIO.WebApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace Dirigent.Tests
{
	/// <summary>
	/// The tokens of the web API: what is accepted and why not, and which scope every route needs.
	/// </summary>
	[TestClass()]
	public class TokenTests
	{
		static readonly DateTimeOffset Now = new( 2026, 11, 24, 12, 0, 0, TimeSpan.Zero );

		static (TokenMint mint, TokenValidator validator) Setup( string? audience = null )
		{
			var mint = new TokenMint();
			return ( mint, new TokenValidator( mint.PublicKeyPem, audience, () => Now ) );
		}

		static string Valid( TokenMint m, string scope = "read", object? aud = null ) => m.Mint( scope, "alice", TimeSpan.FromMinutes( 10 ), Now, aud );

		static IDictionary<string, object> Claims( params (string, object)[] claims ) => claims.ToDictionary( c => c.Item1, c => c.Item2 );

		// ---- accepted

		[TestMethod]
		public void AValidTokenIsAcceptedWithItsSubjectAndScopes()
		{
			var (m, v) = Setup();
			var r = v.Validate( m.Mint( "read control", "alice", TimeSpan.FromMinutes( 10 ), Now ) );
			Assert.IsTrue( r.Ok, r.Error );
			Assert.AreEqual( "alice", r.Subject );
			CollectionAssert.AreEquivalent( new[] { "read", "control" }, r.Scopes.ToList() );
			Assert.AreEqual( Now.AddMinutes( 10 ), r.Expires );
		}

		[TestMethod]
		public void ScopesComeFromScopeAsAStringAndFromScpAsAStringOrAnArray()
		{
			var (m, v) = Setup();
			long exp = Now.AddMinutes( 5 ).ToUnixTimeSeconds();

			var s1 = v.Validate( m.Mint( Claims( ("exp", exp), ("scope", "read sim") ) ) );
			CollectionAssert.AreEquivalent( new[] { "read", "sim" }, s1.Scopes.ToList() );

			var s2 = v.Validate( m.Mint( Claims( ("exp", exp), ("scp", "control read") ) ) );
			CollectionAssert.AreEquivalent( new[] { "control", "read" }, s2.Scopes.ToList() );

			var s3 = v.Validate( m.Mint( Claims( ("exp", exp), ("scp", new[] { "read", "control" } ) ) ) );
			CollectionAssert.AreEquivalent( new[] { "read", "control" }, s3.Scopes.ToList() );

			var s4 = v.Validate( m.Mint( Claims( ("exp", exp), ("scope", "read"), ("scp", new[] { "sim" } ) ) ) );
			CollectionAssert.AreEquivalent( new[] { "read", "sim" }, s4.Scopes.ToList(), "both claims add up" );

			Assert.AreEqual( 0, v.Validate( m.Mint( Claims( ("exp", exp) ) ) ).Scopes.Count, "a token with no scope allows nothing" );
		}

		[TestMethod]
		public void ControlDoesNotImplyRead()
		{
			var (m, v) = Setup();
			var r = v.Validate( Valid( m, "control" ) );
			Assert.IsTrue( r.Ok );
			Assert.IsFalse( r.Scopes.Contains( "read" ) );
		}

		// ---- time

		[TestMethod]
		public void AnExpiredTokenIsRefusedUnlessItIsWithinTheClockTolerance()
		{
			var (m, v) = Setup();
			var expiredAt = Now.AddSeconds( -61 );
			var r = v.Validate( m.Mint( Claims( ("exp", expiredAt.ToUnixTimeSeconds()), ("scope", "read") ) ) );
			Assert.IsFalse( r.Ok );
			StringAssert.StartsWith( r.Error, "expired at 2026-11-24 11:58:59" );

			Assert.IsTrue( v.Validate( m.Mint( Claims( ("exp", Now.AddSeconds( -50 ).ToUnixTimeSeconds() ), ("scope", "read") ) ) ).Ok, "50 s late: the clocks may differ by a minute" );
		}

		[TestMethod]
		public void ATokenThatIsNotValidYetIsRefusedUnlessItIsWithinTheTolerance()
		{
			var (m, v) = Setup();
			long exp = Now.AddHours( 1 ).ToUnixTimeSeconds();

			var early = v.Validate( m.Mint( Claims( ("exp", exp), ("nbf", Now.AddMinutes( 5 ).ToUnixTimeSeconds() ), ("scope", "read") ) ) );
			Assert.IsFalse( early.Ok );
			StringAssert.StartsWith( early.Error, "not valid yet" );

			Assert.IsTrue( v.Validate( m.Mint( Claims( ("exp", exp), ("nbf", Now.AddSeconds( 50 ).ToUnixTimeSeconds() ), ("scope", "read") ) ) ).Ok, "within the tolerance" );
			Assert.IsTrue( v.Validate( m.Mint( Claims( ("exp", exp), ("nbf", Now.AddSeconds( -10 ).ToUnixTimeSeconds() ), ("scope", "read") ) ) ).Ok );
		}

		[TestMethod]
		public void ATokenWithoutAnExpiryIsRefused()
		{
			var (m, v) = Setup();
			var r = v.Validate( m.Mint( Claims( ("scope", "read"), ("sub", "x") ) ) );
			Assert.IsFalse( r.Ok );
			StringAssert.Contains( r.Error, "exp is required" );
		}

		// ---- the signature

		[TestMethod]
		public void OnlyES256IsAccepted()
		{
			var (m, v) = Setup();
			long exp = Now.AddHours( 1 ).ToUnixTimeSeconds();

			var none = v.Validate( m.Mint( Claims( ("exp", exp), ("scope", "control") ), alg: "none" ) );
			Assert.IsFalse( none.Ok );
			StringAssert.Contains( none.Error, "unsupported algorithm" );

			var hs = v.Validate( m.Mint( Claims( ("exp", exp), ("scope", "control") ), alg: "HS256" ) );
			Assert.IsFalse( hs.Ok, "the public key used as an HMAC secret must not work" );
			StringAssert.Contains( hs.Error, "unsupported algorithm" );

			Assert.IsFalse( v.Validate( m.Mint( Claims( ("exp", exp) ), alg: "es256" ) ).Ok, "exactly ES256" );
			Assert.IsFalse( v.Validate( m.Mint( Claims( ("exp", exp) ), alg: "ES384" ) ).Ok );
		}

		[TestMethod]
		public void ATamperedPayloadOrAnotherKeysSignatureIsRefused()
		{
			var (m, v) = Setup();
			var token = Valid( m, "read" );
			var parts = token.Split( '.' );

			// the scope is rewritten, the signature kept
			var forged = TokenMint.B64( System.Text.Encoding.UTF8.GetBytes( System.Text.Json.JsonSerializer.Serialize(
				new Dictionary<string, object> { ["sub"] = "alice", ["scope"] = "read control sim", ["exp"] = Now.AddHours( 1 ).ToUnixTimeSeconds() } ) ) );
			var r = v.Validate( parts[0] + "." + forged + "." + parts[2] );
			Assert.IsFalse( r.Ok );
			Assert.AreEqual( "bad signature", r.Error );

			using var other = new TokenMint();
			var r2 = v.Validate( other.Mint( "control", "mallory", TimeSpan.FromMinutes( 5 ), Now ) );
			Assert.AreEqual( "bad signature", r2.Error, "signed by a key that is not ours" );

			// a signature of the wrong length
			Assert.AreEqual( "bad signature", v.Validate( parts[0] + "." + parts[1] + "." + TokenMint.B64( new byte[10] ) ).Error );
		}

		[TestMethod]
		public void MalformedTokensAreRefused()
		{
			var (m, v) = Setup();
			foreach( var bad in new[] { "", "   ", "abc", "a.b", "a.b.c.d", "!!!.???.***", "e30.e30", "e30.e30.", "bm90anNvbg.bm90anNvbg.AAAA" } )
			{
				var r = v.Validate( bad );
				Assert.IsFalse( r.Ok, "token: " + bad );
			}
			Assert.AreEqual( "no token", v.Validate( null ).Error );
			Assert.AreEqual( "malformed token", v.Validate( "a.b.c.d" ).Error );
			Assert.AreEqual( "malformed token", v.Validate( new string( 'a', 20000 ) ).Error, "a huge one is not even looked at" );
		}

		// ---- the audience

		[TestMethod]
		public void TheAudienceIsCheckedOnlyWhenOneIsSet()
		{
			var (m, none) = Setup();
			Assert.IsTrue( none.Validate( Valid( m, "read" ) ).Ok, "no audience configured: not checked" );
			Assert.IsTrue( none.Validate( Valid( m, "read", "somebody-else" ) ).Ok );

			var v = new TokenValidator( m.PublicKeyPem, "dirigent-lab", () => Now );
			Assert.IsTrue( v.Validate( Valid( m, "read", "dirigent-lab" ) ).Ok, "a string" );
			Assert.IsTrue( v.Validate( Valid( m, "read", new[] { "other", "dirigent-lab" } ) ).Ok, "an array that contains it" );

			var wrong = v.Validate( Valid( m, "read", "other-app" ) );
			Assert.IsFalse( wrong.Ok );
			StringAssert.StartsWith( wrong.Error, "wrong audience" );
			Assert.IsFalse( v.Validate( Valid( m, "read", new[] { "a", "b" } ) ).Ok );
			Assert.IsFalse( v.Validate( Valid( m, "read" ) ).Ok, "no aud at all, one required" );
		}

		[TestMethod]
		public void ABadPublicKeyIsAConfigurationError()
		{
			Assert.ThrowsException<SecurityConfigException>( () => new TokenValidator( "not a key" ) );

			using var rsa = RSA.Create( 2048 );
			Assert.ThrowsException<SecurityConfigException>( () => new TokenValidator( rsa.ExportSubjectPublicKeyInfoPem() ), "an RSA key is not ES256" );

			using var p384 = ECDsa.Create( ECCurve.NamedCurves.nistP384 );
			Assert.ThrowsException<SecurityConfigException>( () => new TokenValidator( p384.ExportSubjectPublicKeyInfoPem() ), "nor is P-384" );
		}

		// ---- the scopes of the routes

		static IEnumerable<MethodInfo> RouteMethods()
			=> typeof( ScopeMap ).Assembly.GetTypes()
				.Where( t => typeof( WebApiController ).IsAssignableFrom( t ) && !t.IsAbstract )
				.SelectMany( t => t.GetMethods( BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly ) )
				.Where( m => m.GetCustomAttributes<RouteAttribute>().Any() );

		[TestMethod]
		public void EveryRouteMethodDeclaresItsScope()
		{
			var methods = RouteMethods().ToList();
			Assert.IsTrue( methods.Count > 40, "only " + methods.Count + " route methods found - did the controllers move?" );

			var without = methods.Where( m => m.GetCustomAttribute<ScopeAttribute>() is null ).Select( m => m.DeclaringType!.Name + "." + m.Name ).ToList();
			Assert.AreEqual( 0, without.Count, "route methods without a [Scope]:\n  " + string.Join( "\n  ", without ) );

			var bad = methods.Where( m => m.GetCustomAttribute<ScopeAttribute>()!.Required.Any( r => r is not ( "read" or "control" or "sim" or "open" ) ) )
				.Select( m => m.DeclaringType!.Name + "." + m.Name ).ToList();
			Assert.AreEqual( 0, bad.Count, "unknown scopes: " + string.Join( ", ", bad ) );
		}

		static string Scope( string verb, string path )
		{
			var (required, open, matched) = ScopeMap.Resolve( verb, path );
			return open ? "open" : string.Join( " ", required ) + ( matched ? "" : " (unmatched)" );
		}

		[TestMethod]
		public void TheScopeLookupResolvesTemplatesWithParameters()
		{
			Assert.AreEqual( "control", Scope( "POST", "/api/plandefs/p1/start" ) );
			Assert.AreEqual( "control", Scope( "POST", "/api/plandefs/some%20plan/restart" ) );
			Assert.AreEqual( "read", Scope( "GET", "/api/plandefs/p1" ) );
			Assert.AreEqual( "read", Scope( "GET", "/api/appstates" ) );
			Assert.AreEqual( "read", Scope( "GET", "/api/appstates/m1.a" ) );
			Assert.AreEqual( "read", Scope( "GET", "/api/appstate/m1.a" ), "the older singular" );
			Assert.AreEqual( "read", Scope( "GET", "/api/downloads/j1/archives/x.zip" ) );
			Assert.AreEqual( "control", Scope( "DELETE", "/api/downloads/j1" ) );
			Assert.AreEqual( "control", Scope( "POST", "/api/downloads" ) );
			Assert.AreEqual( "read", Scope( "POST", "/api/files/query" ), "the one POST that only looks" );
			Assert.AreEqual( "control", Scope( "POST", "/api/cli" ) );
			Assert.AreEqual( "control", Scope( "POST", "/api/killall" ) );
			Assert.AreEqual( "control", Scope( "POST", "/api/sharedconfig/reload" ) );
			Assert.AreEqual( "control", Scope( "POST", "/api/machinedefs/m1/reboot" ) );
			Assert.AreEqual( "control", Scope( "POST", "/api/marks" ), "and sim as well when its operation is clear: asked by the controller" );
			Assert.AreEqual( "control", Scope( "POST", "/api/filedefs/pkg.x/mark" ) );
			Assert.AreEqual( "sim", Scope( "POST", "/api/filedefs/pkg.x/clear" ) );
			Assert.AreEqual( "sim", Scope( "POST", "/api/appdefs/m1.a/clear" ) );
			Assert.AreEqual( "control", Scope( "POST", "/api/scripts/abc/kill" ) );
			Assert.AreEqual( "read", Scope( "HEAD", "/api/appstates" ), "HEAD counts as GET" );
		}

		[TestMethod]
		public void TheIndexAndTheManualAreOpenAndNothingElseIs()
		{
			Assert.AreEqual( "open", Scope( "GET", "/api" ) );
			Assert.AreEqual( "open", Scope( "GET", "/api/help" ) );
			Assert.AreEqual( "open", Scope( "GET", "/api/help/HTTP.md" ) );

			var open = ScopeMap.Routes().Where( r => r.Scope == "open" ).Select( r => r.Verb + " " + r.Path ).OrderBy( x => x ).ToList();
			CollectionAssert.AreEqual( new[] { "GET /api", "GET /api/help", "GET /api/help/{doc}" }, open, "the only routes without a token" );
		}

		[TestMethod]
		public void ARequestThatMatchesNoRouteNeedsControl()
		{
			Assert.AreEqual( "control (unmatched)", Scope( "GET", "/api/nothing/here" ) );
			Assert.AreEqual( "control (unmatched)", Scope( "PUT", "/api/appstates" ), "right path, a verb nobody declared" );
			Assert.AreEqual( "control (unmatched)", Scope( "GET", "/api/appstates/a/b/c" ) );
		}

		[TestMethod]
		public void OnlyClearNeedsSimAndNothingNeedsMoreThanOneScope()
		{
			var sim = ScopeMap.Routes().Where( r => r.Scope.Contains( "sim" ) ).Select( r => r.Verb + " " + r.Path ).OrderBy( x => x ).ToList();
			CollectionAssert.AreEqual( new[] { "POST /api/appdefs/{id}/clear", "POST /api/filedefs/{id}/clear" }, sim );
			Assert.IsFalse( ScopeMap.Routes().Any( r => r.Scope.Contains( ' ' ) ) );
		}
	}
}
