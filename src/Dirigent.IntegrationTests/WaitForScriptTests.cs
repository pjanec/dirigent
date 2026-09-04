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
	/// `WaitForScript` - the command that answers when a script is over rather than when it has
	/// started, and the machinery that lets it wait without stopping the master.
	/// </summary>
	[TestClass()]
	public class WaitForScriptTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 30 );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		/// <summary>
		/// The markable world, plus a plan referring to a machine no agent will ever serve - a script
		/// waiting for that plan's machines never finishes, which is how the timeout is tested.
		/// </summary>
		static Task<TestBed.TestBed> StartBed()
			=> TestBed.TestBed.StartAsync( new TestBedOptions()
			{
				Scenario = Worlds.MarkableWorld()
					.RawXml( "<Plan Name='never'><App AppIdTuple='ghost.app' ExeFullPath='[cmd]'/></Plan>" ),
			} );

		static string Describe( IReadOnlyList<string> lines )
			=> lines.Count == 0 ? "(no lines)" : string.Join( " | ", lines );

		[TestMethod()]
		public async Task TheAnswerComesWhenTheScriptIsOverNotWhenItStarts()
		{
			// The proof that it waited: the moment the command answers, the script is already
			// finished. Without the wait the state would still be Running here - the mark spans
			// several master ticks, resolving the package and dispatching a slave per machine.
			using var bed = await StartBed();

			var instance = Guid.NewGuid();
			var package = await bed.Operator.GetVfsNodeAsync( "run.pkg" );

			var lines = await bed.SendCliCommandAsync(
					$"StartScript {instance} {Scripts.BuiltIn.MarkFiles._Name} \"{Args( package.Id )}\""
					+ $" ; WaitForScript {instance} timeout=30", Timeout );

			CollectionAssert.AreEqual( new List<string>() { "ACK", "ACK", "END" }, lines.ToList(),
				$"the start acknowledges, the wait acknowledges, and END says it is over: {Describe( lines )}" );

			var state = await bed.Operator.GetScriptStateAsync( instance );
			Assert.AreEqual( EScriptStatus.Finished, state?.Status,
				"the script must already be finished when the command answers" );

			// and it really did the work
			var result = Tools.Deserialize<Scripts.BuiltIn.MarkOrClearFiles.TResult>( state!.Data );
			Assert.AreEqual( 2, result?.Marked, "one log per application" );
		}

		[TestMethod()]
		public async Task WaitingForAScriptThatHasAlreadyFinishedAnswersAtOnce()
		{
			using var bed = await StartBed();

			var instance = Guid.NewGuid();
			await bed.SendCliCommandAsync( $"StartScript {instance} {Scripts.BuiltIn.ListVfsNodes._Name}", Timeout );

			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetScriptStateAsync( instance ) )?.Status == EScriptStatus.Finished,
				Timeout, "the script finishes on its own" );

			var lines = await bed.SendCliCommandAsync( $"WaitForScript {instance}", Timeout );

			CollectionAssert.AreEqual( new List<string>() { "ACK", "END" }, lines.ToList(), Describe( lines ) );
		}

		[TestMethod()]
		public async Task WaitingForAnUnknownScriptAnswersOneErrorAndNoAck()
		{
			// nothing to wait for is a failure of the command, and it is reported before the
			// acknowledgement so that the answer is a single ERROR line
			using var bed = await StartBed();

			var lines = await bed.SendCliCommandAsync( $"WaitForScript {Guid.NewGuid()}", Timeout );

			Assert.AreEqual( 1, lines.Count, Describe( lines ) );
			StringAssert.StartsWith( lines[0], "ERROR", Describe( lines ) );
			Assert.IsFalse( lines.Any( l => l.StartsWith( "ACK" ) ), Describe( lines ) );
		}

		[TestMethod()]
		public async Task AFailedScriptIsAnError()
		{
			using var bed = await StartBed();

			var instance = Guid.NewGuid();

			// no node to act on, so the script throws
			var lines = await bed.SendCliCommandAsync(
					$"StartScript {instance} {Scripts.BuiltIn.MarkFiles._Name} \"{{}}\""
					+ $" ; WaitForScript {instance} timeout=30", Timeout );

			Assert.AreEqual( "ACK", lines[0], Describe( lines ) );
			Assert.AreEqual( "ACK", lines[1], Describe( lines ) );
			StringAssert.StartsWith( lines[2], "ERROR", Describe( lines ) );
			StringAssert.Contains( lines[2], "failed", Describe( lines ) );

			// the reason the script gave, not just "it failed"
			StringAssert.Contains( lines[2], "nothing to act on", Describe( lines ) );
		}

		[TestMethod()]
		public async Task TheTimeoutStopsTheScriptAndSaysSo()
		{
			using var bed = await StartBed();

			var instance = Guid.NewGuid();

			// waits for a machine that no agent serves, so it would wait for ever
			var lines = await bed.SendCliCommandAsync(
					$"StartScript {instance} {Scripts.BuiltIn.RunPlanWhenMachinesOnline._Name} \"{{Plan:'never'}}\""
					+ $" ; WaitForScript {instance} timeout=1", Timeout );

			Assert.AreEqual( "ACK", lines[0], Describe( lines ) );
			Assert.AreEqual( "ACK", lines[1], Describe( lines ) );
			StringAssert.StartsWith( lines[2], "ERROR", Describe( lines ) );
			StringAssert.Contains( lines[2], "did not finish", Describe( lines ) );

			// stopped rather than left running: a script that lands its work after the caller has
			// carried on is worse than one that never ran
			await bed.WaitUntilAsync(
				async () =>
				{
					var status = ( await bed.Operator.GetScriptStateAsync( instance ) )?.Status;
					return status == EScriptStatus.Cancelled;
				},
				Timeout, "the script the wait gave up on is stopped" );
		}

		[TestMethod()]
		public async Task ABadTimeoutIsRefusedBeforeAnythingIsWaitedFor()
		{
			using var bed = await StartBed();

			var instance = Guid.NewGuid();
			await bed.SendCliCommandAsync( $"StartScript {instance} {Scripts.BuiltIn.ListVfsNodes._Name}", Timeout );

			var lines = await bed.SendCliCommandAsync( $"WaitForScript {instance} timeout=soon", Timeout );

			Assert.AreEqual( 1, lines.Count, Describe( lines ) );
			StringAssert.StartsWith( lines[0], "ERROR", Describe( lines ) );
			StringAssert.Contains( lines[0], "timeout", Describe( lines ) );
		}

		[TestMethod()]
		public async Task AWaitingCommandDoesNotStopTheMaster()
		{
			// the whole reason the waiting is done by a command that reports itself unfinished rather
			// than by one that blocks: everything else has to carry on meanwhile
			using var bed = await StartBed();

			var instance = Guid.NewGuid();

			var waiting = bed.SendCliCommandAsync(
					$"StartScript {instance} {Scripts.BuiltIn.RunPlanWhenMachinesOnline._Name} \"{{Plan:'never'}}\""
					+ $" ; WaitForScript {instance} timeout=25", Timeout );

			// while that sits there, the master answers other requests and runs other work
			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetScriptStateAsync( instance ) )?.Status == EScriptStatus.Running,
				Timeout, "the awaited script is running" );

			var others = await bed.SendCliCommandAsync( "GetAllPlansState", TimeSpan.FromSeconds( 5 ) );
			Assert.AreEqual( "END", others.Last(), $"another request was answered meanwhile: {Describe( others )}" );

			var apps = await bed.SendCliCommandAsync( "GetAllAppsState", TimeSpan.FromSeconds( 5 ) );
			Assert.AreEqual( "END", apps.Last(), Describe( apps ) );

			Assert.IsFalse( waiting.IsCompleted, "and the wait is still waiting" );

			// clean up: stop the script so the request ends
			await bed.SendCliCommandAsync( $"KillScript {instance}", Timeout );

			var lines = await waiting;
			StringAssert.StartsWith( lines.Last(), "ERROR", $"a killed script is an error: {Describe( lines )}" );
			StringAssert.Contains( lines.Last(), "cancelled", Describe( lines ) );
		}

		/// <summary>
		/// The arguments of a Mark/Clear script naming a package by its config id.
		/// </summary>
		/// <remarks>
		/// Single quotes need no doubling here: the whole thing is wrapped in double quotes on the
		/// command line, and inside those the master's tokenizer treats a single quote as a plain
		/// character - which is what lets the relaxed JSON through.
		/// </remarks>
		static string Args( string packageId ) => $"{{Node:{{Id:'{packageId}'}}}}";
	}
}
