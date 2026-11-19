using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using Dirigent.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// A plan that marks (or clears) files when it starts: &lt;Plan MarkFiles="run.pkg" MarkTimeout="..."&gt;.
	/// The marking is made slow on purpose (the slaves wait before they work), so that what happens while it is
	/// under way can be looked at - nothing launches, the plan is InProgress, a Stop or a Kill All does not cut it.
	/// </summary>
	[TestClass()]
	public class PlanMarkingTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 40 );
		const string DelayVar = "DIRIGENT_TEST_MARK_DELAY_MS";

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		[TestCleanup()]
		public void TearDown() => Environment.SetEnvironmentVariable( DelayVar, null );

		static void SlowMarking( int ms ) => Environment.SetEnvironmentVariable( DelayVar, ms.ToString() );

		/// <summary>The markable world plus plan "p" with both its apps, and the given plan attributes.</summary>
		static Scenario World( params (string Name, string Value)[] attrs )
			=> Worlds.MarkableWorld()
				.App( "m1.stubborn", a => a.LongRunning().IgnoresClose().ClosePolitelyFirst( 5.0 ) )
				.Plan( "p", p =>
				{
					p.App( "m1.camera" ).App( "m2.recorder" );
					foreach( var (n, v) in attrs ) p.Attribute( n, v );
				} );

		static Task<TestBed.TestBed> StartBed( Scenario s )
			=> TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = s } );

		static async Task<bool> AnyRunning( TestBed.TestBed bed, params string[] apps )
		{
			foreach( var a in apps )
			{
				var parts = a.Split( '.' );
				if( ( await bed.Operator.GetAppStateAsync( bed.App( parts[0], parts[1] ) ) )?.Running ?? false ) return true;
			}
			return false;
		}

		static async Task<bool> AllRunning( TestBed.TestBed bed, params string[] apps )
		{
			foreach( var a in apps )
			{
				var parts = a.Split( '.' );
				if( !( ( await bed.Operator.GetAppStateAsync( bed.App( parts[0], parts[1] ) ) )?.Running ?? false ) ) return false;
			}
			return true;
		}

		static async Task<PlanState> Plan( TestBed.TestBed bed ) => ( await bed.Operator.GetPlanStateAsync( "p" ) )!;

		static async Task<bool> IsMarked( TestBed.TestBed bed )
		{
			var lines = await bed.SendCliCommandAsync( "GetFiles run.pkg", Timeout );
			var res = JsonSerializer.Deserialize<FileDefFilesResult>( lines[0].Substring( "FILES:run.pkg:".Length ) )!;
			return res.fileDefs.Single().files.Any( f => f.markedAt is not null );
		}

		/// <summary>Starts the plan and waits until its state says it is marking (the start is a message to the master).</summary>
		static async Task StartAndSeeMarking( TestBed.TestBed bed )
		{
			await bed.Operator.StartPlanAsync( "p" );
			await bed.WaitUntilAsync( async () => ( await Plan( bed ) ).Phase.StartsWith( "Marking files" ), Timeout, "the plan is marking" );
		}

		/// <summary>Runs on the master's thread in one go: no tick, so no end of a Kill All, can come in between.</summary>
		static Task OnMaster( TestBed.TestBed bed, Action act ) => bed.Master.AddSynchronousOp( act ).WaitAsync();

		static async Task WaitForResult( TestBed.TestBed bed, string what )
			=> await bed.WaitUntilAsync( async () =>
			{
				var ps = await Plan( bed );
				return ps.Phase.Length == 0 && ps.MarkResult is not null;
			}, Timeout, what );

		[TestMethod()]
		public async Task FilesAreMarkedBeforeTheFirstAppStartsAndThePlanIsInProgressMeanwhile()
		{
			SlowMarking( 2000 );
			using var bed = await StartBed( World( ( "MarkFiles", "run.pkg" ) ) );

			await StartAndSeeMarking( bed );

			var first = await Plan( bed );
			StringAssert.StartsWith( first.Phase, "Marking files (run.pkg)" );
			Assert.IsNotNull( first.MarkStarted );

			// as long as the phase is set nothing is launched and the plan is in progress; the apps are looked at
			// BEFORE the plan, so that a phase seen afterwards proves it was set when the apps were looked at
			var seen = 0;
			while( true )
			{
				var running = await AnyRunning( bed, "m1.camera", "m2.recorder" );
				var ps = await Plan( bed );
				if( ps.Phase.Length == 0 ) break;
				seen++;
				Assert.IsFalse( running, "an app was launched while the files were still being marked" );
				Assert.AreEqual( PlanState.EOpStatus.InProgress, ps.OpStatus );
				Assert.IsTrue( ps.Running );
				await Task.Delay( 50 );
			}
			Assert.IsTrue( seen > 3, "the slow marking was observed" );

			await bed.WaitUntilAsync( async () => ( await Plan( bed ) ).OpStatus == PlanState.EOpStatus.Success, Timeout, "the plan reports Success" );

			var result = ( await Plan( bed ) ).MarkResult!;
			Assert.AreEqual( 2, result.Marked, "the camera's and the recorder's log" );
			Assert.IsFalse( result.TimedOut );
			Assert.IsFalse( result.Cancelled );
			Assert.AreEqual( 0, result.Errors.Count, string.Join( "|", result.Errors ) );
			Assert.IsTrue( await IsMarked( bed ), "the lines are drawn" );
		}

		[TestMethod()]
		public async Task TheStartTimeoutOfThePlanCountsFromTheLaunchNotFromTheStart()
		{
			// marking takes longer than the whole StartTimeout, yet the plan does not fail
			SlowMarking( 3000 );
			using var bed = await StartBed( World( ( "MarkFiles", "run.pkg" ), ( "StartTimeout", "2.5" ) ) );

			await bed.Operator.StartPlanAsync( "p" );

			await bed.WaitUntilAsync( async () =>
			{
				var st = ( await Plan( bed ) ).OpStatus;
				Assert.AreNotEqual( PlanState.EOpStatus.Failure, st, "the time spent marking must not count" );
				return st == PlanState.EOpStatus.Success;
			}, Timeout, "the plan reports Success" );
		}

		[TestMethod()]
		public async Task AMarkingThatFailsDoesNotFailThePlan()
		{
			// the only file of the package is on a machine nobody runs
			var s = World( ( "MarkFiles", "ghostpkg" ) )
				.RawXml( "<FilePackage Id='ghostpkg' Title='Ghost'><File Id='gf' Title='G' MachineId='ghost' Path='C:/x/a.log' Clearable='1'/></FilePackage>" );
			using var bed = await StartBed( s );

			await bed.Operator.StartPlanAsync( "p" );

			await bed.WaitUntilAsync( async () => ( await Plan( bed ) ).OpStatus == PlanState.EOpStatus.Success, Timeout, "the plan reports Success anyway" );
			await WaitForResult( bed, "the result of the marking is recorded" );

			var result = ( await Plan( bed ) ).MarkResult!;
			Assert.AreEqual( 0, result.Marked );
			Assert.IsTrue( result.Errors.Count > 0, "the reason is recorded" );
			StringAssert.Contains( result.Errors[0], "online" );
		}

		[TestMethod()]
		public async Task AMarkingThatTakesTooLongIsStoppedBeforeTheAppsLaunch()
		{
			SlowMarking( 20000 );
			using var bed = await StartBed( World( ( "MarkFiles", "run.pkg" ), ( "MarkTimeout", "2" ) ) );

			var watch = System.Diagnostics.Stopwatch.StartNew();
			await bed.Operator.StartPlanAsync( "p" );

			await bed.WaitUntilAsync( async () => await AllRunning( bed, "m1.camera", "m2.recorder" ), Timeout, "the apps launch after the timeout" );
			Assert.IsTrue( watch.Elapsed < TimeSpan.FromSeconds( 15 ), "not after the marking would have been done" );

			var result = ( await Plan( bed ) ).MarkResult!;
			Assert.IsTrue( result.TimedOut );
			Assert.AreEqual( 0, result.Marked );
			Assert.AreEqual( string.Empty, ( await Plan( bed ) ).Phase );

			// and the operation was stopped, not left to run on
			await bed.WaitUntilAsync( async () => ( await bed.Operator.GetAllScriptsStateAsync() ).All( kv => kv.Value.Status != EScriptStatus.Running ),
				Timeout, "the marking script was stopped, not left running" );
		}

		[TestMethod()]
		public async Task AStopDuringTheMarkingLaunchesNothingAndTheResultIsRecordedLater()
		{
			SlowMarking( 2500 );
			using var bed = await StartBed( World( ( "MarkFiles", "run.pkg" ) ) );

			await StartAndSeeMarking( bed );

			await bed.SendCliCommandAsync( "StopPlan p", Timeout );
			await bed.WaitUntilAsync( async () => ( await Plan( bed ) ).OpStatus == PlanState.EOpStatus.None, Timeout, "the plan is stopped" );

			await WaitForResult( bed, "the marking ended and was recorded although the plan was stopped" );

			Assert.IsFalse( await AnyRunning( bed, "m1.camera", "m2.recorder" ), "no app was launched" );
			Assert.AreEqual( 2, ( await Plan( bed ) ).MarkResult!.Marked );
			Assert.AreEqual( PlanState.EOpStatus.None, ( await Plan( bed ) ).OpStatus );
			Assert.IsTrue( await IsMarked( bed ) );
		}

		[TestMethod()]
		public async Task AStartWhileTheMarkingIsStillGoingLaunchesWhenItEndsWithoutMarkingAgain()
		{
			SlowMarking( 2500 );
			using var bed = await StartBed( World( ( "MarkFiles", "run.pkg" ) ) );

			await StartAndSeeMarking( bed );
			var started = ( await Plan( bed ) ).MarkStarted;
			await bed.SendCliCommandAsync( "StopPlan p", Timeout );
			await bed.Operator.StartPlanAsync( "p" );
			await bed.WaitUntilAsync( async () => ( await Plan( bed ) ).Running, Timeout, "the plan runs again" );

			var ps = await Plan( bed );
			StringAssert.StartsWith( ps.Phase, "Marking" );
			Assert.AreEqual( started, ps.MarkStarted, "no second marking was started" );
			Assert.AreEqual( PlanState.EOpStatus.InProgress, ps.OpStatus );
			Assert.IsFalse( await AnyRunning( bed, "m1.camera", "m2.recorder" ) );

			await bed.WaitUntilAsync( async () => await AllRunning( bed, "m1.camera", "m2.recorder" ), Timeout, "launched when the marking ended" );
			Assert.AreEqual( 2, ( await Plan( bed ) ).MarkResult!.Marked );
		}

		[TestMethod()]
		public async Task ARestartMarksAgainAfterTheAppsHaveDied()
		{
			using var bed = await StartBed( World( ( "MarkFiles", "run.pkg" ) ) );

			await bed.Operator.StartPlanAsync( "p" );
			await bed.WaitUntilAsync( async () => ( await Plan( bed ) ).OpStatus == PlanState.EOpStatus.Success, Timeout, "the plan is up" );
			var firstMark = ( await Plan( bed ) ).MarkStarted;
			Assert.IsNotNull( firstMark );

			SlowMarking( 2500 );
			await bed.SendCliCommandAsync( "RestartPlan p", Timeout );

			// the apps are taken down, then the marking begins, and only after it the apps come back
			await bed.WaitUntilAsync( async () =>
			{
				var ps = await Plan( bed );
				return ps.Phase.Length > 0 && ps.MarkStarted > firstMark;
			}, Timeout, "the restart marks again" );
			Assert.IsFalse( await AnyRunning( bed, "m1.camera", "m2.recorder" ), "the apps stay down while the marking goes" );

			await bed.WaitUntilAsync( async () => await AllRunning( bed, "m1.camera", "m2.recorder" ), Timeout, "the apps are back" );
		}

		[TestMethod()]
		public async Task AKillAllDuringTheMarkingStopsThePlanButDoesNotCutTheMarking()
		{
			SlowMarking( 2500 );
			using var bed = await StartBed( World( ( "MarkFiles", "run.pkg" ) ) );

			await StartAndSeeMarking( bed );

			await bed.SendCliCommandAsync( "KillAll", Timeout );
			await bed.WaitUntilAsync( async () => ( await Plan( bed ) ).OpStatus == PlanState.EOpStatus.None, Timeout, "the plan is stopped" );
			Assert.IsTrue( ( await Plan( bed ) ).Phase.Length > 0, "but the marking goes on" );

			await WaitForResult( bed, "the marking finished" );

			var result = ( await Plan( bed ) ).MarkResult!;
			Assert.IsFalse( result.Cancelled, "Kill All does not cancel it" );
			Assert.AreEqual( 2, result.Marked );
			Assert.IsFalse( await AnyRunning( bed, "m1.camera", "m2.recorder" ), "no app was launched" );
			Assert.AreEqual( PlanState.EOpStatus.None, ( await Plan( bed ) ).OpStatus );
		}

		[TestMethod()]
		public async Task ASecondKillAllInTheWindowCancelsTheMarkingAndThePlanIsNotStuck()
		{
			SlowMarking( 20000 );
			using var bed = await StartBed( World( ( "MarkFiles", "run.pkg" ) ) );

			await StartAndSeeMarking( bed );

			// both on the master's thread without a tick between: the first Kill All is still going when the second comes
			await OnMaster( bed, () => bed.Master.KillAll( "test", new KillAllArgs() ) );
			Assert.IsTrue( ( await Plan( bed ) ).Phase.Length > 0, "the first Kill All leaves the marking alone" );

			await OnMaster( bed, () =>
			{
				bed.Master.KillAll( "test", new KillAllArgs() );
				bed.Master.KillAll( "test", new KillAllArgs() );
			} );

			await WaitForResult( bed, "the second Kill All cancelled the marking" );
			var ps = await Plan( bed );
			Assert.IsTrue( ps.MarkResult!.Cancelled );
			Assert.AreEqual( string.Empty, ps.Phase );
			Assert.AreEqual( PlanState.EOpStatus.None, ps.OpStatus );
			Assert.IsFalse( await AnyRunning( bed, "m1.camera", "m2.recorder" ) );
		}

		[TestMethod()]
		public async Task AMarkingScriptKilledByHandIsRecordedAsCancelled()
		{
			SlowMarking( 20000 );
			using var bed = await StartBed( World( ( "MarkFiles", "run.pkg" ) ) );

			await bed.Operator.StartPlanAsync( "p" );

			Guid script = default;
			await bed.WaitUntilAsync( async () =>
			{
				var alive = ( await bed.Operator.GetAllScriptsStateAsync() ).Where( kv => kv.Value.IsAlive && ( kv.Value.Text ?? "" ).Contains( "machine(s)" ) ).ToList();
				if( alive.Count == 0 ) return false;
				script = alive.First().Key;
				return true;
			}, Timeout, "the marking script runs" );

			await bed.Operator.KillScriptAsync( script );

			await WaitForResult( bed, "the plan notices" );
			Assert.IsTrue( ( await Plan( bed ) ).MarkResult!.Cancelled );
		}

		[TestMethod()]
		public async Task ThePlanStateCarriesPhaseAndResultOverTheCliAndRest()
		{
			SlowMarking( 2000 );
			using var bed = await StartBed( World( ( "MarkFiles", "run.pkg" ) ) );

			// a plan that has not marked: the line is what it ever was
			var idle = await bed.SendCliCommandAsync( "GetPlanState p", Timeout );
			Assert.AreEqual( "PLAN:p:None", idle[0] );

			await StartAndSeeMarking( bed );
			var during = ( await bed.SendCliCommandAsync( "GetPlanState p", Timeout ) )[0];
			StringAssert.StartsWith( during, "PLAN:p:InProgress:{" );
			using( var doc = JsonDocument.Parse( during.Substring( "PLAN:p:InProgress:".Length ) ) )
				StringAssert.StartsWith( doc.RootElement.GetProperty( "phase" ).GetString(), "Marking files" );

			await WaitForResult( bed, "done" );
			var after = ( await bed.SendCliCommandAsync( "GetPlanState p", Timeout ) )[0];
			var json = after.Substring( after.IndexOf( ":{" ) + 1 );
			using var doc2 = JsonDocument.Parse( json );
			Assert.AreEqual( 2, doc2.RootElement.GetProperty( "markResult" ).GetProperty( "marked" ).GetInt32() );
			Assert.IsFalse( doc2.RootElement.TryGetProperty( "phase", out _ ), "no phase once it is over" );
		}
	}
}
