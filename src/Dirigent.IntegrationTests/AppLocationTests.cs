using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using Dirigent.Web;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// Where an application's binary is, as the agent of its machine resolves it - with the code the
	/// launcher starts the process from. Driven through the CLI twins of the REST calls.
	/// </summary>
	[TestClass()]
	public class AppLocationTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 30 );

		[TestInitialize()]
		public void SetUp()
		{
			Diagnostics.ClearLog();
			Environment.SetEnvironmentVariable( "DIRIGENT_LOCTEST", "v1" );
		}

		static string SystemDriveRoot => Path.GetPathRoot( Path.GetTempPath() )!;

		static Scenario World()
			=> Scenario.TwoMachines()
				.Share( "m1", "C", SystemDriveRoot )
				.App( "m1.rel", a => a.Exe( @"bin\%DIRIGENT_LOCTEST%\tool.exe" ).StartupDir( @"work\%APP_ID%" ).Args( "--id %APP_ID% --on %MACHINE_ID%" ) )
				.App( "m1.onpath", a => a.Exe( "cmd.exe" ).Args( "/c exit" ) )
				.App( "m1.nowhere", a => a.Exe( "definitely-not-here-4711.exe" ) )
				.App( "m1.dc", a => a.Exe( "[dirigent.command]" ).Args( "GetAllAppsState" ) )
				.App( "m1.shell", a => a.Exe( "[cmd.command]" ).Args( "echo %APP_ID%" ) )
				.App( "m2.other", a => a.Exe( "{testapp}" ).Args( "--run-forever" ) )
				.RawXml( "<App AppIdTuple='ghost.x' ExeFullPath='c:\\x.exe'/>" )
				.RawXml(
					"<Plan Name='planX'><App AppIdTuple='{m1}.rel' ExeFullPath='D:\\plan\\other.exe' StartupDir='D:\\plan'/></Plan>" );

		static async Task<TestBed.TestBed> StartBed()
		{
			var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World() } );

			Directory.CreateDirectory( Path.Combine( bed.TempRoot, "bin", "v1" ) );
			File.WriteAllText( Path.Combine( bed.TempRoot, "bin", "v1", "tool.exe" ), "not really an exe" );
			Directory.CreateDirectory( Path.Combine( bed.TempRoot, "work", "rel" ) );

			return bed;
		}

		static async Task<AppLocationEntry> One( TestBed.TestBed bed, string id )
		{
			var lines = await bed.SendCliCommandAsync( $"GetAppLocation {id}", Timeout );
			Assert.AreEqual( 2, lines.Count, string.Join( " | ", lines ) );
			Assert.AreEqual( "END", lines[1] );

			var prefix = $"APPLOCATION:{id}:";
			StringAssert.StartsWith( lines[0], prefix, lines[0] );
			return JsonSerializer.Deserialize<AppLocationEntry>( lines[0].Substring( prefix.Length ) )!;
		}

		[TestMethod()]
		public async Task ARelativeExeWithAVariableIsExpandedAndRootedLikeTheLauncherDoesIt()
		{
			using var bed = await StartBed();

			var loc = await One( bed, "m1.rel" );

			Assert.IsNull( loc.error, loc.error );
			Assert.AreEqual( "m1.rel", loc.app );
			Assert.AreEqual( "m1", loc.machine );
			Assert.AreEqual( "process", loc.kind );
			Assert.AreEqual( "none", loc.planSource );

			Assert.AreEqual( @"bin\%DIRIGENT_LOCTEST%\tool.exe", loc.exe.declared );
			Assert.AreEqual( Path.Combine( bed.TempRoot, "bin", "v1", "tool.exe" ), loc.exe.localPath );
			Assert.IsTrue( loc.exe.exists );
			Assert.AreEqual( 17L, loc.exe.size );
			Assert.IsNotNull( loc.exe.modified );
			Assert.IsNull( loc.exe.foundOnPath );

			Assert.AreEqual( @"work\%APP_ID%", loc.startupDir.declared );
			Assert.AreEqual( Path.Combine( bed.TempRoot, "work", "rel" ), loc.startupDir.localPath );
			Assert.IsTrue( loc.startupDir.exists );

			Assert.AreEqual( "--id %APP_ID% --on %MACHINE_ID%", loc.cmdLineArgs.declared );
			Assert.AreEqual( "--id rel --on m1", loc.cmdLineArgs.expanded );

			// through the share of m1
			Assert.AreEqual( @"\\127.0.0.1\C\" + loc.exe.localPath!.Substring( SystemDriveRoot.Length ), loc.exe.uncPath );
		}

		[TestMethod()]
		public async Task ABareNameIsLookedUpOnThePathAndAMissingOneSaysSo()
		{
			using var bed = await StartBed();

			var found = await One( bed, "m1.onpath" );
			Assert.IsTrue( found.exe.exists );
			Assert.IsTrue( found.exe.localPath!.EndsWith( "cmd.exe", StringComparison.OrdinalIgnoreCase ), found.exe.localPath );
			Assert.AreEqual( true, found.exe.foundOnPath );
			Assert.IsTrue( found.exe.size > 0 );

			var missing = await One( bed, "m1.nowhere" );
			Assert.IsFalse( missing.exe.exists );
			Assert.AreEqual( "definitely-not-here-4711.exe", missing.exe.localPath, "as written" );
			Assert.IsNull( missing.exe.foundOnPath );
			Assert.IsNull( missing.error, "a missing file is a fact about the app, not a failure" );
		}

		[TestMethod()]
		public async Task ReservedExeNamesAreReportedForWhatTheyAre()
		{
			using var bed = await StartBed();

			var dc = await One( bed, "m1.dc" );
			Assert.AreEqual( "dirigentCommand", dc.kind );
			Assert.IsNull( dc.exe.localPath, "a Dirigent command runs no program" );
			Assert.AreEqual( "GetAllAppsState", dc.cmdLineArgs.expanded );

			var shell = await One( bed, "m1.shell" );
			Assert.AreEqual( "cmd.command", shell.kind );
			Assert.AreEqual( Environment.GetEnvironmentVariable( "ComSpec" ), shell.exe.localPath );
			Assert.AreEqual( "/c echo shell", shell.cmdLineArgs.expanded, "with the prefix the launcher adds" );
		}

		[TestMethod()]
		public async Task AnAppOfAnotherMachineIsAnsweredByThatMachinesAgent()
		{
			using var bed = await StartBed();

			var loc = await One( bed, "m2.other" );

			Assert.IsNull( loc.error, loc.error );
			Assert.AreEqual( "m2", loc.machine );
			Assert.AreEqual( bed.RenderContext.TestAppPath, loc.exe.localPath );
			Assert.IsTrue( loc.exe.exists );
			Assert.IsTrue( loc.exe.size > 0 );
			Assert.IsNotNull( loc.exe.fileVersion, "a built executable has a version resource" );
			Assert.IsNull( loc.exe.uncPath, "m2 declares no share" );
			Assert.IsFalse( string.IsNullOrEmpty( loc.exe.uncError ) );
		}

		[TestMethod()]
		public async Task APlanSelectsItsOwnDefinition()
		{
			using var bed = await StartBed();

			var loc = await One( bed, "m1.rel@planX" );

			Assert.AreEqual( "planX", loc.plan );
			Assert.AreEqual( "requested", loc.planSource );
			Assert.AreEqual( @"D:\plan\other.exe", loc.exe.declared );
			Assert.AreEqual( @"D:\plan\other.exe", loc.exe.localPath );
			Assert.AreEqual( @"D:\plan", loc.startupDir.localPath );
		}

		[TestMethod()]
		public async Task AllLocationsAreListedAndAMachineNobodyServesFailsOnlyItsOwnApp()
		{
			using var bed = await StartBed();

			var lines = await bed.SendCliCommandAsync( "GetAppLocations", Timeout );
			Assert.AreEqual( 2, lines.Count, string.Join( " | ", lines ) );
			StringAssert.StartsWith( lines[0], "APPLOCATIONS:" );

			var all = JsonSerializer.Deserialize<AppLocationEntry[]>( lines[0].Substring( "APPLOCATIONS:".Length ) )!;
			CollectionAssert.AreEqual(
				new[] { "ghost.x", "m1.dc", "m1.nowhere", "m1.onpath", "m1.rel", "m1.shell", "m2.other" },
				all.Select( a => a.app ).ToList() );

			var ghost = all.Single( a => a.app == "ghost.x" );
			StringAssert.Contains( ghost.error, "ghost" );
			Assert.AreEqual( @"c:\x.exe", ghost.exe.declared, "what is declared is still said" );

			Assert.IsTrue( all.Where( a => a.app != "ghost.x" ).All( a => a.error is null ) );

			// narrowed to a machine
			var m2 = await bed.SendCliCommandAsync( "GetAppLocations machine=m2", Timeout );
			var only = JsonSerializer.Deserialize<AppLocationEntry[]>( m2[0].Substring( "APPLOCATIONS:".Length ) )!;
			Assert.AreEqual( "m2.other", only.Single().app );
		}

		[TestMethod()]
		public async Task AnUnknownAppIsAnError()
		{
			using var bed = await StartBed();

			foreach( var id in new[] { "m1.nosuch", "m1.rel@nosuchplan" } )
			{
				var lines = await bed.SendCliCommandAsync( $"GetAppLocation {id}", Timeout );
				Assert.AreEqual( 1, lines.Count, string.Join( " | ", lines ) );
				StringAssert.StartsWith( lines[0], "ERROR", id );
			}
		}
	}
}
