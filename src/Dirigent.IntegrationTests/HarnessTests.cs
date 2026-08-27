using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.TestBed;
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

		const string Config = @"<Shared>
	<Machine Name=""{m1}"" IP=""127.0.0.1""/>
	<App AppIdTuple=""{m1}.idler"" ExeFullPath=""{testapp}"" CmdLineArgs=""--run-forever"" StartupDir=""{temp}""/>
</Shared>";

		static Task<TestBed.TestBed> StartBed( bool keepTempRoot = false )
			=> TestBed.TestBed.StartAsync( new TestBedOptions()
			{
				Machines = new() { "m1" },
				SharedConfigXml = Config,
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
		public async Task MachineIdsAreUniquePerRunSoRunsCannotCollide()
		{
			// the agent status file path is derived from the machine id and is machine-global,
			// so two runs sharing an id would corrupt each other's recovery state
			using var bed1 = await StartBed();
			using var bed2 = await StartBed();

			Assert.AreNotEqual( bed1.Machine( "m1" ), bed2.Machine( "m1" ) );
			Assert.AreNotEqual( bed1.MasterPort, bed2.MasterPort );
			Assert.AreNotEqual( bed1.TempRoot, bed2.TempRoot );

			// and they really are two independent worlds
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
