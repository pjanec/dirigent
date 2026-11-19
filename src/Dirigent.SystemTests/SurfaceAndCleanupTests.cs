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

			var app = http.GetAsync( $"{root}/appfiles/m1.camera" ).GetAwaiter().GetResult();
			var appText = app.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.OK, app.StatusCode, appText );
			StringAssert.Contains( appText, "\"nodes\"", appText );
			StringAssert.Contains( appText, "recent.log", $"the seeded log is listed: {appText}" );

			var node = http.GetAsync( $"{root}/nodefiles/log?machine=m1&app=camera" ).GetAwaiter().GetResult();
			var nodeText = node.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.OK, node.StatusCode, nodeText );
			StringAssert.Contains( nodeText, "recent.log", nodeText );

			var missing = http.GetAsync( $"{root}/appfiles/m1.nosuch" ).GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.NotFound, missing.StatusCode );
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
