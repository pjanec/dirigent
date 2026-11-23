using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Dirigent.SystemTests
{
	/// <summary>
	/// The web API with security on, against a real master: who gets in with which token. The test mints tokens with a key pair of its own,
	/// whose public half it has put where the master looks for <c>remoting-public.pem</c>.
	/// </summary>
	[TestClass()]
	public class TokenSystemTests
	{
		const string Audience = "dirigent-system-test";

		static string LogText( SystemWorld world, string machine )
		{
			var path = world.Files.LogOf( machine );
			if( !File.Exists( path ) ) return "";
			using var file = new FileStream( path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete );
			using var reader = new StreamReader( file );
			return reader.ReadToEnd();
		}

		static HttpResponseMessage Call( SystemWorld world, HttpMethod method, string path, string? token, string? body = null )
		{
			using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds( 30 ) };
			using var request = new HttpRequestMessage( method, $"http://127.0.0.1:{world.HttpPort}{path}" );
			if( token is not null ) request.Headers.Authorization = new AuthenticationHeaderValue( "Bearer", token );
			if( body is not null ) request.Content = new StringContent( body, Encoding.UTF8, "text/plain" );
			return http.SendAsync( request ).GetAwaiter().GetResult();
		}

		static string Text( HttpResponseMessage r ) => r.Content.ReadAsStringAsync().GetAwaiter().GetResult();

		static void ExpectSuccess( HttpResponseMessage r, string what )
			=> Assert.IsTrue( r.IsSuccessStatusCode, $"{what}: {(int) r.StatusCode} {Text( r )}" );

		static void Expect( HttpResponseMessage r, HttpStatusCode code, string what )
			=> Assert.AreEqual( code, r.StatusCode, $"{what}: {(int) r.StatusCode} {Text( r )}" );

		[TestMethod()]
		public void TokensDecideWhoGetsInAndWhatTheyMayDo()
		{
			using var world = SystemWorld.Start( "ControlWorld", secured: true, cliNamed: false, tokens: true, audience: Audience );

			// open: the way in for a caller that knows only the address
			var index = Call( world, HttpMethod.Get, "/api", null );
			Expect( index, HttpStatusCode.OK, "GET /api" );
			var doc = JsonDocument.Parse( Text( index ) ).RootElement;
			Assert.AreEqual( "on", doc.GetProperty( "authentication" ).GetString() );
			var routes = doc.GetProperty( "routes" ).EnumerateArray().ToList();
			Assert.IsTrue( routes.Any( r => r.GetProperty( "path" ).GetString() == "/api/plandefs/{id}/start" && r.GetProperty( "scope" ).GetString() == "control" ), "each route lists its scope" );
			Assert.IsTrue( routes.Any( r => r.GetProperty( "path" ).GetString() == "/api/appstates" && r.GetProperty( "scope" ).GetString() == "read" ) );
			Expect( Call( world, HttpMethod.Get, "/api/help", null ), HttpStatusCode.OK, "GET /api/help (redirects to the manual)" );
			Expect( Call( world, HttpMethod.Get, "/api/help/HTTP.md", null ), HttpStatusCode.OK, "GET /api/help/HTTP.md" );

			// no token: 401, with the header and the usual JSON
			var none = Call( world, HttpMethod.Get, "/api/appstates", null );
			Expect( none, HttpStatusCode.Unauthorized, "GET /api/appstates without a token" );
			Assert.IsTrue( none.Headers.WwwAuthenticate.Any( h => h.Scheme == "Bearer" ), "WWW-Authenticate: Bearer" );
			StringAssert.Contains( JsonDocument.Parse( Text( none ) ).RootElement.GetProperty( "error" ).GetString(), "no token" );

			// read gets in
			Expect( Call( world, HttpMethod.Get, "/api/appstates", world.Token( "read" ) ), HttpStatusCode.OK, "GET /api/appstates with read" );

			// read does not act; control does - and control alone does not read
			var read = world.Token( "read" );
			var forbidden = Call( world, HttpMethod.Post, "/api/plandefs/all/start", read );
			Expect( forbidden, HttpStatusCode.Forbidden, "POST plandefs/all/start with read" );
			StringAssert.Contains( JsonDocument.Parse( Text( forbidden ) ).RootElement.GetProperty( "error" ).GetString(), "needs the scope control; the token has read" );

			var control = world.Token( "control" );
			ExpectSuccess( Call( world, HttpMethod.Post, "/api/plandefs/all/start", control ), "POST plandefs/all/start with control" );
			Expect( Call( world, HttpMethod.Get, "/api/appstates", control ), HttpStatusCode.Forbidden, "control does not imply read" );

			// the audit trail of who did it
			world.WaitUntil( "the master logs who started the plan", TimeSpan.FromSeconds( 10 ),
				() => LogText( world, world.Files.MasterMachine ).Contains( "system-test POST /api/plandefs/all/start" ) );

			// clear is a scope of its own
			Expect( Call( world, HttpMethod.Post, "/api/filedefs/logs.all/clear", control ), HttpStatusCode.Forbidden, "clear with control only" );
			Expect( Call( world, HttpMethod.Post, "/api/marks", world.Token( "control" ), "{\"operation\":\"clear\",\"fileDefs\":[{\"id\":\"logs.all\"}]}" ),
				HttpStatusCode.Forbidden, "POST /api/marks with operation clear needs sim as well" );
			var sim = world.Token( "control sim" );
			Assert.AreNotEqual( HttpStatusCode.Forbidden, Call( world, HttpMethod.Post, "/api/filedefs/logs.all/clear", sim ).StatusCode, "with sim the door is open (what the route answers is its own business)" );
			Assert.AreNotEqual( HttpStatusCode.Forbidden, Call( world, HttpMethod.Post, "/api/marks", sim, "{\"operation\":\"clear\",\"fileDefs\":[{\"id\":\"logs.all\"}]}" ).StatusCode );

			// the CLI as a whole needs control
			Expect( Call( world, HttpMethod.Post, "/api/cli", read, "GetAllAppsState" ), HttpStatusCode.Forbidden, "POST /api/cli with read" );
			Expect( Call( world, HttpMethod.Post, "/api/cli", control, "GetAllAppsState" ), HttpStatusCode.OK, "POST /api/cli with control" );

			// a route nobody described needs control
			Expect( Call( world, HttpMethod.Get, "/api/nothing/here", read ), HttpStatusCode.Forbidden, "an unmatched route fails closed" );

			// refused tokens: expired, another key, no alg, the wrong audience
			Expect( Call( world, HttpMethod.Get, "/api/appstates", world.Token( "read", TimeSpan.FromMinutes( -10 ) ) ), HttpStatusCode.Unauthorized, "an expired token" );

			using( var other = new Dirigent.TestBed.TokenMint() )
				Expect( Call( world, HttpMethod.Get, "/api/appstates", other.Mint( "read", "mallory", audience: Audience ) ), HttpStatusCode.Unauthorized, "a token of another key" );

			var unsigned = world.Tokens!.Mint( new Dictionary<string, object> { ["scope"] = "control read", ["exp"] = DateTimeOffset.UtcNow.AddHours( 1 ).ToUnixTimeSeconds(), ["aud"] = Audience }, alg: "none" );
			Expect( Call( world, HttpMethod.Get, "/api/appstates", unsigned ), HttpStatusCode.Unauthorized, "alg none" );

			var otherApp = world.Tokens!.Mint( "read", "x", audience: "another-application" );
			var wrongAudience = Call( world, HttpMethod.Get, "/api/appstates", otherApp );
			Expect( wrongAudience, HttpStatusCode.Unauthorized, "a token for another audience" );
			StringAssert.Contains( Text( wrongAudience ), "wrong audience" );

			// the web socket and the static folder need read too
			Expect( Call( world, HttpMethod.Get, "/websock", null ), HttpStatusCode.Unauthorized, "/websock without a token" );
			Expect( Call( world, HttpMethod.Get, "/index.html", null ), HttpStatusCode.Unauthorized, "the static folder without a token" );
		}

		[TestMethod()]
		public void AMasterWithAWebServerAndNoTokenKeyDoesNotStartAndSaysWhereItLooked()
		{
			var folder = Path.Combine( Path.GetTempPath(), "dirigent-nokey-" + Guid.NewGuid().ToString( "N" ).Substring( 0, 6 ) );
			Directory.CreateDirectory( folder );
			try
			{
				File.WriteAllText( Path.Combine( folder, "site.secret" ), new string( 'k', 40 ) );
				var exe = SystemWorld.Tool( "Dirigent.Agent.Console", "Dirigent.Agent.exe" );
				var local = Path.Combine( folder, "LocalConfig.xml" );

				var run = SystemWorld.RunTool( exe, new[] { "--security", "on", "--mode", "master", "--httpPort", SystemWorld.FreePort().ToString(), "--localConfigFile", local, "--logFile", Path.Combine( folder, "x.log" ) } );

				Assert.AreEqual( 5, run.ExitCode, run.All );
				StringAssert.Contains( run.All, "remoting-public.pem" );
				StringAssert.Contains( run.All, Path.Combine( folder, "remoting-public.pem" ) );
				StringAssert.Contains( run.All, Path.GetDirectoryName( exe )! );
			}
			finally
			{
				try { Directory.Delete( folder, true ); } catch { }
			}
		}

		[TestMethod()]
		public void AuditLetsEverythingThroughAndSaysWhatItWouldHaveRefused()
		{
			using var world = SystemWorld.Start( "ControlWorld", secured: true, cliNamed: false, tokens: true, securityMode: "audit" );

			var master = world.Files.MasterMachine;

			// no token at all, and it passes
			Expect( Call( world, HttpMethod.Get, "/api/appstates", null ), HttpStatusCode.OK, "GET /api/appstates without a token in audit mode" );
			ExpectSuccess( Call( world, HttpMethod.Post, "/api/plandefs/all/start", null ), "a call that acts, without a token" );

			// a token that is not enough passes as well
			ExpectSuccess( Call( world, HttpMethod.Post, "/api/plandefs/all/stop", world.Token( "read" ) ), "read for an acting route" );

			var index = JsonDocument.Parse( Text( Call( world, HttpMethod.Get, "/api", null ) ) ).RootElement;
			Assert.AreEqual( "audit", index.GetProperty( "authentication" ).GetString() );

			try
			{
			world.WaitUntil( "the would-be refusals are logged", TimeSpan.FromSeconds( 10 ), () =>
			{
				var log = LogText( world, master );
				return log.Contains( "audit: 127.0.0.1 GET /api/appstates would be refused (401): no token" )
					&& log.Contains( "audit: 127.0.0.1 POST /api/plandefs/all/stop would be refused (403): this route needs the scope control" );
			} );
			}
			catch( Exception e )
			{
				throw new Exception( e.Message + Environment.NewLine + string.Join( Environment.NewLine, LogText( world, master ).Split( (char) 10 ).Where( l => l.Contains( "audit" ) || l.Contains( "Warn" ) || l.Contains( "WARN" ) ).Take( 20 ) ) );
			}
		}
	}
}
