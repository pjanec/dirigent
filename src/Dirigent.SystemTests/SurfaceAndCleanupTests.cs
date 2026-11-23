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

			// one JSON string, as documented - the lines joined with escaped line breaks
			var lines = System.Text.Json.JsonDocument.Parse( text ).RootElement.GetString()!;
			StringAssert.EndsWith( lines.TrimEnd(), "END", lines );
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

		/// <summary>
		/// A route that takes no body must still answer a caller that sends one - '{}' with a JSON content type is what a
		/// generic client sends, and Windows PowerShell asks with Expect: 100-continue first. Unread, the body arrived on a
		/// connection the server had answered and closed, and the caller got a reset instead of the answer.
		/// </summary>
		[TestMethod()]
		public void ARouteWithoutABodyAnswersACallerThatSendsOne()
		{
			using var world = SystemWorld.Start( "LoggingWorld", withHttp: true );

			using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds( 90 ) };
			var root = $"http://127.0.0.1:{world.HttpPort}/api";

			var calls = new (HttpMethod Method, string Route, System.Net.HttpStatusCode Expected)[]
			{
				( HttpMethod.Post, "filedefs/logs.all/mark", System.Net.HttpStatusCode.OK ),
				( HttpMethod.Post, "filedefs/logs.all/unmark", System.Net.HttpStatusCode.OK ),
				( HttpMethod.Post, "appdefs/m1.camera/unmark", System.Net.HttpStatusCode.OK ),
				( HttpMethod.Post, $"scripts/{Guid.NewGuid()}/kill", System.Net.HttpStatusCode.NotFound ),
				( HttpMethod.Delete, $"downloads/{Guid.NewGuid()}", System.Net.HttpStatusCode.NotFound ),
			};

			foreach( var (method, route, expected) in calls )
			{
				for( int i = 0; i < 3; i++ ) // a reset is a race; give it a few chances
				{
					using var request = new HttpRequestMessage( method, $"{root}/{route}" )
					{
						Content = new StringContent( "{}", System.Text.Encoding.UTF8, "application/json" ),
					};
					request.Headers.ExpectContinue = true;

					var response = http.Send( request );
					var text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
					Assert.AreEqual( expected, response.StatusCode, $"{method} {route}: {text}" );
				}
			}
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

			// the site's name, from <Shared Name="...">
			Assert.AreEqual( "Control world", Call( HttpMethod.Get, "status", ok ).GetProperty( "siteName" ).GetString() );
			Assert.AreEqual( "Control world", Call( HttpMethod.Get, "", ok ).GetProperty( "siteName" ).GetString() );

			// a running application's state, as fields
			var running = Call( HttpMethod.Get, "appstates/m1.idler", ok ).GetProperty( "state" );
			Assert.IsTrue( running.GetProperty( "pid" ).GetInt32() > 0, running.ToString() );
			Assert.AreEqual( "all", running.GetProperty( "plan" ).GetString(), running.ToString() );
			Assert.AreEqual( System.Text.Json.JsonValueKind.Null, running.GetProperty( "exitCode" ).ValueKind, "no exit code while it runs" );
			Assert.IsFalse( running.GetProperty( "offline" ).GetBoolean(), running.ToString() );
			Assert.IsTrue( running.GetProperty( "reportAgeSeconds" ).GetDouble() < 10, running.ToString() );
			var startedAt = running.GetProperty( "startedAt" ).GetDateTime();
			Assert.IsTrue( Math.Abs( ( DateTime.UtcNow - startedAt.ToUniversalTime() ).TotalMinutes ) < 5, $"started a moment ago: {running}" );
			Assert.AreEqual( System.Text.Json.JsonValueKind.Null, running.GetProperty( "endedAt" ).ValueKind, "no end while it runs" );

			// the CLI line keeps its fields where they were; the start and the end come after the PID
			var line = world.Cli( "GetAppState m1.idler" ).Trim().Split( ':' );
			Assert.AreEqual( "APP", line[0] );
			Assert.AreEqual( "m1.idler", line[1] );
			Assert.IsTrue( line[2].Contains( 'R' ), string.Join( ":", line ) );
			Assert.AreEqual( "all", line[8], string.Join( ":", line ) );
			Assert.IsTrue( line.Length >= 12 && System.Text.RegularExpressions.Regex.IsMatch( line[10], @"^\d{8}T\d{6}Z$" ) && line[11] == "",
				"StartedAt (and no EndedAt) after the PID: " + string.Join( ":", line ) );

			// the component, named explicitly by the application
			Assert.AreEqual( "Tools", Call( HttpMethod.Get, "appdefs/m2.broken", ok ).GetProperty( "component" ).GetString() );
			Assert.AreEqual( System.Text.Json.JsonValueKind.Null, Call( HttpMethod.Get, "appdefs/m1.idler", ok ).GetProperty( "component" ).ValueKind );

			// the locations carry the application's groups, so that versions can be shown per component
			var locations = Call( HttpMethod.Get, "applocations?machine=m1", ok );
			Assert.IsTrue( locations.EnumerateArray().All( l => l.TryGetProperty( "groups", out _ ) ), locations.ToString() );

			// an application killed, and waited for
			var killed = Call( HttpMethod.Post, "appdefs/m1.idler/kill?waitSeconds=30", ok );
			Assert.IsFalse( killed.GetProperty( "state" ).GetProperty( "flags" ).GetString()!.Contains( 'R' ), killed.ToString() );
			Assert.AreNotEqual( System.Text.Json.JsonValueKind.Null, killed.GetProperty( "state" ).GetProperty( "endedAt" ).ValueKind, $"the end is recorded: {killed}" );
			Call( HttpMethod.Post, "appdefs/m1.nosuch/kill", System.Net.HttpStatusCode.NotFound );

			// a start that fails is said at once, not at the end of the wait (seen on a site: 20 s for a missing program)
			var clock = System.Diagnostics.Stopwatch.StartNew();
			var broken = Call( HttpMethod.Post, "appdefs/m2.broken/start?waitSeconds=60", System.Net.HttpStatusCode.Conflict );
			StringAssert.Contains( broken.GetProperty( "error" ).GetString(), "failed to start", broken.ToString() );
			Assert.IsTrue( clock.Elapsed < TimeSpan.FromSeconds( 15 ), $"the failure took {clock.Elapsed} to be told" );

			// a machine nobody serves: refused, rather than a 202 for a start that goes nowhere
			var ghost = Call( HttpMethod.Post, "appdefs/ghost.app/start", System.Net.HttpStatusCode.Conflict );
			StringAssert.Contains( ghost.GetProperty( "error" ).GetString(), "not connected", ghost.ToString() );

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
			// the same answer - but for the age of the last report, which moves between two calls
			string WithoutAge( string json ) => System.Text.RegularExpressions.Regex.Replace( json, "\"reportAgeSeconds\":[^,}]*", "" );
			var plural = Get( "appstates/m1.camera", ok );
			Assert.AreEqual( WithoutAge( plural ), WithoutAge( Get( "appstate/m1.camera", ok ) ) );
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
		public void TheStatusCodesAreTheDocumentedOnes()
		{
			// each line was answered differently on a real 7-machine site - see HTTP.md, Conventions
			using var world = SystemWorld.Start( "ControlWorld", withHttp: true );

			using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds( 30 ) };
			var root = $"http://127.0.0.1:{world.HttpPort}/api";

			System.Text.Json.JsonElement Send( HttpMethod method, string url, System.Net.HttpStatusCode expected, string? body = null, string type = "application/json" )
			{
				var req = new HttpRequestMessage( method, $"{root}/{url}" );
				if( body is not null ) req.Content = new StringContent( body, Encoding.UTF8, type );
				var r = http.SendAsync( req ).GetAwaiter().GetResult();
				var text = r.Content.ReadAsStringAsync().GetAwaiter().GetResult();
				Assert.AreEqual( expected, r.StatusCode, $"{method} {url}: {text}" );
				Assert.AreEqual( "application/json", r.Content.Headers.ContentType?.MediaType, $"{method} {url}: {text}" );
				return System.Text.Json.JsonDocument.Parse( text ).RootElement.Clone();
			}
			string Error( System.Text.Json.JsonElement e ) => e.GetProperty( "error" ).GetString() ?? "";

			var badRequest = System.Net.HttpStatusCode.BadRequest;
			var notFound = System.Net.HttpStatusCode.NotFound;

			// an id that cannot be parsed is 400, everywhere
			Send( HttpMethod.Get, "appdefs/m1.", badRequest );
			Send( HttpMethod.Get, "appstates/m1.", badRequest );
			Send( HttpMethod.Get, "downloads/not-a-guid", badRequest );

			// every 404 says what is missing
			StringAssert.Contains( Error( Send( HttpMethod.Get, "appdefs/m1.nosuch", notFound ) ), "m1.nosuch" );
			StringAssert.Contains( Error( Send( HttpMethod.Get, "clients/nosuch", notFound ) ), "nosuch" );
			StringAssert.Contains( Error( Send( HttpMethod.Get, "plandefs/nosuch", notFound ) ), "nosuch" );
			StringAssert.Contains( Error( Send( HttpMethod.Get, "appstates/m1.idler@nosuch", notFound ) ), "nosuch" );
			StringAssert.Contains( Error( Send( HttpMethod.Get, $"scriptdefs/{Guid.NewGuid()}", notFound ) ), "script" );

			// a filter naming something unknown is not an empty answer
			StringAssert.Contains( Error( Send( HttpMethod.Get, "applocations?machine=nosuch", notFound ) ), "nosuch" );
			StringAssert.Contains( Error( Send( HttpMethod.Get, "filedefs?type=Nonsense", badRequest ) ), "FilePackage" );

			// a script the master does not have is not started to fail
			StringAssert.Contains( Error( Send( HttpMethod.Post, "scripts", notFound, "{\"name\":\"BuiltIns/NoSuch.cs\"}" ) ), "NoSuch" );

			// a file that is XML but not a shared config: the caller's mistake, and the running config stays
			var notShared = Path.Combine( world.Root, "not-a-shared-config.xml" );
			File.WriteAllText( notShared, "<Local/>" );
			StringAssert.Contains( Error( Send( HttpMethod.Post, "sharedconfig/reload", badRequest,
				System.Text.Json.JsonSerializer.Serialize( new { file = notShared, killApps = false } ) ) ), "<Shared>" );

			// the CLI: nothing to run is 400; an unknown app is an ERROR like any unknown id
			Send( HttpMethod.Post, "cli", badRequest, "", "text/plain" );
			var cli = Send( HttpMethod.Post, "cli", System.Net.HttpStatusCode.OK, "GetAppState m1.nosuch", "text/plain" );
			StringAssert.StartsWith( cli.GetString(), "ERROR", cli.ToString() );

			// a path no route takes is still answered as the API answers
			Send( HttpMethod.Get, "help/..%5C..%5CDirigent.Agent.dll.config", notFound );

			// the plan says what it marks
			var plan = Send( HttpMethod.Get, "plandefs/all", System.Net.HttpStatusCode.OK );
			Assert.AreEqual( 0, plan.GetProperty( "markFiles" ).GetArrayLength(), plan.ToString() );
			Assert.AreEqual( 0, plan.GetProperty( "clearFiles" ).GetArrayLength(), plan.ToString() );
			Assert.AreEqual( 60.0, plan.GetProperty( "markTimeout" ).GetDouble(), plan.ToString() );
		}

		[TestMethod()]
		public void TheApiDescribesItselfAndTheRootLeadsThere()
		{
			// what a tool or an AI agent that knows only the address finds
			using var world = SystemWorld.Start( "TwoMachines", withHttp: true );

			using var noRedirect = new HttpClient( new HttpClientHandler() { AllowAutoRedirect = false } ) { Timeout = TimeSpan.FromSeconds( 30 ) };
			using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds( 30 ) };
			var host = $"http://127.0.0.1:{world.HttpPort}";

			var root = noRedirect.GetAsync( $"{host}/" ).GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.Found, root.StatusCode );
			Assert.IsTrue( root.Headers.Location?.OriginalString.TrimEnd( '/' ).EndsWith( "/api" ) == true, root.Headers.Location?.OriginalString );

			foreach( var path in new[] { "/api", "/api/", "/" } ) // the last one followed through the redirect
			{
				var r = http.GetAsync( host + path ).GetAwaiter().GetResult();
				var text = r.Content.ReadAsStringAsync().GetAwaiter().GetResult();
				Assert.AreEqual( System.Net.HttpStatusCode.OK, r.StatusCode, $"{path}: {text}" );
				Assert.AreEqual( "application/json", r.Content.Headers.ContentType?.MediaType, path );

				var index = System.Text.Json.JsonDocument.Parse( text ).RootElement;
				Assert.AreEqual( "Dirigent", index.GetProperty( "name" ).GetString() );
				Assert.AreEqual( "/api/help/HTTP.md", index.GetProperty( "help" ).GetString() );
				Assert.IsTrue( index.GetProperty( "start" ).GetArrayLength() > 0 );
				Assert.IsTrue( index.GetProperty( "docs" ).EnumerateArray().Any( d => d.GetString() == "/api/help/Files.md" ), text );
			}

			// every route is listed, and every one has its section in the manual - a route added without documentation fails here
			var manual = http.GetStringAsync( $"{host}/api/help/HTTP.md" ).GetAwaiter().GetResult();
			var all = System.Text.Json.JsonDocument.Parse( http.GetStringAsync( $"{host}/api" ).GetAwaiter().GetResult() ).RootElement.GetProperty( "routes" );
			var undocumented = new List<string>();
			foreach( var route in all.EnumerateArray() )
			{
				var help = route.GetProperty( "help" ).GetString()!;
				if( !help.Contains( '#' ) ) undocumented.Add( $"{route.GetProperty( "method" ).GetString()} {route.GetProperty( "path" ).GetString()}" );
			}
			Assert.AreEqual( 0, undocumented.Count, "routes with no section in HTTP.md: " + string.Join( ", ", undocumented ) );
			Assert.IsTrue( all.GetArrayLength() > 50, $"{all.GetArrayLength()} routes" );

			// JSON over HTTP starts with JSON, not with a byte order mark - strict parsers (Windows PowerShell's) refuse it
			foreach( var (method, path, body) in new (HttpMethod, string, string?)[] {
				( HttpMethod.Get, "/api", null ), ( HttpMethod.Get, "/api/status", null ), ( HttpMethod.Get, "/api/appstates", null ),
				( HttpMethod.Get, "/api/filedefs", null ), ( HttpMethod.Get, "/api/appdefs/nosuch.app", null ), ( HttpMethod.Post, "/api/cli", "GetAllPlansState" ) } )
			{
				var req = new HttpRequestMessage( method, host + path );
				if( body is not null ) req.Content = new StringContent( body, Encoding.UTF8, "text/plain" );
				var bytes = http.SendAsync( req ).GetAwaiter().GetResult().Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
				Assert.IsTrue( bytes.Length > 0 && ( bytes[0] == '{' || bytes[0] == '[' || bytes[0] == '"' ),
					$"{method} {path} starts with 0x{( bytes.Length > 0 ? bytes[0] : 0 ):X2}" );
			}
			Assert.IsTrue( all.EnumerateArray().Any( r => r.GetProperty( "path" ).GetString() == "/api/status"
				&& r.GetProperty( "help" ).GetString() == "/api/help/HTTP.md#GET-apistatus" ) );
			StringAssert.Contains( manual, "### GET /api/status" );
		}

		[TestMethod()]
		public void ARunningPlanTakesCheckpointsAndACollectionSinceATimeStartsThere()
		{
			// the plan marks the log when it starts and takes a checkpoint of it every 10 s while it runs; a collection
			// "since" a time after a checkpoint holds what was written since that checkpoint - nothing older
			using var world = SystemWorld.Start( "CheckpointWorld", withHttp: true );

			using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds( 120 ) };
			var root = $"http://127.0.0.1:{world.HttpPort}/api";
			System.Text.Json.JsonElement Json( string text ) => System.Text.Json.JsonDocument.Parse( text ).RootElement.Clone();

			var logs = world.Files.Apps.Single( a => a.IdTuple == "m1.ticker" ).LogsDir;
			Directory.CreateDirectory( logs );
			var log = Path.Combine( logs, "run.log" );
			File.WriteAllText( log, "before the run\n" );

			var plan = Json( http.GetStringAsync( $"{root}/plandefs/run" ).GetAwaiter().GetResult() );
			Assert.AreEqual( 10.0, plan.GetProperty( "markInterval" ).GetDouble(), plan.ToString() );

			var started = http.PostAsync( $"{root}/plandefs/run/start?waitSeconds=60", null ).GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.OK, started.StatusCode, started.Content.ReadAsStringAsync().GetAwaiter().GetResult() );

			// the marking at the start is in the plan's state (a derived answer type: it was once dropped by the serializer)
			var afterStart = Json( http.GetStringAsync( $"{root}/planstates/run" ).GetAwaiter().GetResult() ).GetProperty( "state" );
			Assert.IsTrue( afterStart.TryGetProperty( "markResult", out var mr ) && mr.GetProperty( "marked" ).GetInt32() == 1, afterStart.ToString() );

			File.AppendAllText( log, "during the run, before the checkpoint\n" );

			// the first checkpoint comes about 10 s after the start
			System.Text.Json.JsonElement state = default;
			var clock = System.Diagnostics.Stopwatch.StartNew();
			while( clock.Elapsed < TimeSpan.FromSeconds( 40 ) )
			{
				state = Json( http.GetStringAsync( $"{root}/planstates/run" ).GetAwaiter().GetResult() ).GetProperty( "state" );
				if( state.TryGetProperty( "lastCheckpoint", out var lc ) && lc.ValueKind != System.Text.Json.JsonValueKind.Null ) break;
				Thread.Sleep( 1000 );
			}
			Assert.IsTrue( state.TryGetProperty( "lastCheckpoint", out var last ) && last.ValueKind != System.Text.Json.JsonValueKind.Null,
				$"a checkpoint within 40 s: {state}\nplan: {plan}\nmaster log:\n" + string.Join( "\n", new StreamReader( new FileStream( world.Files.LogOf( world.Files.MasterMachine ), FileMode.Open, FileAccess.Read, FileShare.ReadWrite ) ).ReadToEnd().Split( '\n' )
					.Where( l => l.Contains( "Plan run" ) || l.Contains( "heckpoint" ) || l.Contains( "Mark" ) || l.Contains( "ERROR" ) || l.Contains( "WARN" ) ).TakeLast( 30 ) ) );

			// a checkpoint is the plan's own business: no message box for anybody (it once sent one to every client, every interval)
			Thread.Sleep( 2000 );
			var traffic = Json( http.GetStringAsync( $"{root}/status" ).GetAwaiter().GetResult() ).GetProperty( "traffic" );
			Assert.IsFalse( traffic.GetProperty( "sent" ).EnumerateArray().Any( t => t.GetProperty( "type" ).GetString() == "UserNotificationMessage" ),
				$"no notification from the marking or the checkpoints: {traffic}" );

			Thread.Sleep( 1500 );
			var since = DateTime.UtcNow;
			Thread.Sleep( 1500 );
			File.AppendAllText( log, "after the time asked for\n" );

			var body = System.Text.Json.JsonSerializer.Serialize( new { fileDef = new { id = "log" }, since = since.ToString( "o" ) } );
			var post = http.PostAsync( $"{root}/downloads", new StringContent( body, Encoding.UTF8, "application/json" ) ).GetAwaiter().GetResult();
			var id = Json( post.Content.ReadAsStringAsync().GetAwaiter().GetResult() ).GetProperty( "id" ).GetString();
			var job = Json( http.GetStringAsync( $"{root}/downloads/{id}?wait=60" ).GetAwaiter().GetResult() );
			Assert.AreEqual( "Finished", job.GetProperty( "status" ).GetString(), job.ToString() );

			var bytes = http.GetByteArrayAsync( $"http://127.0.0.1:{world.HttpPort}{job.GetProperty( "archives" )[0].GetProperty( "url" ).GetString()}" ).GetAwaiter().GetResult();
			using var zip = new System.IO.Compression.ZipArchive( new MemoryStream( bytes ) );
			var entry = zip.Entries.Single( e => e.Name.StartsWith( "run" ) && e.Name.EndsWith( ".log" ) );
			using var reader = new StreamReader( entry.Open() );
			var text = reader.ReadToEnd();

			StringAssert.Contains( text, "after the time asked for", text );
			Assert.IsFalse( text.Contains( "before the run" ), $"nothing from before the plan's mark: {text}" );
			Assert.IsFalse( text.Contains( "before the checkpoint" ), $"nothing from before the checkpoint: {text}" );

			using var comment = new StreamReader( zip.GetEntry( "_comment.txt" )!.Open() );
			var cover = comment.ReadToEnd();
			StringAssert.Contains( cover, "Window    :", "the cover note says the time asked for" );
			Assert.IsFalse( cover.Contains( "Since     :" ), $"the Window line says where the files start; no mark line repeats it: {cover}" );
		}

		[TestMethod()]
		public void TheBanIsAnsweredAsTheApiAnswers()
		{
			// more than the limit in one second bans the address - this master's only, and the world goes with the test
			using var world = SystemWorld.Start( "TwoMachines", withHttp: true );

			using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds( 30 ) };
			var root = $"http://127.0.0.1:{world.HttpPort}/api";

			var burst = Enumerable.Range( 0, 300 ).Select( _ => http.GetAsync( $"{root}/downloads" ) ).ToArray();
			System.Threading.Tasks.Task.WaitAll( burst );
			Assert.IsTrue( burst.Any( t => t.Result.StatusCode == System.Net.HttpStatusCode.Forbidden ),
				"300 requests at once are over the limit: " + string.Join( ",", burst.Select( t => (int) t.Result.StatusCode ).Distinct() ) );

			var r = http.GetAsync( $"{root}/status" ).GetAwaiter().GetResult();
			var text = r.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.Forbidden, r.StatusCode, text );
			Assert.AreEqual( "application/json", r.Content.Headers.ContentType?.MediaType, text );
			var error = System.Text.Json.JsonDocument.Parse( text ).RootElement.GetProperty( "error" ).GetString();
			StringAssert.Contains( error, "banned for 10 minutes", error );
			StringAssert.Contains( error, "/api/appstates", "and it says what to do instead: " + error );
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

			// beside the files, the state of the site: readable, the versions first, the applications as facts
			using( var zip = new System.IO.Compression.ZipArchive( new MemoryStream( bytes ) ) )
			{
				var entry = zip.GetEntry( "_sitestate.json" );
				Assert.IsNotNull( entry, "entries: " + string.Join( ", ", zip.Entries.Select( e => e.FullName ) ) );
				using var reader = new StreamReader( entry!.Open() );
				var text = reader.ReadToEnd();
				StringAssert.Contains( text, "\n  \"", "indented, for a person to read" );
				var site = System.Text.Json.JsonDocument.Parse( text ).RootElement;
				Assert.AreEqual( "system test", site.GetProperty( "comment" ).GetString(), text );
				Assert.IsTrue( site.GetProperty( "versions" ).GetArrayLength() > 0, text );
				Assert.IsTrue( site.GetProperty( "machines" ).EnumerateArray().Any( m => m.GetProperty( "id" ).GetString() == "m1" ), text );
				Assert.IsTrue( site.GetProperty( "apps" ).EnumerateArray().Any( a => a.GetProperty( "id" ).GetString() == "m1.camera" ), text );
				var names = site.EnumerateObject().Select( p => p.Name ).ToList();
				Assert.IsTrue( names.IndexOf( "versions" ) < names.IndexOf( "apps" ), "the versions before the applications: " + string.Join( ",", names ) );
			}

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

			// a name of the caller's: used as given, ".zip" added, what a file name cannot hold made '_'
			var named = http.PostAsync( $"{root}/downloads",
				new StringContent( "{\"fileDef\":{\"id\":\"logs.all\"},\"name\":\"Bug 4711: site logs\",\"siteState\":false}", Encoding.UTF8, "application/json" ) )
				.GetAwaiter().GetResult();
			var namedId = System.Text.Json.JsonDocument.Parse( named.Content.ReadAsStringAsync().GetAwaiter().GetResult() ).RootElement.GetProperty( "id" ).GetString();
			var namedJob = System.Text.Json.JsonDocument.Parse(
				http.GetStringAsync( $"{root}/downloads/{namedId}?wait=60" ).GetAwaiter().GetResult() ).RootElement;
			Assert.AreEqual( "Bug 4711_ site logs.zip", namedJob.GetProperty( "archives" )[0].GetProperty( "name" ).GetString(), namedJob.ToString() );
			Assert.IsTrue( namedJob.GetProperty( "archives" )[0].GetProperty( "size" ).GetInt64() > 0,
				"the size is known once the job is finished - before anything is downloaded" );
			var namedBytes = http.GetByteArrayAsync( $"http://127.0.0.1:{world.HttpPort}{namedJob.GetProperty( "archives" )[0].GetProperty( "url" ).GetString()}" ).GetAwaiter().GetResult();
			using( var namedZip = new System.IO.Compression.ZipArchive( new MemoryStream( namedBytes ) ) )
				Assert.IsNull( namedZip.GetEntry( "_sitestate.json" ), "\"siteState\": false leaves it out" );
			http.DeleteAsync( $"{root}/downloads/{namedId}" ).GetAwaiter().GetResult();
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

		[TestMethod()]
		public void ACollectionOfATimeWindowIsCutOnBothSidesAndThePreviewListsTheSameFiles()
		{
			// the plan checkpoints the log every 10 s. A window starts after the first checkpoint and ends before the second:
			// the file is cut from the first checkpoint to the second - what was written before the first, and after the second, is not in it
			using var world = SystemWorld.Start( "CheckpointWorld", withHttp: true );

			using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds( 120 ) };
			var root = $"http://127.0.0.1:{world.HttpPort}/api";
			System.Text.Json.JsonElement Json( string text ) => System.Text.Json.JsonDocument.Parse( text ).RootElement.Clone();
			System.Net.Http.StringContent Body( object o ) => new( System.Text.Json.JsonSerializer.Serialize( o ), Encoding.UTF8, "application/json" );

			var logs = world.Files.Apps.Single( a => a.IdTuple == "m1.ticker" ).LogsDir;
			Directory.CreateDirectory( logs );
			var log = Path.Combine( logs, "run.log" );
			File.WriteAllText( log, "before the run\n" );

			// another log of the application, from yesterday: well inside the two days of the definition, outside the window
			var yesterday = Path.Combine( logs, "yesterday.log" );
			File.WriteAllText( yesterday, "yesterday's run\n" );
			File.SetCreationTimeUtc( yesterday, DateTime.UtcNow.AddDays( -1 ) );
			File.SetLastWriteTimeUtc( yesterday, DateTime.UtcNow.AddDays( -1 ).AddHours( 1 ) );

			var started = http.PostAsync( $"{root}/plandefs/run/start?waitSeconds=60", null ).GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.OK, started.StatusCode, started.Content.ReadAsStringAsync().GetAwaiter().GetResult() );

			File.AppendAllText( log, "during the run, before the checkpoint\n" );

			string? LastCheckpoint()
			{
				var state = Json( http.GetStringAsync( $"{root}/planstates/run" ).GetAwaiter().GetResult() ).GetProperty( "state" );
				return state.TryGetProperty( "lastCheckpoint", out var lc ) && lc.ValueKind != System.Text.Json.JsonValueKind.Null ? lc.ToString() : null;
			}

			string WaitForCheckpointOtherThan( string? previous )
			{
				var clock = System.Diagnostics.Stopwatch.StartNew();
				while( clock.Elapsed < TimeSpan.FromSeconds( 40 ) )
				{
					var now = LastCheckpoint();
					if( now is not null && now != previous ) return now;
					Thread.Sleep( 500 );
				}
				Assert.Fail( "a checkpoint within 40 s" );
				return string.Empty;
			}

			var first = WaitForCheckpointOtherThan( null );

			Thread.Sleep( 1500 );
			var since = DateTime.UtcNow;
			Thread.Sleep( 1500 );
			File.AppendAllText( log, "inside the window\n" );
			Thread.Sleep( 1500 );
			var until = DateTime.UtcNow;
			Thread.Sleep( 1500 );

			WaitForCheckpointOtherThan( first ); // the second one: the first record at or after the end of the window
			File.AppendAllText( log, "after the second checkpoint\n" );

			var window = new { since = since.ToString( "o" ), until = until.ToString( "o" ) };

			// the preview with the window: the file written in it, not the one of yesterday - and without it, both
			var query = http.PostAsync( $"{root}/files/query", Body( new { fileDefs = new[] { new { id = "log" } }, window.since, window.until } ) ).GetAwaiter().GetResult();
			var queryText = query.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.OK, query.StatusCode, queryText );
			List<string> Previewed( string text )
				=> Json( text ).GetProperty( "items" )[0].GetProperty( "fileDefs" )[0].GetProperty( "files" ).EnumerateArray()
					.Select( f => Path.GetFileName( f.GetProperty( "localPath" ).GetString()! ) ).OrderBy( x => x ).ToList();
			CollectionAssert.AreEqual( new[] { "run.log" }, Previewed( queryText ), queryText );

			var queryAll = http.PostAsync( $"{root}/files/query", Body( new { fileDefs = new[] { new { id = "log" } } } ) ).GetAwaiter().GetResult();
			CollectionAssert.AreEqual( new[] { "run.log", "yesterday.log" }, Previewed( queryAll.Content.ReadAsStringAsync().GetAwaiter().GetResult() ) );

			// the collection
			var post = http.PostAsync( $"{root}/downloads", Body( new { fileDef = new { id = "log" }, window.since, window.until } ) ).GetAwaiter().GetResult();
			var postText = post.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.Accepted, post.StatusCode, postText );
			var id = Json( postText ).GetProperty( "id" ).GetString();
			var job = Json( http.GetStringAsync( $"{root}/downloads/{id}?wait=60" ).GetAwaiter().GetResult() );
			Assert.AreEqual( "Finished", job.GetProperty( "status" ).GetString(), job.ToString() );

			var bytes = http.GetByteArrayAsync( $"http://127.0.0.1:{world.HttpPort}{job.GetProperty( "archives" )[0].GetProperty( "url" ).GetString()}" ).GetAwaiter().GetResult();
			using var zip = new System.IO.Compression.ZipArchive( new MemoryStream( bytes ) );

			var files = zip.Entries.Where( e => !e.Name.StartsWith( "_" ) ).Select( e => e.Name ).OrderBy( x => x ).ToList();
			CollectionAssert.AreEqual( new[] { "run.window.log" }, files, "the same files as the preview, and the cut one is named for what cut it: " + string.Join( ", ", zip.Entries.Select( e => e.FullName ) ) );

			using var reader = new StreamReader( zip.Entries.Single( e => e.Name == "run.window.log" ).Open() );
			var text = reader.ReadToEnd();
			StringAssert.Contains( text, "inside the window", text );
			Assert.IsFalse( text.Contains( "before the run" ), $"nothing from before the plan's mark: {text}" );
			Assert.IsFalse( text.Contains( "before the checkpoint" ), $"nothing from before the first checkpoint: {text}" );
			Assert.IsFalse( text.Contains( "after the second checkpoint" ), $"nothing from after the checkpoint the window ends at: {text}" );

			using var comment = new StreamReader( zip.GetEntry( "_comment.txt" )!.Open() );
			StringAssert.Contains( comment.ReadToEnd(), "Window    :", "the cover note says the window asked for" );
		}

		[TestMethod()]
		public void AWindowThatEndsBeforeItStartsIsABadRequest()
		{
			using var world = SystemWorld.Start( "CheckpointWorld", withHttp: true );

			using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds( 60 ) };
			var root = $"http://127.0.0.1:{world.HttpPort}/api";
			System.Net.Http.StringContent Body( string s ) => new( s, Encoding.UTF8, "application/json" );

			foreach( var (route, selection) in new[] { ( "downloads", "{\"fileDef\":{\"id\":\"log\"}" ), ( "files/query", "{\"fileDefs\":[{\"id\":\"log\"}]" ) } )
			{
				var r = http.PostAsync( $"{root}/{route}", Body( selection + ",\"since\":\"2026-11-21T15:00:00Z\",\"until\":\"2026-11-21T14:00:00Z\"}" ) ).GetAwaiter().GetResult();
				var text = r.Content.ReadAsStringAsync().GetAwaiter().GetResult();
				Assert.AreEqual( System.Net.HttpStatusCode.BadRequest, r.StatusCode, $"{route}: {text}" );
				Assert.AreEqual( "application/json", r.Content.Headers.ContentType?.MediaType, route );
				StringAssert.Contains( System.Text.Json.JsonDocument.Parse( text ).RootElement.GetProperty( "error" ).GetString(), "until", text );

				// either end alone, or both in order, is accepted
				var ok = http.PostAsync( $"{root}/{route}", Body( selection + ",\"until\":\"2026-11-21T14:00:00Z\"}" ) ).GetAwaiter().GetResult();
				Assert.IsTrue( (int) ok.StatusCode < 300, $"{route}: {(int) ok.StatusCode} {ok.Content.ReadAsStringAsync().GetAwaiter().GetResult()}" );
			}
		}
	}
}
