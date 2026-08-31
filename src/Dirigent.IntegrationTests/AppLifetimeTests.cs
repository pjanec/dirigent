using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// What Dirigent does with an application over its life: keeping it up, taking it and its
	/// children down, asking it politely first, and handing it its environment.
	/// </summary>
	[TestClass()]
	public class AppLifetimeTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 30 );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		[TestMethod()]
		public async Task ApplicationIsLaunchedAgainAfterItDies()
		{
			// RestartOnCrash is the promise that an application declared to be up stays up
			var scenario = Scenario.OneMachine()
				.App( "m1.flaky", a => a.ExitsAfter( 1.0, exitCode: 1 ).RestartOnCrash() );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );
			var app = bed.App( "m1", "flaky" );

			await bed.Operator.StartAppAsync( app );

			int firstPid = 0;
			await bed.WaitUntilAsync(
				async () =>
				{
					var state = await bed.Operator.GetAppStateAsync( app );
					if( !( state?.Running ?? false ) ) return false;
					firstPid = state!.PID;
					return firstPid > 0;
				},
				Timeout, "the application runs a first time" );

			// it exits by itself after a second; a different pid means it was launched again
			await bed.WaitUntilAsync(
				async () =>
				{
					var state = await bed.Operator.GetAppStateAsync( app );
					return ( state?.Running ?? false ) && state!.PID != firstPid;
				},
				Timeout, $"the application is running again under a new pid (was {firstPid})" );

			// and killing it on purpose is not a crash - it must stay down
			await bed.Operator.KillAppAsync( app );

			await bed.WaitUntilAsync(
				async () => !( ( await bed.Operator.GetAppStateAsync( app ) )?.Running ?? true ),
				Timeout, "the application stops when killed" );

			var afterKill = await bed.Operator.GetAppStateAsync( app );
			Assert.IsTrue( afterKill!.Killed, "a killed application should be marked killed" );
		}

		[TestMethod()]
		public async Task ApplicationThatDiesStaysDownWithoutRestartOnCrash()
		{
			// the counterpart, so the test above is about the setting and not about Dirigent
			// restarting everything
			var scenario = Scenario.OneMachine()
				.App( "m1.quitter", a => a.ExitsAfter( 0.5, exitCode: 1 ) );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );
			var app = bed.App( "m1", "quitter" );

			await bed.Operator.StartAppAsync( app );

			await bed.WaitUntilAsync(
				async () =>
				{
					var state = await bed.Operator.GetAppStateAsync( app );
					return state is not null && state.Started && !state.Running;
				},
				Timeout, "the application has exited" );

			var exited = await bed.Operator.GetAppStateAsync( app );

			// A negative property needs an observation window rather than a condition to wait for:
			// there is no event for "it was not restarted". Two seconds is several restart cycles of
			// an application that exits after half a second, and the check can only fail if a
			// restart really happens - so it is not a flaky sleep.
			var watch = Stopwatch.StartNew();
			while( watch.Elapsed < TimeSpan.FromSeconds( 2 ) )
			{
				var state = await bed.Operator.GetAppStateAsync( app );
				Assert.IsFalse( state!.Running,
					$"nothing should have restarted it, but it is running again as pid {state.PID}" );
				await Task.Delay( 200 );
			}

			var later = await bed.Operator.GetAppStateAsync( app );
			Assert.AreEqual( exited!.ExitCode, later!.ExitCode, "the exit code should still be reported" );
		}

		[TestMethod()]
		public async Task KillTreeTakesTheChildrenToo()
		{
			// an application that spawns workers leaves orphans behind unless the whole tree is
			// killed - which is what KillTree is for
			var scenario = Scenario.OneMachine()
				.App( "m1.parent", a => a.LongRunning().SpawnsChildren( 2 ).KillTree() );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );
			var app = bed.App( "m1", "parent" );

			await bed.Operator.StartAppAsync( app );

			await bed.WaitUntilAsync(
				async () => ( ( await bed.Operator.GetAppStateAsync( app ) )?.PID ?? 0 ) > 0,
				Timeout, "the parent runs" );

			var appDir = bed.RenderContext.AppDir( "m1", "parent" );
			await bed.WaitUntilAsync(
				async () => await Task.FromResult( ReportedChildren( appDir ).Count >= 2 ),
				Timeout, "the parent has spawned its children and said so" );

			var children = ReportedChildren( appDir );
			Assert.IsTrue( children.All( IsAlive ), "the children should be running to begin with" );

			await bed.Operator.KillAppAsync( app );

			await bed.WaitUntilAsync(
				async () => await Task.FromResult( children.All( pid => !IsAlive( pid ) ) ),
				Timeout, $"every child is gone (pids {string.Join( ", ", children )})" );
		}

		[TestMethod()]
		public async Task SoftKillAsksBeforeItInsists()
		{
			// An application that refuses to close is still gone when the kill returns: the close
			// request is given the configured second, then the process is killed anyway. A console
			// application has no window to close, so this is the escalation path, which is the one
			// worth knowing works - a stuck application must not block a kill for ever.
			var scenario = Scenario.OneMachine()
				.App( "m1.stubborn", a => a.LongRunning().IgnoresClose().ClosePolitelyFirst( 1.0 ) );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );
			var app = bed.App( "m1", "stubborn" );

			await bed.Operator.StartAppAsync( app );
			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetAppStateAsync( app ) )?.Running ?? false,
				Timeout, "the stubborn application runs" );

			var pid = ( await bed.Operator.GetAppStateAsync( app ) )!.PID;
			var watch = Stopwatch.StartNew();

			await bed.Operator.KillAppAsync( app );

			await bed.WaitUntilAsync(
				async () => !( ( await bed.Operator.GetAppStateAsync( app ) )?.Running ?? true ),
				Timeout, "the application is reported down" );

			Assert.IsFalse( IsAlive( pid ), "the process itself should be gone, not just reported down" );
			Assert.IsTrue( watch.Elapsed < TimeSpan.FromSeconds( 20 ),
				$"the escalation should be bounded by the timeout, took {watch.Elapsed.TotalSeconds:0.0}s" );
		}

		[TestMethod()]
		public async Task EnvironmentVariablesFromTheConfigReachTheApplication()
		{
			var scenario = Scenario.OneMachine()
				.App( "m1.reporter", a => a
					.LongRunning()
					.Env( "DIRIGENT_TEST_ROLE", "camera" )
					.Env( "DIRIGENT_TEST_INDEX", "7" )
					.PrintsEnvironment() );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );
			var app = bed.App( "m1", "reporter" );

			await bed.Operator.StartAppAsync( app );

			var envFile = Path.Combine( bed.RenderContext.AppDir( "m1", "reporter" ), "env.txt" );
			await bed.WaitUntilAsync(
				async () => await Task.FromResult( File.Exists( envFile ) ),
				Timeout, "the application wrote its environment out" );

			var environment = File.ReadAllLines( envFile );
			AssertHasVariable( environment, "DIRIGENT_TEST_ROLE", "camera" );
			AssertHasVariable( environment, "DIRIGENT_TEST_INDEX", "7" );
		}

		[TestMethod()]
		public async Task VariablesGivenAtStartReachTheApplicationToo()
		{
			// the operator can override the environment for one launch, which is how an application
			// is started "in a different mode" without touching the config
			var scenario = Scenario.OneMachine()
				.App( "m1.reporter", a => a.LongRunning().Env( "DIRIGENT_TEST_ROLE", "fromconfig" ).PrintsEnvironment() );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );
			var app = bed.App( "m1", "reporter" );

			await bed.Operator.StartAppAsync( app, vars: new Dictionary<string, string>()
			{
				{ "DIRIGENT_TEST_ROLE", "fromoperator" },
				{ "DIRIGENT_TEST_EXTRA", "yes" },
			} );

			var envFile = Path.Combine( bed.RenderContext.AppDir( "m1", "reporter" ), "env.txt" );
			await bed.WaitUntilAsync(
				async () => await Task.FromResult( File.Exists( envFile ) ),
				Timeout, "the application wrote its environment out" );

			var environment = File.ReadAllLines( envFile );
			AssertHasVariable( environment, "DIRIGENT_TEST_ROLE", "fromoperator" );
			AssertHasVariable( environment, "DIRIGENT_TEST_EXTRA", "yes" );
		}

		// ---- helpers -------------------------------------------------------------------

		static void AssertHasVariable( string[] environment, string name, string value )
		{
			var expected = $"{name}={value}";
			Assert.IsTrue(
				environment.Any( line => line.Trim().Equals( expected, StringComparison.OrdinalIgnoreCase ) ),
				$"expected '{expected}' among the application's variables; "
				+ $"saw: {string.Join( ", ", environment.Where( l => l.Contains( "DIRIGENT_TEST" ) ) )}" );
		}

		/// <summary>
		/// The pids of the children, as the application itself reported them. Asking Windows who its
		/// children are would mean WMI in a test for no good reason.
		/// </summary>
		static List<int> ReportedChildren( string appDir )
		{
			var file = Path.Combine( appDir, "children.txt" );
			if( !File.Exists( file ) ) return new List<int>();

			return File.ReadAllLines( file )
				.Select( line => line.Trim() )
				.Where( line => line.Length > 0 )
				.Select( int.Parse )
				.ToList();
		}

		static bool IsAlive( int pid )
		{
			try
			{
				using var process = Process.GetProcessById( pid );
				return !process.HasExited;
			}
			catch( ArgumentException )
			{
				return false;   // no such process any more
			}
		}
	}
}
