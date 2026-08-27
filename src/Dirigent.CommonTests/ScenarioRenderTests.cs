using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.TestBed.Scenarios;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dirigent.Tests
{
	/// <summary>
	/// The scenario renderer is code, and silent drift in it would quietly weaken every test built
	/// on top. The strongest guard is cheap: render, then feed the result through the real
	/// SharedConfigReader and check that what comes out the far end is what the scenario asked for.
	/// </summary>
	[TestClass()]
	public class ScenarioRenderTests
	{
		static readonly RenderContext Ctx = new RenderContext(
			tempRoot: @"C:\temp\testrun",
			testAppPath: @"C:\bin\Dirigent.TestApp.exe" );

		static SharedConfig RoundTrip( Scenario scenario )
		{
			var xml = SharedConfigRenderer.Render( scenario.Spec, Ctx );
			// the real reader, so a rendering the product would reject fails here
			return new SharedConfigReader( new StringReader( xml ) ).Config;
		}

		[TestMethod()]
		public void PresetWorldRoundTripsThroughTheRealReader()
		{
			var cfg = RoundTrip( Scenario.TwoMachinesWithIdlers() );

			CollectionAssert.AreEquivalent(
				new List<string>() { "m1", "m2" },
				cfg.Machines.Select( m => m.Id ).ToList() );

			var apps = cfg.AppDefaults.Select( a => a.Id.ToString() ).ToList();
			CollectionAssert.AreEquivalent( new List<string>() { "m1.idler", "m2.idler" }, apps );

			foreach( var app in cfg.AppDefaults )
			{
				Assert.AreEqual( @"C:\bin\Dirigent.TestApp.exe", app.ExeFullPath );
				StringAssert.Contains( app.CmdLineArgs, "--run-forever" );
				StringAssert.StartsWith( app.StartupDir, @"C:\temp\testrun" );
			}
		}

		[TestMethod()]
		public void ApplicationsAreMinimizedUnlessAskedOtherwise()
		{
			// a test run must not throw windows at whoever is using the machine, so this is the
			// default; only a test about the window style itself should change it
			var cfg = RoundTrip( Scenario.OneMachine()
				.App( "m1.quiet", a => a.LongRunning() )
				.App( "m1.loud", a => a.LongRunning().WindowStyle( WindowStyleSpec.Normal ) ) );

			Assert.AreEqual( EWindowStyle.Minimized, Find( cfg, "m1.quiet" ).WindowStyle );
			Assert.AreEqual( EWindowStyle.Normal, Find( cfg, "m1.loud" ).WindowStyle );
		}

		[TestMethod()]
		public void LogNodesAndPackagesRoundTrip()
		{
			var cfg = RoundTrip( Scenario.LoggingApps() );

			// one log node per application, bound to that application and machine
			var logNodes = cfg.VfsNodes.Where( n => n.Id == "log" ).ToList();
			Assert.AreEqual( 3, logNodes.Count );
			CollectionAssert.AreEquivalent(
				new List<string>() { "m1.camera", "m1.tracker", "m2.recorder" },
				logNodes.Select( n => $"{n.MachineId}.{n.AppId}" ).ToList() );

			foreach( var node in logNodes )
			{
				Assert.AreEqual( "Newest", node.Filter );
				StringAssert.Contains( node.Xml!, "Mask=\"*.log\"" );
				StringAssert.Contains( node.Xml!, "MaxSeconds=\"172800\"" );  // two days
				StringAssert.Contains( node.Path!, "logs" );
			}

			// and a package that collects them all, wherever they are
			var package = cfg.VfsNodes.OfType<FilePackageDef>().Single( p => p.Id == "logs.all" );
			var reference = package.Children.OfType<FileRef>().Single();
			Assert.AreEqual( "log", reference.Id );
			Assert.AreEqual( "*", reference.MachineId );
			Assert.AreEqual( "*", reference.AppId );
		}

		[TestMethod()]
		public void PlansRoundTrip()
		{
			var cfg = RoundTrip( Scenario.TwoMachinesWithIdlers().PlanWithEverything() );

			var plan = cfg.Plans.Single();
			Assert.AreEqual( "all", plan.Name );
			CollectionAssert.AreEquivalent(
				new List<string>() { "m1.idler", "m2.idler" },
				plan.AppDefs.Select( a => a.Id.ToString() ).ToList() );
		}

		[TestMethod()]
		public void SharesAndMachineReferencesUseRealMachineIds()
		{
			var scenario = Scenario.TwoMachines()
				.Share( "m1", "C$", @"C:\" )
				.App( "m1.camera", a => a.LongRunning().WithLogNode() )
				.Package( "logs.m1", "Logs/m1", p => p.RefMachine( "log", "m1" ) );

			var cfg = RoundTrip( scenario );

			var m1 = cfg.Machines.Single( m => m.Id == "m1" );
			Assert.AreEqual( @"C:\", m1.FileShares.Single( s => s.Name == "C$" ).Path );

			// a machine name in a reference has to become the machine's real id, not stay a name
			var package = cfg.VfsNodes.OfType<FilePackageDef>().Single( p => p.Id == "logs.m1" );
			Assert.AreEqual( "m1", package.Children.OfType<FileRef>().Single().MachineId );
		}

		[TestMethod()]
		public void PathsWithBackslashesAndMasksSurviveEscaping()
		{
			// rendering with XElement rather than string concatenation is what makes this hold
			var scenario = Scenario.OneMachine()
				.App( "m1.a", a => a
					.LongRunning()
					.WithFolderNode( "tree", @"{appdir}\sub dir", mask: "**/*.{log,txt}", maxSeconds: 3600 ) );

			var cfg = RoundTrip( scenario );

			var folder = cfg.VfsNodes.OfType<FolderDef>().Single( n => n.Id == "tree" );
			Assert.AreEqual( @"C:\temp\testrun\apps\m1\a\sub dir", folder.Path );
			Assert.AreEqual( "**/*.{log,txt}", folder.Mask );
			Assert.AreEqual( 3600.0, folder.MaxSeconds );
		}

		[TestMethod()]
		public void EnvironmentVariablesAndRawXmlRoundTrip()
		{
			var scenario = Scenario.OneMachine()
				.App( "m1.a", a => a.LongRunning().Env( "MY_VAR", "my value" ) )
				.RawXml( @"<File Id=""global"" Path=""\\server\share\file.txt""/>" );

			var cfg = RoundTrip( scenario );

			var app = Find( cfg, "m1.a" );
			Assert.AreEqual( "my value", app.EnvVarsToSet["MY_VAR"] );

			Assert.IsTrue( cfg.VfsNodes.Any( n => n.Id == "global" ),
				"the raw xml escape hatch should reach the config" );
		}

		[TestMethod()]
		public void SeedingCreatesFilesWithTheRequestedAges()
		{
			var tempRoot = Path.Combine( Path.GetTempPath(), "DirigentSeedTest_" + Guid.NewGuid().ToString( "N" ) );
			try
			{
				var ctx = new RenderContext( tempRoot, @"C:\bin\Dirigent.TestApp.exe" );
				var scenario = Scenario.OneMachine()
					.App( "m1.camera", a => a.LongRunning() )
					.Seed( "m1.camera", "fresh.log", ageDays: 1 )
					.Seed( "m1.camera", "old.log", ageDays: 9 )
					.Seed( "m1", "machine.txt", ageDays: 0, content: "hello" );

				WorldSeeder.Seed( scenario.Spec, ctx );

				var logs = ctx.AppLogsDir( "m1", "camera" );
				var fresh = Path.Combine( logs, "fresh.log" );
				var old = Path.Combine( logs, "old.log" );

				Assert.IsTrue( File.Exists( fresh ) );
				Assert.IsTrue( File.Exists( old ) );

				// the age has to be on the last write time, which is what Dirigent's filters use
				var freshAge = ( DateTime.UtcNow - File.GetLastWriteTimeUtc( fresh ) ).TotalDays;
				var oldAge = ( DateTime.UtcNow - File.GetLastWriteTimeUtc( old ) ).TotalDays;
				Assert.IsTrue( Math.Abs( freshAge - 1 ) < 0.01, $"expected ~1 day, got {freshAge:0.###}" );
				Assert.IsTrue( Math.Abs( oldAge - 9 ) < 0.01, $"expected ~9 days, got {oldAge:0.###}" );

				Assert.AreEqual( "hello", File.ReadAllText( Path.Combine( ctx.MachineDir( "m1" ), "machine.txt" ) ) );

				// the working folder exists too, so an app can be started in it
				Assert.IsTrue( Directory.Exists( ctx.AppDir( "m1", "camera" ) ) );
			}
			finally
			{
				try { Directory.Delete( tempRoot, true ); } catch {}
			}
		}

		static AppDef Find( SharedConfig cfg, string idTuple )
			=> cfg.AppDefaults.Single( a => a.Id.ToString() == idTuple );
	}
}
