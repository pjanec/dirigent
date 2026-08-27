using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// Plans: what Dirigent is for. Applications started in dependency order across machines, kept
	/// running or forgotten depending on how they are declared, and a plan status that says whether
	/// the whole thing is up.
	/// </summary>
	[TestClass()]
	public class PlanTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 30 );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		[TestMethod()]
		public async Task PlanStartsEveryApplicationAndReportsSuccess()
		{
			var scenario = Scenario.TwoMachinesWithIdlers().PlanWithEverything();

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );

			await bed.Operator.StartPlanAsync( "all" );

			await bed.WaitUntilAsync(
				async () => await AllRunning( bed, bed.App( "m1", "idler" ), bed.App( "m2", "idler" ) ),
				Timeout, "both idlers of the plan run" );

			// a plan whose applications are all up and initialized reports success
			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetPlanStateAsync( "all" ) )?.OpStatus
							== PlanState.EOpStatus.Success,
				Timeout, "the plan reports Success" );
		}

		[TestMethod()]
		public async Task KillingThePlanTakesEveryApplicationDown()
		{
			var scenario = Scenario.TwoMachinesWithIdlers().PlanWithEverything();

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );

			await bed.Operator.StartPlanAsync( "all" );
			await bed.WaitUntilAsync(
				async () => await AllRunning( bed, bed.App( "m1", "idler" ), bed.App( "m2", "idler" ) ),
				Timeout, "both idlers run before we kill the plan" );

			await bed.Operator.KillPlanAsync( "all" );

			await bed.WaitUntilAsync(
				async () => await NoneRunning( bed, bed.App( "m1", "idler" ), bed.App( "m2", "idler" ) ),
				Timeout, "no application of the plan is left running" );

			// and the plan itself is back to controlling nothing
			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetPlanStateAsync( "all" ) )?.OpStatus
							== PlanState.EOpStatus.None,
				Timeout, "the plan reports None once everything is down" );
		}

		[TestMethod()]
		public async Task DependentApplicationWaitsUntilItsDependencyIsInitialized()
		{
			// the first application only counts as initialized two seconds after launch, and the
			// second waits for it - so the second must not be running while the first is not yet
			// initialized. Two seconds is long enough to observe and short enough not to drag.
			var scenario = Scenario.OneMachine()
				.App( "m1.first", a => a.LongRunning() )
				.App( "m1.second", a => a.LongRunning() )
				.Plan( "ordered", p => p
					.App( "m1.first", a => a.InitializedAfter( 2.0 ) )
					.App( "m1.second", a => a.DependsOn( "first" ) ) );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );

			var first = bed.App( "m1", "first" );
			var second = bed.App( "m1", "second" );

			await bed.Operator.StartPlanAsync( "ordered" );

			// while the dependency is not initialized, the dependent one must not have been started
			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetAppStateAsync( first ) )?.Running ?? false,
				Timeout, "the first application runs" );

			var firstState = await bed.Operator.GetAppStateAsync( first );
			Assert.IsFalse( firstState!.Initialized,
				"the first application should not count as initialized for two seconds" );

			var secondState = await bed.Operator.GetAppStateAsync( second );
			Assert.IsFalse( secondState?.Started ?? false,
				"the second application must wait for its dependency to initialize" );

			// and once it is initialized, the dependent one follows
			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetAppStateAsync( first ) )?.Initialized ?? false,
				Timeout, "the first application reports initialized" );

			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetAppStateAsync( second ) )?.Running ?? false,
				Timeout, "the second application runs once its dependency is initialized" );
		}

		[TestMethod()]
		public async Task DependencyIsWaitedForAcrossMachines()
		{
			// the same, with the dependency on the other machine: the wait is on state arriving over
			// the network, which is the part a single-machine test cannot show
			var scenario = Scenario.TwoMachines()
				.App( "m1.leader", a => a.LongRunning() )
				.App( "m2.follower", a => a.LongRunning().WritesLog() )
				.Plan( "ordered", p => p
					.App( "m1.leader", a => a.InitializedAfter( 2.0 ) )
					.App( "m2.follower", a => a.DependsOn( "m1.leader" ) ) );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );

			var leader = bed.App( "m1", "leader" );
			var follower = bed.App( "m2", "follower" );

			await bed.Operator.StartPlanAsync( "ordered" );

			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetAppStateAsync( leader ) )?.Running ?? false,
				Timeout, "the leader runs" );

			Assert.IsFalse( ( await bed.Operator.GetAppStateAsync( follower ) )?.Started ?? false,
				"the follower on the other machine must wait too" );

			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetAppStateAsync( follower ) )?.Running ?? false,
				Timeout, "the follower runs once the leader is initialized" );

			// its log exists, so it really ran rather than merely being reported as running
			await bed.WaitUntilAsync(
				async () => File.Exists( Path.Combine( bed.RenderContext.AppLogsDir( "m2", "follower" ), "app.log" ) ),
				Timeout, "the follower wrote its log" );
		}

		[TestMethod()]
		public async Task VolatileApplicationThatExitsDoesNotFailThePlan()
		{
			// the utility-command case: a one-shot application that exits is not a failure, as long
			// as it is declared volatile and its exit code is what the init condition expects
			var scenario = Scenario.OneMachine()
				.App( "m1.service", a => a.LongRunning() )
				.App( "m1.oneshot", a => a.ExitsAfter( 0.5, exitCode: 0 ) )
				.Plan( "mixed", p => p
					.App( "m1.service" )
					.App( "m1.oneshot", a => a.Volatile().InitializedOnExitCode( 0 ) ) );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );

			await bed.Operator.StartPlanAsync( "mixed" );

			await bed.WaitUntilAsync(
				async () =>
				{
					var oneshot = await bed.Operator.GetAppStateAsync( bed.App( "m1", "oneshot" ) );
					return oneshot is not null && oneshot.Started && !oneshot.Running;
				},
				Timeout, "the one-shot application has come and gone" );

			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetPlanStateAsync( "mixed" ) )?.OpStatus
							== PlanState.EOpStatus.Success,
				Timeout, "the plan is a success despite the volatile application having exited" );

			Assert.IsTrue( ( await bed.Operator.GetAppStateAsync( bed.App( "m1", "service" ) ) )!.Running,
				"the non-volatile application is still being kept up" );
		}

		[TestMethod()]
		public async Task ApplicationThatDiesUnexpectedlyFailsThePlan()
		{
			// the other side of the same coin: a plan is a promise that its applications are up, so
			// one of them exiting is a failure to report, not something to shrug at
			var scenario = Scenario.OneMachine()
				.App( "m1.quitter", a => a.ExitsAfter( 1.0, exitCode: 7 ) )
				.Plan( "fragile", "m1.quitter" );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );

			await bed.Operator.StartPlanAsync( "fragile" );

			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetPlanStateAsync( "fragile" ) )?.OpStatus
							== PlanState.EOpStatus.Failure,
				Timeout, "the plan reports Failure after its application exited" );

			var state = await bed.Operator.GetAppStateAsync( bed.App( "m1", "quitter" ) );
			Assert.AreEqual( 7, state!.ExitCode, "the exit code is still visible on the application" );
		}

		[TestMethod()]
		public async Task StartingAnApplicationOfAPlanTakesThePlansDefinition()
		{
			// an application can be defined differently inside a plan than standalone, and naming the
			// plan on StartApp is what chooses between them. Here the two write different log files.
			var scenario = Scenario.OneMachine()
				.App( "m1.worker", a => a.LongRunning().WritesLog( "standalone.log" ) )
				.Plan( "production", p => p
					.App( "m1.worker", a => a.Attribute( "CmdLineArgs",
						@"--run-forever --write-log ""{applogs}\production.log"" --every 0.2" ) ) );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );

			var worker = bed.App( "m1", "worker" );
			var logs = bed.RenderContext.AppLogsDir( "m1", "worker" );

			await bed.Operator.StartAppAsync( worker, planName: "production" );

			await bed.WaitUntilAsync(
				async () => File.Exists( Path.Combine( logs, "production.log" ) ),
				Timeout, "the worker ran with the plan's command line" );

			Assert.IsFalse( File.Exists( Path.Combine( logs, "standalone.log" ) ),
				"the standalone definition should not have been used" );

			// and the other way round: no plan named means the standalone definition
			await bed.Operator.KillAppAsync( worker );
			await bed.WaitUntilAsync(
				async () => !( ( await bed.Operator.GetAppStateAsync( worker ) )?.Running ?? true ),
				Timeout, "the worker stops before we start it again" );

			await bed.Operator.StartAppAsync( worker, planName: "" );

			await bed.WaitUntilAsync(
				async () => File.Exists( Path.Combine( logs, "standalone.log" ) ),
				Timeout, "the worker ran with its standalone command line" );
		}
		// ---- helpers -------------------------------------------------------------------

		static async Task<bool> AllRunning( TestBed.TestBed bed, params AppIdTuple[] apps )
		{
			foreach( var app in apps )
			{
				if( !( ( await bed.Operator.GetAppStateAsync( app ) )?.Running ?? false ) ) return false;
			}
			return true;
		}

		static async Task<bool> NoneRunning( TestBed.TestBed bed, params AppIdTuple[] apps )
		{
			foreach( var app in apps )
			{
				var state = await bed.Operator.GetAppStateAsync( app );
				if( state is null || state.Running ) return false;
			}
			return true;
		}
	}
}
