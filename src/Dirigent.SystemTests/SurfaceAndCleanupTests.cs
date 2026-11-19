using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System.Net.Http;
using System.Text;

namespace Dirigent.SystemTests
{
	/// <summary>The other remote-control surface: the web server.</summary>
	[TestClass()]
	public class RestSurfaceTests
	{
		[TestMethod()]
		public void TheWebServerAnswersTheSameCommands()
		{
			using var world = SystemWorld.Start( "LoggingWorld", withHttp: true );

			using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds( 20 ) };

			var url = $"http://127.0.0.1:{world.HttpPort}/api/cli";
			var body = new StringContent( "GetAllAppsState", Encoding.UTF8 );

			var response = http.PostAsync( url, body ).GetAwaiter().GetResult();
			var text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

			Assert.IsTrue( response.IsSuccessStatusCode,
				$"the web server answered: {(int) response.StatusCode} {text}" );

			StringAssert.Contains( text, "m1.camera",
				$"the applications came back over HTTP: {text}" );
		}

		[TestMethod()]
		public void TheFilesOfAnAppAndOfANodeAreListedOverHttp()
		{
			using var world = SystemWorld.Start( "LoggingWorld", withHttp: true );

			using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds( 30 ) };
			var root = $"http://127.0.0.1:{world.HttpPort}/api";

