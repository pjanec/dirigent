using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// Tests of the harness itself. If these break, every other integration test becomes
	/// untrustworthy - silently, which is the dangerous part.
	/// </summary>
	[TestClass()]
	public class HarnessTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 20 );

		static Task<TestBed.TestBed> StartBed( bool keepTempRoot = false )
			=> TestBed.TestBed.StartAsync( new TestBedOptions()
			{
				Scenario = Scenario.OneMachine().App( "m1.idler", a => a.LongRunning() ),
				KeepTempRoot = keepTempRoot,
			} );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		[TestMethod()]
		public async Task ApplicationsDoNotOutliveTheTestBed()
		{
			// Dirigent leaves managed apps running when an agent stops, so the test bed has to
			// clean up after itself or a test run slowly fills the machine with idle processes.
			int pid;
			string tempRoot;

			var bed = await StartBed();
			try
			{
				var app = bed.App( "m1", "idler" );
				await bed.Operator.StartAppAsync( app );
				await bed.WaitUntilAsync(
					async () => ( await bed.Operator.GetAppStateAsync( app ) )?.Running ?? false,
					Timeout, $"{app} reports running" );

				pid = ( await bed.Operator.GetAppStateAsync( app ) )!.PID;
				tempRoot = bed.TempRoot;
				Assert.IsTrue( pid > 0 );
				Assert.IsTrue( IsAlive( pid ), "the app should be alive while the bed is up" );
			}
			finally
			{
				bed.Dispose();
			}

			Assert.IsFalse( IsAlive( pid ), $"pid {pid} should have been killed when the test bed was disposed" );
			Assert.IsFalse( Directory.Exists( tempRoot ), "the temporary folder should have been removed" );
		}

		[TestMethod()]
		public async Task TimeoutReportsWhatItWasWaitingForAndWhatTheWorldLookedLike()
		{
			// The whole approach depends on condition waits rather than sleeps, which is only
			// bearable if a timeout explains itself.
			using var bed = await StartBed();

			var error = await Assert.ThrowsExceptionAsync<TimeoutException>( async () =>
				await bed.WaitUntilAsync(
					() => false,
					TimeSpan.FromMilliseconds( 300 ),
					"something that never happens" ) );

			StringAssert.Contains( error.Message, "something that never happens" );
			StringAssert.Contains( error.Message, "app states:" );      // the state dump
			StringAssert.Contains( error.Message, bed.Machine( "m1" ) ); // naming the real machine id
		}

		[TestMethod()]
		public async Task TwoBedsAreIndependentWorlds()
		{
			// they may use the same machine ids, because what keeps them apart is their own
			// ports and folders - not mangled names
			using var bed1 = await StartBed();
			using var bed2 = await StartBed();

			Assert.AreEqual( "m1", bed1.Machine( "m1" ), "machine ids should be used verbatim" );
			Assert.AreEqual( bed1.Machine( "m1" ), bed2.Machine( "m1" ) );

			Assert.AreNotEqual( bed1.MasterPort, bed2.MasterPort );
			Assert.AreNotEqual( bed1.TempRoot, bed2.TempRoot );
			Assert.AreNotEqual( bed1.AgentStatusFolder, bed2.AgentStatusFolder );
			Assert.AreNotEqual( bed1.DownloadFolder, bed2.DownloadFolder );

			var app1 = bed1.App( "m1", "idler" );
			await bed1.Operator.StartAppAsync( app1 );
			await bed1.WaitUntilAsync(
				async () => ( await bed1.Operator.GetAppStateAsync( app1 ) )?.Running ?? false,
				Timeout, "the app in the first bed runs" );

			var statesInBed2 = await bed2.Operator.GetAllAppsStateAsync();
			Assert.IsFalse( statesInBed2.Any( x => x.Value.Running ),
				"the second bed should not see anything running in the first" );
		}

		[TestMethod()]
		public async Task AgentStatusFileStaysOutOfTheMachineGlobalLocation()
		{
			// the default path is %LocalAppData%\Dirigent\agent_status_<machineId>.json, which a
			// test must not touch: a real agent named m1 would have its recovery state corrupted
			var globalPath = AgentStateSaverLoader.GetStatusFilePath( "m1" );
			var globalExistedBefore = File.Exists( globalPath );

			using( var bed = await StartBed() )
			{
				var expected = AgentStateSaverLoader.GetStatusFilePath( "m1", bed.AgentStatusFolder );
				StringAssert.StartsWith( expected, bed.TempRoot );

				var app = bed.App( "m1", "idler" );
				await bed.Operator.StartAppAsync( app );
				await bed.WaitUntilAsync(
					async () => ( await bed.Operator.GetAppStateAsync( app ) )?.Running ?? false,
					Timeout, $"{app} reports running, which is what makes the agent save its status" );

				await bed.WaitUntilAsync(
					() => File.Exists( expected ),
					Timeout, $"the agent writes its status file to {expected}" );
			}

			Assert.AreEqual( globalExistedBefore, File.Exists( globalPath ),
				"the machine-global status file must be left exactly as it was" );
		}

		[TestMethod()]
		public async Task DownloadsVariableResolvesToTheBedsFolder()
		{
			// %DOWNLOADS% is expanded on the machine that owns the node, so this exercises the
			// whole remote resolution path - and proves a download would not litter the real
			// user's Downloads folder
			using var bed = await StartBed();

			var node = new FolderDef()
			{
				Id = "downloads",
				MachineId = bed.Machine( "m1" ),
				Path = "%DOWNLOADS%",
			};

			var resolved = await bed.Operator.ResolveAsync( node, forceUNC: false, includeContent: false );

			Assert.IsNotNull( resolved, "the download folder should resolve" );
			Assert.AreEqual(
				Path.GetFullPath( bed.DownloadFolder ).TrimEnd( Path.DirectorySeparatorChar ),
				Path.GetFullPath( resolved!.Path! ).TrimEnd( Path.DirectorySeparatorChar ),
				"%DOWNLOADS% should point at the bed's folder, not the real user's" );
		}

		[TestMethod()]
		public async Task TempRootIsKeptWhenAsked()
		{
			string tempRoot;
			var bed = await StartBed( keepTempRoot: true );
			try
			{
				tempRoot = bed.TempRoot;
				Assert.IsTrue( File.Exists( bed.SharedConfigPath ), "the generated shared config should be there" );
			}
			finally
			{
				bed.Dispose();
			}

			Assert.IsTrue( Directory.Exists( tempRoot ), "the folder should survive for inspection" );
			Isolation.DeleteTempRoot( tempRoot );
		}

		static bool IsAlive( int pid )
		{
			try
			{
				using var p = Process.GetProcessById( pid );
				return !p.HasExited;
			}
			catch( ArgumentException )
			{
				return false;
			}
		}
	}
}
