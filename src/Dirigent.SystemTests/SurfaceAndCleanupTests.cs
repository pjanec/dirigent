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
		public void AppDefinitionsSayWhereTheBinaryIsDeclaredAndTheAgentsSayWhereItIs()
		{
			using var world = SystemWorld.Start( "LoggingWorld", withHttp: true );

			using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds( 30 ) };
			var root = $"http://127.0.0.1:{world.HttpPort}/api";

			// declared: additive fields on the definition
			var def = http.GetAsync( $"{root}/appdefs/m1.camera" ).GetAwaiter().GetResult();
			var defText = def.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.OK, def.StatusCode, defText );
			var d = System.Text.Json.JsonDocument.Parse( defText ).RootElement;
			Assert.AreEqual( "m1.camera", d.GetProperty( "id" ).GetString() );
			Assert.AreEqual( "m1", d.GetProperty( "machine" ).GetString() );
			Assert.IsTrue( d.GetProperty( "exeFullPath" ).GetString()!.Length > 0, defText );
			Assert.IsTrue( d.TryGetProperty( "startupDir", out _ ) && d.TryGetProperty( "cmdLineArgs", out _ ), defText );

			// resolved: asked of the agent
			var all = http.GetAsync( $"{root}/applocations" ).GetAwaiter().GetResult();
			var allText = all.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.OK, all.StatusCode, allText );
			var list = System.Text.Json.JsonDocument.Parse( allText ).RootElement;
			Assert.AreEqual( 3, list.GetArrayLength(), allText );

			var one = http.GetAsync( $"{root}/appdefs/m1.camera/location" ).GetAwaiter().GetResult();
			var oneText = one.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.OK, one.StatusCode, oneText );
			var loc = System.Text.Json.JsonDocument.Parse( oneText ).RootElement;
			Assert.IsTrue( loc.GetProperty( "exe" ).GetProperty( "exists" ).GetBoolean(), oneText );
			Assert.AreEqual( System.Net.HttpStatusCode.NotFound,
				http.GetAsync( $"{root}/appdefs/m1.nosuch/location" ).GetAwaiter().GetResult().StatusCode );
		}

		[TestMethod()]
		public void TheManualIsServedAndAMarkIsAnsweredWithAReportOverHttp()
		{
			using var world = SystemWorld.Start( "LoggingWorld", withHttp: true );

			using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds( 90 ) };
			var root = $"http://127.0.0.1:{world.HttpPort}/api";

			// /api/help redirects to the page, so that its relative links reach the other pages
			var help = http.GetAsync( $"{root}/help" ).GetAwaiter().GetResult();
			var helpText = help.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.OK, help.StatusCode );
			Assert.AreEqual( "text/markdown", help.Content.Headers.ContentType?.MediaType );
			Assert.AreEqual( "utf-8", help.Content.Headers.ContentType?.CharSet );
			StringAssert.Contains( helpText, "GET /api/filedefs", "the reference of the API" );
			StringAssert.EndsWith( help.RequestMessage!.RequestUri!.AbsolutePath, "/api/help/HTTP.md" );

			var files = http.GetAsync( $"{root}/help/files.md" ).GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.OK, files.StatusCode, "case does not matter" );
			StringAssert.Contains( files.Content.ReadAsStringAsync().GetAwaiter().GetResult(), "file definition" );

			foreach( var name in new[] { "nosuch.md", "..%2Fversion.txt", "Note-3.1.19-Followup-SharedConfig-and-FileAPI.md" } )
			{
				var gone = http.GetAsync( $"{root}/help/{name}" ).GetAwaiter().GetResult();
				Assert.AreEqual( System.Net.HttpStatusCode.NotFound, gone.StatusCode, name );
				Assert.AreEqual( "application/json", gone.Content.Headers.ContentType?.MediaType, name );
			}

			// a mark: the report names what was skipped, and says why nothing was done
			var mark = http.PostAsync( $"{root}/filedefs/logs.all/mark", new StringContent( "" ) ).GetAwaiter().GetResult();
			var markText = mark.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.OK, mark.StatusCode, markText );
			var report = System.Text.Json.JsonDocument.Parse( markText ).RootElement;
			Assert.AreEqual( "mark", report.GetProperty( "operation" ).GetString() );
			Assert.IsTrue( report.GetProperty( "machines" ).GetArrayLength() > 0, markText );
			StringAssert.Contains( report.GetProperty( "message" ).GetString(), "Clearable", "the logs of this world are not Clearable" );

			Assert.AreEqual( System.Net.HttpStatusCode.NotFound,
				http.PostAsync( $"{root}/filedefs/nosuch/mark", new StringContent( "" ) ).GetAwaiter().GetResult().StatusCode );
			Assert.AreEqual( System.Net.HttpStatusCode.NotFound,
				http.PostAsync( $"{root}/appdefs/m1.nosuch/clear", new StringContent( "" ) ).GetAwaiter().GetResult().StatusCode );
		}

		[TestMethod()]
		public void ASelectionIsPreviewedDownloadedAndMarkedOverHttp()
		{
			using var world = SystemWorld.Start( "LoggingWorld", withHttp: true );

			using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds( 90 ) };
			var root = $"http://127.0.0.1:{world.HttpPort}/api";

			System.Net.Http.StringContent Json( string s ) => new( s, System.Text.Encoding.UTF8, "application/json" );
			const string selection = "{\"fileDefs\":[{\"id\":\"logs.all\"}],\"apps\":[\"m1.camera\"]}";

			// the preview: one item each, and the totals; it changes nothing
			var query = http.PostAsync( $"{root}/files/query", Json( selection ) ).GetAwaiter().GetResult();
			var queryText = query.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.OK, query.StatusCode, queryText );
			var q = System.Text.Json.JsonDocument.Parse( queryText ).RootElement;
			Assert.AreEqual( 2, q.GetProperty( "items" ).GetArrayLength(), queryText );
			Assert.IsTrue( q.GetProperty( "totals" ).GetProperty( "files" ).GetInt32() > 0, queryText );
			var firstFile = q.GetProperty( "items" )[0].GetProperty( "fileDefs" )[0].GetProperty( "files" )[0];
			Assert.AreEqual( "log", firstFile.GetProperty( "fileDefId" ).GetString(), queryText );

			Assert.AreEqual( System.Net.HttpStatusCode.NotFound,
				http.PostAsync( $"{root}/files/query", Json( "{\"fileDefs\":[{\"id\":\"nosuch\"}]}" ) ).GetAwaiter().GetResult().StatusCode );
			Assert.AreEqual( System.Net.HttpStatusCode.BadRequest,
				http.PostAsync( $"{root}/files/query", Json( "{}" ) ).GetAwaiter().GetResult().StatusCode );

			// two items, one job, one archive with a top folder per item
			var start = http.PostAsync( $"{root}/downloads", Json( selection.TrimEnd( '}' ) + ",\"comment\":\"selection\"}" ) ).GetAwaiter().GetResult();
			var startText = start.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.Accepted, start.StatusCode, startText );
			var jobId = System.Text.Json.JsonDocument.Parse( startText ).RootElement.GetProperty( "id" ).GetString();

			var done = http.GetAsync( $"{root}/downloads/{jobId}?wait=90" ).GetAwaiter().GetResult();
			var doneText = done.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			var job = System.Text.Json.JsonDocument.Parse( doneText ).RootElement;
			Assert.AreEqual( "Finished", job.GetProperty( "status" ).GetString(), doneText );

			var archiveUrl = job.GetProperty( "archives" )[0].GetProperty( "url" ).GetString();
			var zipBytes = http.GetByteArrayAsync( $"http://127.0.0.1:{world.HttpPort}{archiveUrl}" ).GetAwaiter().GetResult();
			using var zip = new System.IO.Compression.ZipArchive( new System.IO.MemoryStream( zipBytes ) );
			var names = zip.Entries.Select( e => e.FullName ).ToList();
			Assert.IsTrue( names.Any( n => n.Contains( "All apps/" ) ), string.Join( ", ", names ) );
			Assert.IsTrue( names.Any( n => n.Contains( "m1.camera/" ) ), string.Join( ", ", names ) );

			// the same selection marked: one report
			var mark = http.PostAsync( $"{root}/marks", Json( selection.TrimEnd( '}' ) + ",\"operation\":\"mark\"}" ) ).GetAwaiter().GetResult();
			var markText = mark.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.OK, mark.StatusCode, markText );
			Assert.AreEqual( "mark", System.Text.Json.JsonDocument.Parse( markText ).RootElement.GetProperty( "operation" ).GetString() );
			Assert.AreEqual( System.Net.HttpStatusCode.BadRequest,
				http.PostAsync( $"{root}/marks", Json( selection.TrimEnd( '}' ) + ",\"operation\":\"explode\"}" ) ).GetAwaiter().GetResult().StatusCode );

			// the group filter
			var groups = http.GetAsync( $"{root}/filedefs?type=FilePackage&group=nosuchgroup" ).GetAwaiter().GetResult().Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( "[]", groups );
		}

		[TestMethod()]
		public void TheSiteIsDescribedAndControlledOverHttp()
		{
			using var world = SystemWorld.Start( "ControlWorld", withHttp: true );

			using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds( 120 ) };
			var root = $"http://127.0.0.1:{world.HttpPort}/api";

			System.Text.Json.JsonElement Call( HttpMethod method, string url, System.Net.HttpStatusCode expected, string? body = null )
			{
				var req = new HttpRequestMessage( method, $"{root}/{url}" );
				if( body is not null ) req.Content = new StringContent( body, System.Text.Encoding.UTF8, "application/json" );
				var r = http.SendAsync( req ).GetAwaiter().GetResult();
				var text = r.Content.ReadAsStringAsync().GetAwaiter().GetResult();
				Assert.AreEqual( expected, r.StatusCode, $"{method} {url}: {text}" );
				return text.Length == 0 ? default : System.Text.Json.JsonDocument.Parse( text ).RootElement.Clone();
			}
			var ok = System.Net.HttpStatusCode.OK;
			var accepted = System.Net.HttpStatusCode.Accepted;

			// the master and its machines
			var status = Call( HttpMethod.Get, "status", ok );
			Assert.IsFalse( string.IsNullOrEmpty( status.GetProperty( "version" ).GetString() ) );
			Assert.AreEqual( 2, status.GetProperty( "sharedConfig" ).GetProperty( "machines" ).GetInt32() );
			Assert.AreEqual( world.HttpPort, status.GetProperty( "httpPort" ).GetInt32() );

			var machines = Call( HttpMethod.Get, "machinedefs", ok );
			Assert.AreEqual( 2, machines.GetArrayLength() );
			var m1 = Call( HttpMethod.Get, "machinedefs/m1", ok );
			Assert.IsTrue( m1.GetProperty( "connected" ).GetBoolean() );
			Assert.AreEqual( "m1.idler", m1.GetProperty( "apps" )[0].GetString() );
			Call( HttpMethod.Get, "machinedefs/nosuch", System.Net.HttpStatusCode.NotFound );
			Call( HttpMethod.Post, "machinedefs/nosuch/wakeup", System.Net.HttpStatusCode.NotFound );
			Call( HttpMethod.Post, "machinedefs/m1/wakeup", System.Net.HttpStatusCode.Conflict ); // already online

			// a plan, started and waited for
			var plan = Call( HttpMethod.Post, "plandefs/all/start?waitSeconds=60", ok, "{}" );
			Assert.AreEqual( "Success", plan.GetProperty( "state" ).GetProperty( "code" ).GetString(), plan.ToString() );
			Call( HttpMethod.Post, "plandefs/nosuch/start", System.Net.HttpStatusCode.NotFound );
			Call( HttpMethod.Post, "plandefs/all/start", System.Net.HttpStatusCode.BadRequest, "{ not json" );
			Call( HttpMethod.Post, "plandefs/all/start?waitSeconds=abc", System.Net.HttpStatusCode.BadRequest );

			// an application killed, and waited for
			var killed = Call( HttpMethod.Post, "appdefs/m1.idler/kill?waitSeconds=30", ok );
			Assert.IsFalse( killed.GetProperty( "state" ).GetProperty( "flags" ).GetString()!.Contains( 'R' ), killed.ToString() );
			Call( HttpMethod.Post, "appdefs/m1.nosuch/kill", System.Net.HttpStatusCode.NotFound );

			// a script started by name and waited for: the answer holds what it returned
			var script = Call( HttpMethod.Post, "scripts?waitSeconds=30", ok, "{\"name\":\"BuiltIns/ListVfsNodes.cs\",\"args\":{}}" );
			Assert.AreEqual( "Finished", script.GetProperty( "status" ).GetString(), script.ToString() );
			Assert.IsTrue( script.TryGetProperty( "data", out _ ) );
			Call( HttpMethod.Post, "scripts", System.Net.HttpStatusCode.BadRequest, "{}" );

			var started = Call( HttpMethod.Post, "scripts", accepted, "{\"name\":\"BuiltIns/ListVfsNodes.cs\"}" );
			var scriptId = started.GetProperty( "id" ).GetString();
			Call( HttpMethod.Post, $"scripts/{Guid.NewGuid()}/kill", System.Net.HttpStatusCode.NotFound );
			Call( HttpMethod.Post, $"scripts/{scriptId}/kill", accepted );
			Call( HttpMethod.Post, $"scripts/{Guid.NewGuid()}/start", System.Net.HttpStatusCode.NotFound );
			// a script the configuration defines: started by either name of the same operation
			foreach( var form in new[] { "scriptdefs", "scripts" } )
			{
				var defined = Call( HttpMethod.Post, $"{form}/{Dirigent.TestBed.Scenarios.Scenario.ControlScriptId}/start?waitSeconds=30", ok );
				Assert.AreEqual( "Finished", defined.GetProperty( "status" ).GetString(), $"{form}: {defined}" );
			}
			var nodef = Call( HttpMethod.Post, $"scriptdefs/{Guid.NewGuid()}/start", System.Net.HttpStatusCode.NotFound );
			StringAssert.Contains( nodef.GetProperty( "error" ).GetString(), "POST /api/scripts" );

			// variables, and Kill All
			Call( HttpMethod.Post, "vars", accepted, "{\"vars\":{\"SITE\":\"test\"}}" );
			Call( HttpMethod.Post, "vars", System.Net.HttpStatusCode.BadRequest, "{}" );
			Call( HttpMethod.Post, "killall", System.Net.HttpStatusCode.NotFound, "{\"machine\":\"nosuch\"}" );
			Call( HttpMethod.Post, "killall", accepted );
		}

		[TestMethod()]
		public void StateEndpointsAnswerToThePluralAndTheOlderSingularName()
		{
			using var world = SystemWorld.Start( "WaitingWorld", withHttp: true ); // has the plan "never"

			using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds( 20 ) };
			var root = $"http://127.0.0.1:{world.HttpPort}/api";

			string Get( string url, System.Net.HttpStatusCode expected )
			{
				var r = http.GetAsync( $"{root}/{url}" ).GetAwaiter().GetResult();
				var text = r.Content.ReadAsStringAsync().GetAwaiter().GetResult();
				Assert.AreEqual( expected, r.StatusCode, $"{url}: {text}" );
				return text;
			}

			var ok = System.Net.HttpStatusCode.OK;

			// the app: both names, the same answer
			var plural = Get( "appstates/m1.camera", ok );
			Assert.AreEqual( plural, Get( "appstate/m1.camera", ok ) );
			StringAssert.Contains( plural, "\"state\"" );

			// the plan, likewise
			var planPlural = Get( "planstates/never", ok );
			Assert.AreEqual( planPlural, Get( "planstate/never", ok ) );
			StringAssert.Contains( planPlural, "\"state\"" );
			Get( "plandefs/never", ok );

			// the unknown ones are 404 under either name, and a script id that is no guid is the caller's mistake
			var notFound = System.Net.HttpStatusCode.NotFound;
			Get( "appstates/m1.nosuch", notFound );
			Get( "appstate/m1.nosuch", notFound );
			Get( "planstates/nosuchplan", notFound );
			Get( "planstate/nosuchplan", notFound );
			Get( "plandefs/nosuchplan", notFound );
			Get( "scriptstates/not-a-guid", System.Net.HttpStatusCode.BadRequest );
			Get( "scriptstate/not-a-guid", System.Net.HttpStatusCode.BadRequest );
			Get( $"scriptstates/{Guid.NewGuid()}", notFound );
			Get( $"scriptstate/{Guid.NewGuid()}", notFound );
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