			var app = http.GetAsync( $"{root}/appdefs/m1.camera/files" ).GetAwaiter().GetResult();
			var appText = app.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.OK, app.StatusCode, appText );
			StringAssert.Contains( appText, "\"fileDefs\"", appText );
			StringAssert.Contains( appText, "recent.log", $"the seeded log is listed: {appText}" );

			var node = http.GetAsync( $"{root}/filedefs/log/files?machine=m1&app=camera" ).GetAwaiter().GetResult();
			var nodeText = node.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.OK, node.StatusCode, nodeText );
			StringAssert.Contains( nodeText, "recent.log", nodeText );

			var missing = http.GetAsync( $"{root}/appdefs/m1.nosuch/files" ).GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.NotFound, missing.StatusCode );
		}

		[TestMethod()]
		public void ApiErrorsAreJsonAndNotFoundsDoNotGetAnIpBanned()
		{
			using var world = SystemWorld.Start( "LoggingWorld", withHttp: true );

			using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds( 30 ) };
			var root = $"http://127.0.0.1:{world.HttpPort}/api";

			// a body a tool can parse, with the real reason in it
			var missing = http.GetAsync( $"{root}/downloads/{Guid.NewGuid()}" ).GetAwaiter().GetResult();
			var text = missing.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.NotFound, missing.StatusCode, text );
			Assert.AreEqual( "application/json", missing.Content.Headers.ContentType?.MediaType, text );
			var json = System.Text.Json.JsonDocument.Parse( text ).RootElement;
			Assert.AreEqual( 404, json.GetProperty( "code" ).GetInt32() );
			StringAssert.Contains( json.GetProperty( "error" ).GetString(), "No download" );

			// a malformed app id is the caller's mistake: 400, not 500
			var bad = http.PostAsync( $"{root}/downloads",
				new StringContent( "{\"app\":\"m1.\"}", Encoding.UTF8, "application/json" ) ).GetAwaiter().GetResult();
			var badText = bad.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.BadRequest, bad.StatusCode, badText );
			Assert.AreEqual( 400, System.Text.Json.JsonDocument.Parse( badText ).RootElement.GetProperty( "code" ).GetInt32() );

			var badFiles = http.GetAsync( $"{root}/appdefs/m1./files" ).GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.BadRequest, badFiles.StatusCode );

			// polling a job that is not there - a tool that lost track of its job - must never ban the tool
			for( int i = 0; i < 30; i++ )
			{
				var r = http.GetAsync( $"{root}/downloads/{Guid.NewGuid()}" ).GetAwaiter().GetResult();
				Assert.AreEqual( System.Net.HttpStatusCode.NotFound, r.StatusCode, $"request {i}" );
				Thread.Sleep( 30 ); // under the requests-per-second limit, which is another rule
			}
		}

		[TestMethod()]
		public void APackageIsCollectedAndFetchedOverHttp()
		{
			using var world = SystemWorld.Start( "LoggingWorld", withHttp: true );

			using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds( 90 ) };
			var root = $"http://127.0.0.1:{world.HttpPort}/api";

			var nodes = http.GetAsync( $"{root}/filedefs?type=FilePackage" ).GetAwaiter().GetResult();
			var nodesText = nodes.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.OK, nodes.StatusCode, nodesText );
			StringAssert.Contains( nodesText, "logs.all", nodesText );

			// one definition, in the listing shape; and the same id with a machine that has none: 404
			var one = http.GetAsync( $"{root}/filedefs/logs.all" ).GetAwaiter().GetResult();
			var oneText = one.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.OK, one.StatusCode, oneText );
			var oneDef = System.Text.Json.JsonDocument.Parse( oneText ).RootElement;
			Assert.AreEqual( 1, oneDef.GetArrayLength(), oneText );
			Assert.AreEqual( "FilePackage", oneDef[0].GetProperty( "type" ).GetString() );
			Assert.AreEqual( System.Net.HttpStatusCode.NotFound,
				http.GetAsync( $"{root}/filedefs/logs.all?machine=nosuchmachine" ).GetAwaiter().GetResult().StatusCode );
			Assert.AreEqual( System.Net.HttpStatusCode.NotFound,
				http.GetAsync( $"{root}/filedefs/nosuch" ).GetAwaiter().GetResult().StatusCode );

			// the files of that definition are the definition's own entry, typed as it is
			var pkgFiles = http.GetAsync( $"{root}/filedefs/logs.all/files" ).GetAwaiter().GetResult();
			var pkgText = pkgFiles.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.OK, pkgFiles.StatusCode, pkgText );
			var pkgEntry = System.Text.Json.JsonDocument.Parse( pkgText ).RootElement.GetProperty( "fileDefs" )[0];
			Assert.AreEqual( "FilePackage", pkgEntry.GetProperty( "type" ).GetString() );

			var post = http.PostAsync( $"{root}/downloads",
				new StringContent( "{\"fileDef\":{\"id\":\"logs.all\"},\"comment\":\"system test\"}", Encoding.UTF8, "application/json" ) )
				.GetAwaiter().GetResult();
			var postText = post.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.Accepted, post.StatusCode, postText );

			var id = System.Text.Json.JsonDocument.Parse( postText ).RootElement.GetProperty( "id" ).GetString();

			var status = http.GetAsync( $"{root}/downloads/{id}?wait=60" ).GetAwaiter().GetResult();
			var statusText = status.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.OK, status.StatusCode, statusText );

			var job = System.Text.Json.JsonDocument.Parse( statusText ).RootElement;
			Assert.AreEqual( "Finished", job.GetProperty( "status" ).GetString(), statusText );

			var url = job.GetProperty( "archives" )[0].GetProperty( "url" ).GetString();
			StringAssert.Contains( url, "/archives/", url );
			var file = http.GetAsync( $"http://127.0.0.1:{world.HttpPort}{url}" ).GetAwaiter().GetResult();
			var bytes = file.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.OK, file.StatusCode );
			Assert.AreEqual( "application/zip", file.Content.Headers.ContentType?.MediaType );
			Assert.IsTrue( bytes.Length > 0, "the archive came with its bytes" );
			Assert.AreEqual( (byte) 'P', bytes[0], "a zip" );

			// the title "Logs/All apps" gives a name with a space; it travelled as %20 in the url,
			// and the download header carries it in both the plain and the RFC 5987 form
			var fileName = job.GetProperty( "archives" )[0].GetProperty( "name" ).GetString()!;
			StringAssert.StartsWith( fileName, "All apps_" );
			Assert.IsTrue( url!.Contains( "%20" ), url );
			var disposition = string.Join( " ", file.Content.Headers.ContentDisposition?.ToString() ?? "" );
			StringAssert.Contains( disposition, "filename*=UTF-8''All%20apps_", disposition );

			var gone = http.DeleteAsync( $"{root}/downloads/{id}" ).GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.NoContent, gone.StatusCode );
			Assert.AreEqual( System.Net.HttpStatusCode.NotFound,
				http.GetAsync( $"{root}/downloads/{id}" ).GetAwaiter().GetResult().StatusCode );
		}
	}

	/// <summary>
	/// That a run leaves the machine as it found it.
	/// </summary>
	/// <remarks>
	/// The one test that must not use `using`: it takes the world down itself, and then looks at what
	/// is left. A leaked application or a leftover folder is the failure that quietly ruins every
	/// later run, and the only place it can be seen is after a teardown.
	/// </remarks>
	[TestClass()]
	public class CleanupTests
	{
		[TestMethod()]
		public void ARunLeavesNoProcessesAndNoFoldersBehind()
		{
			var world = SystemWorld.Start( "LoggingWorld" );
			var root = world.Root;

			world.Cli( "StartApp m1.camera" );
			world.WaitAppState( "m1.camera", "R" );

			world.Dispose();

			Assert.IsFalse( Directory.Exists( root ), $"the world's folder was removed: {root}" );

			var strays = SystemWorld.ProcessesMentioning( "Dirigent.TestApp.exe", root );
			Assert.AreEqual( 0, strays.Count,
				$"no application outlived the world: pids {string.Join( ", ", strays )}" );
		}
	}
}
