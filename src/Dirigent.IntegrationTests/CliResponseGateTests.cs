using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// A plan step that issues a Dirigent command and holds the plan until that command is over -
	/// `&lt;App ExeFullPath="[dirigent.command]" InitCondition="cliresponse ok|any"&gt;`.
	/// </summary>
	/// <remarks>
	/// The point of the whole feature: a `System Start` plan drawing a line under the log files
	/// before the applications begin writing to them. A step like this has no process, so what the
	/// plan waits for is the master's answer rather than an exit code.
	/// </remarks>
	[TestClass()]
	public class CliResponseGateTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 30 );

		/// <summary>How long a "this does not happen" observation watches for.</summary>
		static readonly TimeSpan Window = TimeSpan.FromSeconds( 2 );

		/// <summary>The script instance the step starts - fixed, the way a config would write it.</summary>
		const string Instance = "7B3C1E90-1111-2222-3333-444455556666";

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		/// <summary>
		/// A plan whose second application waits for a first one that sends the given command line.
		/// </summary>
		/// <param name="condition">the step's InitCondition, or null for none at all</param>
		static Scenario World( string commandLine, string? condition )
			=> Scenario.TwoMachines()
				// something on m2, and a plan naming it: a script waiting for that plan's machines
				// waits exactly as long as m2's agent is away
				.App( "m2.idler", a => a.LongRunning() )
				.Plan( "needs_m2", "m2.idler" )

				.App( "m1.step", a => a.Exe( ReservedExeNames.DirigentCommand ).Args( commandLine ) )
				.App( "m1.follower", a => a.LongRunning() )

				.Plan( "gated", p => p
					.App( "m1.step", a =>
					{
						a.Volatile();
						if( condition is not null ) a.Attribute( "InitCondition", condition );
					} )
					.App( "m1.follower", a => a.DependsOn( "step" ) ) );

		static Task<TestBed.TestBed> StartBed( string commandLine, string? condition )
			=> TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World( commandLine, condition ) } );

		/// <summary>A command line that marks the logs: start a script, then wait for it.</summary>
		static string WaitsForMachines()
			=> $"StartScript {Instance} {Scripts.BuiltIn.RunPlanWhenMachinesOnline._Name} \"{{Plan:'needs_m2'}}\""
			 + $" ; WaitForScript {Instance} timeout=25";

		/// <summary>A command line whose script fails at once - no node to act on.</summary>
		static string FailingScript()
			=> $"StartScript {Instance} {Scripts.BuiltIn.MarkFiles._Name} \"{{}}\""
			 + $" ; WaitForScript {Instance} timeout=25";

		async Task<bool> IsStarted( TestBed.TestBed bed, string app )
			=> ( await bed.Operator.GetAppStateAsync( bed.App( "m1", app ) ) )?.Started ?? false;

		async Task<bool> IsInitialized( TestBed.TestBed bed, string app )
			=> ( await bed.Operator.GetAppStateAsync( bed.App( "m1", app ) ) )?.Initialized ?? false;

		/// <summary>Watches for a while that something does not happen.</summary>
		async Task AssertStaysFalse( Func<Task<bool>> condition, string because )
		{
			var until = DateTime.UtcNow + Window;
			while( DateTime.UtcNow < until )
			{
				Assert.IsFalse( await condition(), because );
				await Task.Delay( 100 );
			}
		}

		[TestMethod()]
		public async Task TheDependentAppWaitsUntilTheCommandIsOver()
		{
			using var bed = await StartBed( WaitsForMachines(), "cliresponse ok" );

			// with m2 away, the script the step starts cannot finish
			bed.StopAgent( "m2" );

			await bed.Operator.StartPlanAsync( "gated" );

			// the step itself runs - and stays uninitialized, so nothing behind it starts
			await bed.WaitUntilAsync( () => IsStarted( bed, "step" ), Timeout, "the step is launched" );
			await AssertStaysFalse( () => IsInitialized( bed, "step" ),
				"the step must not count as initialized while its command is still running" );
			Assert.IsFalse( await IsStarted( bed, "follower" ),
				"and the application depending on it must not have been launched" );

			// let the command finish: the machine it waits for comes back
			bed.StartAgent( "m2" );

			await bed.WaitUntilAsync( () => IsStarted( bed, "follower" ), Timeout,
				"once the command is over, the plan carries on" );

			Assert.IsTrue( await IsInitialized( bed, "step" ), "the step is initialized by the answer" );

			var script = await bed.Operator.GetScriptStateAsync( Guid.Parse( Instance ) );
			Assert.AreEqual( EScriptStatus.Finished, script?.Status,
				"the step's answer came from a script that really finished" );
		}

		[TestMethod()]
		public async Task AFailedCommandHoldsThePlanWhenTheConditionSaysOk()
		{
			using var bed = await StartBed( FailingScript(), "cliresponse ok" );

			await bed.Operator.StartPlanAsync( "gated" );

			await bed.WaitUntilAsync( () => IsStarted( bed, "step" ), Timeout, "the step is launched" );

			// the script fails within a tick or two, and 'ok' means the step is not satisfied
			await AssertStaysFalse( () => IsInitialized( bed, "step" ),
				"a failed command must leave the step uninitialized" );
			Assert.IsFalse( await IsStarted( bed, "follower" ), "so the plan does not carry on" );

			var plan = await bed.Operator.GetPlanStateAsync( "gated" );
			Assert.AreNotEqual( PlanState.EOpStatus.Success, plan?.OpStatus,
				"and the plan is not a success" );
		}

		[TestMethod()]
		public async Task AFailedCommandDoesNotHoldThePlanWhenTheConditionSaysAny()
		{
			// what the mark step wants: attempt it, log the failure, do not stop the system starting
			using var bed = await StartBed( FailingScript(), "cliresponse any" );

			await bed.Operator.StartPlanAsync( "gated" );

			await bed.WaitUntilAsync( () => IsStarted( bed, "follower" ), Timeout,
				"any answer initializes the step, so the plan carries on" );

			var script = await bed.Operator.GetScriptStateAsync( Guid.Parse( Instance ) );
			Assert.AreEqual( EScriptStatus.Failed, script?.Status,
				"and it really was a failure that was passed over" );
		}

		[TestMethod()]
		public async Task AStepWithoutTheConditionInitializesAtOnceAsItAlwaysHas()
		{
			// The compatibility guarantee: a [dirigent.command] step behaves exactly as before unless
			// it opts in. Its command here can never finish, and the plan still carries straight on.
			using var bed = await StartBed( WaitsForMachines(), null );

			bed.StopAgent( "m2" );

			await bed.Operator.StartPlanAsync( "gated" );

			await bed.WaitUntilAsync( () => IsStarted( bed, "follower" ), Timeout,
				"with no init condition the step is initialized at launch, as it always was" );
		}

		[TestMethod()]
		public async Task TheStepWaitsForEveryCommandOfItsLine()
		{
			// three commands, three different shapes of answer: a listing ends with END, StartScript
			// with ACK, and the wait with END. The step is satisfied only when the last one arrives.
			using var bed = await StartBed(
				"GetAllAppsState"
				+ $" ; StartScript {Instance} {Scripts.BuiltIn.RunPlanWhenMachinesOnline._Name} \"{{Plan:'needs_m2'}}\""
				+ $" ; WaitForScript {Instance} timeout=25",
				"cliresponse ok" );

			bed.StopAgent( "m2" );

			await bed.Operator.StartPlanAsync( "gated" );

			await bed.WaitUntilAsync( () => IsStarted( bed, "step" ), Timeout, "the step is launched" );
			await AssertStaysFalse( () => IsInitialized( bed, "step" ),
				"the listing and the start have answered, but the wait has not" );

			bed.StartAgent( "m2" );

			await bed.WaitUntilAsync( () => IsInitialized( bed, "step" ), Timeout,
				"the last command of the line answers and the step is done" );
		}

		[TestMethod()]
		public async Task AnUnknownCommandInTheLineFailsTheStep()
		{
			// the master refuses the whole line at parse time and answers once, not once per command:
			// the step has to settle on that single ERROR rather than wait for answers that never come
			using var bed = await StartBed( "GetAllAppsState ; NoSuchCommand", "cliresponse ok" );

			await bed.Operator.StartPlanAsync( "gated" );

			await bed.WaitUntilAsync( () => IsStarted( bed, "step" ), Timeout, "the step is launched" );

			await AssertStaysFalse( () => IsInitialized( bed, "step" ),
				"a refused command line is a failed step" );
			Assert.IsFalse( await IsStarted( bed, "follower" ) );
		}

		[TestMethod()]
		public async Task AnUnknownCommandIsPassedOverWhenTheConditionSaysAny()
		{
			using var bed = await StartBed( "NoSuchCommand", "cliresponse any" );

			await bed.Operator.StartPlanAsync( "gated" );

			await bed.WaitUntilAsync( () => IsStarted( bed, "follower" ), Timeout,
				"the answer arrived, which is all 'any' asks for" );
		}
	}
}
