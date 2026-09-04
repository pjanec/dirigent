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
	/// What the master answers to every text command - the shape, not just the effect.
	/// </summary>
	/// <remarks>
	/// This is a characterisation suite: it exists to pin the behaviour production depends on, so
	/// that a change to the command pipeline has to break a test before it can break a batch file, a
	/// telnet client, a REST caller or a [dirigent.command] plan step. It was written against the
	/// code as it stood before any of that was touched.
	///
	/// The three response shapes it pins - ACK alone, lines then END, and a single ERROR - are what
	/// docs/CLI.md documents and what every client's read loop is built on.
	/// </remarks>
	[TestClass()]
	public class CliProtocolTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 30 );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		static Task<TestBed.TestBed> StartBed()
			=> TestBed.TestBed.StartAsync( new TestBedOptions()
			{
				Scenario = Scenario.TwoMachinesWithIdlers().Plan( "p1", "m1.idler" ),
			} );

		/// <summary>Fully qualified id of an app of the scenario, as the config really names it.</summary>
		static string App( TestBed.TestBed bed, string machine, string app )
			=> $"{bed.MachineIds[machine]}.{app}";

		[TestMethod()]
		public async Task ASimpleCommandAnswersAckAndNothingElse()
		{
			using var bed = await StartBed();

			var lines = await bed.SendCliCommandAsync( $"StartApp {App( bed, "m1", "idler" )}", Timeout );

			CollectionAssert.AreEqual( new List<string>() { "ACK" }, lines.ToList(),
				"one line, no terminator of its own: this is what every client treats as success" );
		}

		[TestMethod()]
		public async Task AListingAnswersItsLinesThenEndAndNeverAcks()
		{
			// the shape a client's read loop depends on: the lines are not terminal, the END is
			using var bed = await StartBed();

			var lines = await bed.SendCliCommandAsync( "GetAllAppsState", Timeout );

			Assert.IsTrue( lines.Count >= 2, $"expected app lines and an END, got: {Describe( lines )}" );
			Assert.AreEqual( "END", lines.Last(), $"the last line terminates the listing: {Describe( lines )}" );
			Assert.IsFalse( lines.Any( l => l.StartsWith( "ACK" ) ),
				$"a listing does not acknowledge, it just answers: {Describe( lines )}" );
			Assert.IsTrue( lines.Take( lines.Count - 1 ).All( l => l.StartsWith( "APP:" ) ),
				$"every line but the last is an app: {Describe( lines )}" );
		}

		[TestMethod()]
		public async Task EveryListingHasTheSameShape()
		{
			using var bed = await StartBed();

			foreach( var (command, prefix) in new[]
			{
				( "GetAllAppsState", "APP:" ),
				( "GetAllPlansState", "PLAN:" ),
				( "GetAllClientsState", "CLIENT:" ),
			} )
			{
				var lines = await bed.SendCliCommandAsync( command, Timeout );

				Assert.AreEqual( "END", lines.Last(), $"{command} answered: {Describe( lines )}" );
				Assert.IsFalse( lines.Any( l => l.StartsWith( "ACK" ) ), $"{command}: {Describe( lines )}" );
				Assert.IsTrue( lines.Take( lines.Count - 1 ).All( l => l.StartsWith( prefix ) ),
					$"{command}: {Describe( lines )}" );
			}
		}

		[TestMethod()]
		public async Task AnUnknownCommandAnswersOneErrorAndNothingRuns()
		{
			// and the whole line is refused at parse time, which is why a request carrying an unknown
			// command answers once rather than once per command
			using var bed = await StartBed();

			var lines = await bed.SendCliCommandAsync(
					$"StartApp {App( bed, "m1", "idler" )} ; NoSuchCommand ; StopPlan p1", Timeout );

			Assert.AreEqual( 1, lines.Count, $"one answer for the whole line: {Describe( lines )}" );
			StringAssert.StartsWith( lines[0], "ERROR", Describe( lines ) );
			StringAssert.Contains( lines[0], "NoSuchCommand", Describe( lines ) );

			// nothing was executed - not even the command that came before the unknown one
			var state = await bed.Operator.GetAppStateAsync( bed.App( "m1", "idler" ) );
			Assert.IsFalse( state?.Started ?? false,
				"the parse of the whole line failed, so no command of it ran" );
		}

		[TestMethod()]
		public async Task AMissingArgumentAnswersOneError()
		{
			using var bed = await StartBed();

			var lines = await bed.SendCliCommandAsync( "StartApp", Timeout );

			Assert.AreEqual( 1, lines.Count, Describe( lines ) );
			StringAssert.StartsWith( lines[0], "ERROR", Describe( lines ) );
		}

		[TestMethod()]
		public async Task SeveralCommandsOnOneLineAnswerInOrder()
		{
			using var bed = await StartBed();

			var lines = await bed.SendCliCommandAsync(
					$"StartApp {App( bed, "m1", "idler" )} ; GetAllPlansState ; StopPlan p1", Timeout );

			// one terminal line per command, in the order sent, each of the kind that command uses -
			// the listing's END is in the middle, not at the end
			CollectionAssert.AreEqual(
				new List<string>() { "ACK", "PLAN:p1:None", "END", "ACK" }, lines.ToList(),
				$"answered: {Describe( lines )}" );
		}

		[TestMethod()]
		public async Task AFailingCommandDoesNotStopTheOnesAfterIt()
		{
			// this is what makes "all commands of the line must have succeeded" a meaningful rule:
			// the later commands still run and still answer
			using var bed = await StartBed();

			var lines = await bed.SendCliCommandAsync(
					"StartPlan no.such.plan ; GetAllPlansState", Timeout );

			StringAssert.StartsWith( lines[0], "ERROR", Describe( lines ) );
			Assert.AreEqual( "END", lines.Last(),
					$"the second command ran despite the first one failing: {Describe( lines )}" );
		}

		[TestMethod()]
		public async Task TheStateOfAnUnknownScriptIsAnEmptyAnswer()
		{
			// what WaitForScript has to distinguish from a script that has finished
			using var bed = await StartBed();

			var lines = await bed.SendCliCommandAsync( $"GetScriptState {Guid.NewGuid()}", Timeout );

			Assert.AreEqual( 0, lines.Count,
				$"nothing but an empty line, which carries no state: {Describe( lines )}" );
		}

		[TestMethod()]
		public async Task AScriptStateIsAnsweredWithoutAnyTerminator()
		{
			// the fourth shape, and the reason the one-shot CLI exits 4 on this command today:
			// a SCRIPT: line is not terminal and nothing follows it
			using var bed = await StartBed();

			var instance = Guid.NewGuid();
			var started = await bed.SendCliCommandAsync(
					$"StartScript {instance} BuiltIns/ListVfsNodes.cs", Timeout );
			CollectionAssert.AreEqual( new List<string>() { "ACK" }, started.ToList(), Describe( started ) );

			var lines = await bed.SendCliCommandAsync( $"GetScriptState {instance}", Timeout );

			Assert.AreEqual( 1, lines.Count, Describe( lines ) );
			StringAssert.StartsWith( lines[0], $"SCRIPT:{instance}:", Describe( lines ) );
			Assert.IsFalse( lines.Any( l => l == "END" || l.StartsWith( "ACK" ) ),
				$"no terminator at all: {Describe( lines )}" );
		}

		[TestMethod()]
		public async Task ARequestIdIsEchoedOnEveryLine()
		{
			// what the [dirigent.command] response matcher will rely on to tell its answer from
			// somebody else's
			using var bed = await StartBed();

			var lines = await bed.SendCliCommandAsync( "[req42] GetAllPlansState", Timeout );

			Assert.IsTrue( lines.All( l => l.StartsWith( "[req42] " ) ),
				$"every line carries the id, including the terminator: {Describe( lines )}" );
			Assert.AreEqual( "[req42] END", lines.Last(), Describe( lines ) );
		}

		[TestMethod()]
		public async Task CommandNamesAreCaseSensitive()
		{
			// Pinned because it is surprising, not because it is good: the command table is a plain
			// Dictionary with the default comparer (CommandRepository.Create), while the reserved exe
			// names and the init detector names are matched case-insensitively. Anyone tidying this up
			// would change what existing batch files may rely on, so it stays as it is and this test
			// says so out loud.
			using var bed = await StartBed();

			var lines = await bed.SendCliCommandAsync( "getallplansstate", Timeout );

			Assert.AreEqual( 1, lines.Count, Describe( lines ) );
			StringAssert.StartsWith( lines[0], "ERROR: Unknown command", Describe( lines ) );

			// and the exact spelling works
			var exact = await bed.SendCliCommandAsync( "GetAllPlansState", Timeout );
			Assert.AreEqual( "END", exact.Last(), Describe( exact ) );
		}

		static string Describe( IReadOnlyList<string> lines )
			=> lines.Count == 0 ? "(no lines)" : string.Join( " | ", lines );
	}
}
