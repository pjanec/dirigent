using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// The two things that happen to a running system without anybody asking: the configuration
	/// changes, and a machine drops off the network and comes back.
	/// </summary>
	[TestClass()]
	public class ReloadAndReconnectTests
	{
		// the master sleeps three seconds inside a reload, so give these room
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 40 );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		[TestMethod()]
		public async Task ReloadPicksUpAnApplicationThatWasNotThereBefore()
		{
			var before = Scenario.OneMachine()
				.App( "m1.first", a => a.LongRunning() );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = before } );

			var appDefs = await bed.Operator.GetAllAppsDefAsync();
			Assert.AreEqual( 1, appDefs.Count, "one application to begin with" );

			var after = Scenario.OneMachine()
				.App( "m1.first", a => a.LongRunning() )
				.App( "m1.second", a => a.LongRunning() );

			await bed.ReloadSharedConfigAsync( after );

			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetAllAppsDefAsync() ).Any( x => x.Key.AppId == "second" ),
				Timeout, "the new application reaches the operator" );

			// and it can be started, so the definition really arrived rather than just its name
			var second = bed.App( "m1", "second" );
			await bed.Operator.StartAppAsync( second );

			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetAppStateAsync( second ) )?.Running ?? false,
				Timeout, "the new application runs" );
		}

		[TestMethod()]
		public async Task ReloadPicksUpAChangedCommandLine()
		{
			// TODO.md records this as broken on the Linux build. It is a plain expectation of what
			// reload means, so it is worth having pinned either way: the application must be started
			// with the arguments the config now says, not the ones it said at startup.
			var before = Scenario.OneMachine()
				.App( "m1.worker", a => a.LongRunning().WritesLog( "before.log" ) );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = before } );

			var after = Scenario.OneMachine()
				.App( "m1.worker", a => a.LongRunning().WritesLog( "after.log" ) );

			await bed.ReloadSharedConfigAsync( after );

			await bed.WaitUntilAsync(
				async () =>
				{
					var def = ( await bed.Operator.GetAllAppsDefAsync() )
								.FirstOrDefault( x => x.Key.AppId == "worker" ).Value;
					return def is not null && def.CmdLineArgs.Contains( "after.log" );
				},
				Timeout, "the changed command line reaches the operator" );

			var worker = bed.App( "m1", "worker" );
			await bed.Operator.StartAppAsync( worker );

			var logs = bed.RenderContext.AppLogsDir( "m1", "worker" );
			await bed.WaitUntilAsync(
				async () => await Task.FromResult( File.Exists( Path.Combine( logs, "after.log" ) ) ),
				Timeout, "the application was launched with the new command line" );

			Assert.IsFalse( File.Exists( Path.Combine( logs, "before.log" ) ),
				"the old command line should not have been used" );
		}

		[TestMethod()]
		public async Task ReloadThatKillsApplicationsTakesThemDown()
		{
			var scenario = Scenario.OneMachine().App( "m1.idler", a => a.LongRunning() );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );
			var app = bed.App( "m1", "idler" );

			await bed.Operator.StartAppAsync( app );
			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetAppStateAsync( app ) )?.Running ?? false,
				Timeout, "the application runs before the reload" );

			await bed.ReloadSharedConfigAsync( scenario, killApps: true );

			await bed.WaitUntilAsync(
				async () => !( ( await bed.Operator.GetAppStateAsync( app ) )?.Running ?? true ),
				Timeout, "the application was taken down by the reload" );
		}

		[TestMethod()]
		public async Task AgentThatDropsOffComesBackAndWorksAgain()
		{
			var scenario = Scenario.TwoMachinesWithIdlers();

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );

			var onM2 = bed.App( "m2", "idler" );

			bed.StopAgent( "m2" );

			await bed.WaitUntilAsync(
				async () => !await IsConnected( bed, "m2" ),
				Timeout, "the master notices m2 is gone" );

			// the other machine carries on regardless
			var onM1 = bed.App( "m1", "idler" );
			await bed.Operator.StartAppAsync( onM1 );
			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetAppStateAsync( onM1 ) )?.Running ?? false,
				Timeout, "m1 still works while m2 is away" );

			bed.StartAgent( "m2" );

			await bed.WaitUntilAsync(
				async () => await IsConnected( bed, "m2" ),
				Timeout, "m2 is connected again" );

			// and it takes commands again, which is the point of reconnecting
			await bed.Operator.StartAppAsync( onM2 );
			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetAppStateAsync( onM2 ) )?.Running ?? false,
				Timeout, "the application on the returned machine runs" );
		}

		[TestMethod()]
		public async Task ApplicationsOutliveTheirAgentAndAreAdoptedBackWithTheSamePid()
		{
			// Dirigent deliberately leaves managed applications running when its agent goes away, and
			// picks them up again from the status file rather than starting a second copy.
			var scenario = Scenario.OneMachine()
				.App( "m1.idler", a => a.LongRunning().AdoptIfAlreadyRunning() );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );
			var app = bed.App( "m1", "idler" );

			await bed.Operator.StartAppAsync( app );
			await bed.WaitUntilAsync(
				async () => ( ( await bed.Operator.GetAppStateAsync( app ) )?.PID ?? 0 ) > 0,
				Timeout, "the application runs" );

			var pid = ( await bed.Operator.GetAppStateAsync( app ) )!.PID;

			// crash rather than a graceful stop: a graceful exit removes the status file by design,
			// and the status file is the whole mechanism of post-crash recovery
			bed.StopAgent( "m1", crash: true );
			await bed.WaitUntilAsync(
				async () => !await IsConnected( bed, "m1" ),
				Timeout, "the agent is gone" );

			Assert.IsTrue( IsAlive( pid ), "the application should still be running without its agent" );

			bed.StartAgent( "m1" );
			await bed.WaitUntilAsync(
				async () => await IsConnected( bed, "m1" ),
				Timeout, "the agent is back" );

			await bed.WaitUntilAsync(
				async () => ( ( await bed.Operator.GetAppStateAsync( app ) )?.PID ?? 0 ) > 0,
				Timeout, "the agent reports the application again" );

			var adopted = await bed.Operator.GetAppStateAsync( app );
			Assert.AreEqual( pid, adopted!.PID,
				"the same process should have been adopted, not a second one started" );
			Assert.IsTrue( adopted.Running, "and it should be reported as running" );
		}

		// ---- helpers -------------------------------------------------------------------

		static async Task<bool> IsConnected( TestBed.TestBed bed, string machineName )
		{
			var machineId = bed.Machine( machineName );
			var clients = await bed.Operator.GetAllClientsStateAsync();

			return clients.Any( x => x.Key == machineId && x.Value.Connected );
		}

		static bool IsAlive( int pid )
		{
			try
			{
				using var process = System.Diagnostics.Process.GetProcessById( pid );
				return !process.HasExited;
			}
			catch( ArgumentException )
			{
				return false;
			}
		}
	}
}
