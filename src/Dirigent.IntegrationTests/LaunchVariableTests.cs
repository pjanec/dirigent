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
	/// Variables given for one launch must not survive into the next. The deterministic harness
	/// asserted this on the wire - that a plan restart sends StartApp with null Vars. Here it is
	/// asserted where it matters: in the environment the process is actually given.
	/// </summary>
	[TestClass()]
	public class LaunchVariableTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 30 );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		[TestMethod()]
		public async Task VariablesOfOneLaunchDoNotSurviveIntoTheNext()
		{
			var scenario = Scenario.OneMachine()
				.App( "m1.worker", a => a.LongRunning().PrintsEnvironment() )
				.Plan( "run", p => p.App( "m1.worker" ) );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );

			var app = bed.App( "m1", "worker" );
			var envFile = Path.Combine( bed.RenderContext.AppDir( "m1", "worker" ), "env.txt" );

			// first launch, with a variable just for it
			await bed.Operator.StartAppAsync( app, vars: new Dictionary<string, string>()
			{
				{ "DIRIGENT_TEST_ONCE", "yes" },
			} );

			await bed.WaitUntilAsync(
				async () => await Task.FromResult( File.Exists( envFile ) ),
				Timeout, "the worker wrote its environment" );

			var first = await Files.ReadAllLinesAsync( envFile );
			Assert.IsTrue( first.Any( l => l.StartsWith( "DIRIGENT_TEST_ONCE=" ) ),
				"the variable should have reached the first launch" );

			// stop it, and remove the evidence so the next launch cannot be confused with this one
			await bed.Operator.KillAppAsync( app );
			await bed.WaitUntilAsync(
				async () => !( ( await bed.Operator.GetAppStateAsync( app ) )?.Running ?? true ),
				Timeout, "the worker stops" );

			File.Delete( envFile );

			// start it again through the plan, which says nothing about variables
			await bed.Operator.StartPlanAsync( "run" );

			await bed.WaitUntilAsync(
				async () => await Task.FromResult( File.Exists( envFile ) ),
				Timeout, "the worker ran again and wrote its environment" );

			var second = await Files.ReadAllLinesAsync( envFile );
			Assert.IsFalse( second.Any( l => l.StartsWith( "DIRIGENT_TEST_ONCE=" ) ),
				"the variable of the previous launch must not survive into this one" );
		}
	}
}
