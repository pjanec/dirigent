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
	/// The single-instance scripts of the master, addressed by guid: what starting one twice does to
	/// the others, and what a config reload does to all of them.
	/// </summary>
	/// <remarks>
	/// The regression tests for a scoping bug: disposing one script entry used to dispose the whole
	/// LocalScriptRegistry, which is shared by every entry and by the master itself - so ending one
	/// script ended every script running there. It was reached by restarting a script by its id and
	/// by every ReloadSharedConfig.
	/// </remarks>
	[TestClass()]
	public class SingletonScriptTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 30 );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		/// <summary>
		/// Two plans naming machines no agent will serve, so a script waiting for either of them
		/// keeps running until somebody stops it.
		/// </summary>
		static Scenario World()
			=> Scenario.TwoMachinesWithIdlers()
				.RawXml( "<Plan Name='never'><App AppIdTuple='ghost.app' ExeFullPath='[cmd]'/></Plan>" )
				.RawXml( "<Plan Name='never2'><App AppIdTuple='ghost2.app' ExeFullPath='[cmd]'/></Plan>" );

		static Task<TestBed.TestBed> StartBed()
			=> TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World() } );

		static string WaitingScript( Guid instance, string plan )
			=> $"StartScript {instance} {Scripts.BuiltIn.RunPlanWhenMachinesOnline._Name} \"{{Plan:'{plan}'}}\"";

		async Task StartAndAwaitRunning( TestBed.TestBed bed, Guid instance, string plan = "never" )
		{
			var lines = await bed.SendCliCommandAsync( WaitingScript( instance, plan ), Timeout );
			CollectionAssert.AreEqual( new List<string>() { "ACK" }, lines.ToList(),
				string.Join( " | ", lines ) );

			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetScriptStateAsync( instance ) )?.Status == EScriptStatus.Running,
				Timeout, $"script {instance} runs" );
		}

		[TestMethod()]
		public async Task RestartingOneScriptLeavesTheOthersAlone()
		{
			using var bed = await StartBed();

			var bystander = Guid.NewGuid();
			var restarted = Guid.NewGuid();

			await StartAndAwaitRunning( bed, bystander );
			await StartAndAwaitRunning( bed, restarted );

			// start it again under the same id - the case a plan step repeating on every start would hit
			await bed.SendCliCommandAsync( WaitingScript( restarted, "never" ), Timeout );

			// the one nobody asked about has to be untouched, now and for a while
			for( int i = 0; i < 5; i++ )
			{
				var state = await bed.Operator.GetScriptStateAsync( bystander );
				Assert.AreEqual( EScriptStatus.Running, state?.Status,
					"restarting one script must not cancel another" );
				await Task.Delay( 100 );
			}
		}

		[TestMethod()]
		public async Task RestartingARunningScriptReallyStartsItAgain()
		{
			// the other half of the same bug: a start was declined while the old instance was still
			// cancelling, so the restart stopped the script and quietly did not start it
			using var bed = await StartBed();

			var instance = Guid.NewGuid();
			await StartAndAwaitRunning( bed, instance, "never" );

			// the same id, a different plan - so what it does next says which definition is running
			await bed.SendCliCommandAsync( WaitingScript( instance, "no.such.plan" ), Timeout );

			await bed.WaitUntilAsync(
				async () =>
				{
					var state = await bed.Operator.GetScriptStateAsync( instance );
					return state?.Status == EScriptStatus.Failed;
				},
				Timeout, "the restarted script runs with its new arguments and fails on the unknown plan" );

			var failed = await bed.Operator.GetScriptStateAsync( instance );
			StringAssert.Contains( failed!.Data ?? "", "no.such.plan",
				"the failure is the new run's, not the old one's" );

			// and the one that was replaced does not report back afterwards: its cancellation
			// completes on its own thread, and the Cancelled it would publish carries an instance id
			// that now belongs to the new script - so everything watching would show the live script
			// as cancelled. Watched for a while, because the old run notices the cancellation only
			// when it next comes up for air.
			var until = DateTime.UtcNow + TimeSpan.FromSeconds( 1.5 );
			while( DateTime.UtcNow < until )
			{
				var state = await bed.Operator.GetScriptStateAsync( instance );
				Assert.AreEqual( EScriptStatus.Failed, state?.Status,
					"the replaced run must stay silent - this is the live script's state" );
				await Task.Delay( 100 );
			}
		}

		[TestMethod()]
		public async Task AConfigReloadStopsTheSingletonScripts()
		{
			// Pinned as it is, not changed: a reload rebuilds every script definition, and ending a
			// definition has ended its script since long before this work - the registry-wide dispose
			// only made it look wider than it was. What the fix changes is the blast radius of ending
			// ONE script, which the other tests here cover.
			using var bed = await StartBed();

			var instance = Guid.NewGuid();
			await StartAndAwaitRunning( bed, instance );

			await bed.ReloadSharedConfigAsync( World() );

			await bed.WaitUntilAsync(
				async () => !( ( await bed.Operator.GetScriptStateAsync( instance ) )?.IsAlive ?? false ),
				Timeout, "the reload stops the running singleton scripts" );

			var state = await bed.Operator.GetScriptStateAsync( instance );
			Assert.AreEqual( EScriptStatus.Cancelled, state?.Status, "and stops them by cancelling" );
		}

		[TestMethod()]
		public async Task KillingAScriptEndsOnlyThatOne()
		{
			using var bed = await StartBed();

			var killed = Guid.NewGuid();
			var bystander = Guid.NewGuid();

			await StartAndAwaitRunning( bed, killed );
			await StartAndAwaitRunning( bed, bystander );

			await bed.SendCliCommandAsync( $"KillScript {killed}", Timeout );

			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetScriptStateAsync( killed ) )?.Status == EScriptStatus.Cancelled,
				Timeout, "the script named in the kill stops" );

			var state = await bed.Operator.GetScriptStateAsync( bystander );
			Assert.AreEqual( EScriptStatus.Running, state?.Status, "and nothing else does" );
		}
	}
}
