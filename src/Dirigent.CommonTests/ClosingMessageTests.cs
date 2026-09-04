using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.Scripts.BuiltIn;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dirigent.Tests
{
	/// <summary>
	/// What the operator is told when a download finishes.
	/// </summary>
	/// <remarks>
	/// Reported from the field: an incident package over a system where most of the crash dump
	/// folders do not exist - because most machines have not crashed - produced a dialog hundreds of
	/// lines long, one sentence per absent folder. Nobody reads that, which is worse than it sounds:
	/// the two lines that were real problems were in there too, indistinguishable from the ordinary
	/// absences around them.
	///
	/// So the dialog counts rather than lists, and the archive stays the record - every absence in
	/// the `_incomplete.txt` of the machine it belongs to, every failure in `_comment.txt`. What
	/// these tests pin is that the counting never buries the two things worth acting on: a machine
	/// that delivered no archive at all, and an actual error.
	/// </remarks>
	[TestClass()]
	public class ClosingMessageTests
	{
		static readonly List<string> _oneArchive = new() { @"D:\Downloads\Incident_260902_1431.zip" };
		static readonly List<string> _none = new();

		static string Message(
				IReadOnlyList<string>? archives = null,
				int filesCollected = 100,
				int machinesDelivered = 6,
				int notCollectedCount = 0,
				IReadOnlyList<string>? machinesWithNoArchive = null,
				IReadOnlyList<string>? errors = null )
			=> DownloadZipped.ComposeClosingMessage(
				archives ?? _oneArchive,
				filesCollected,
				machinesDelivered,
				notCollectedCount,
				machinesWithNoArchive ?? _none,
				errors ?? _none );

		/// <summary>The notes a crashdump collection over a large system really produces.</summary>
		static List<string> ManyAbsences( int count )
			=> Enumerable.Range( 0, count ).Select( i =>
					$"'C:\\dumps\\app{i:00}' is named by the package but is not on m{i % 40:00},"
					+ $" so it is not in this archive." ).ToList();

		[TestMethod()]
		public void ACleanDownloadSaysWhatItGotAndNothingElseTest()
		{
			var text = Message( filesCollected: 1284, machinesDelivered: 6 );

			StringAssert.Contains( text, @"D:\Downloads\Incident_260902_1431.zip" );
			StringAssert.Contains( text, "1284 file(s) from 6 machine(s)." );

			// nothing went wrong, so nothing is said about anything going wrong
			Assert.IsFalse( text.Contains( "problem", StringComparison.OrdinalIgnoreCase ), text );
			Assert.IsFalse( text.Contains( "Nothing at all" ), text );
			Assert.IsFalse( text.Contains( "had nothing to collect" ), text );
		}

		[TestMethod()]
		public void HundredsOfAbsencesAreCountedNotListedTest()
		{
			// the report that prompted this: 300 absent dump folders
			var absences = ManyAbsences( 300 );

			var text = Message( filesCollected: 1284, notCollectedCount: absences.Count,
					machinesWithNoArchive: new[] { "m17" },
					errors: new[]
					{
						"m17: The network path was not found.",
						"m03: Access to the path 'C:\\logs\\svc.log' is denied.",
					} );

			Console.WriteLine( "the dialog an incident collection now produces:" );
			Console.WriteLine();
			Console.WriteLine( text );

			StringAssert.Contains( text, "300 item(s) named by the package had nothing to collect" );
			StringAssert.Contains( text, "_incomplete.txt", "and it says where to read about them" );

			// not one of the sentences itself
			foreach( var note in absences )
				Assert.IsFalse( text.Contains( note ), "the dialog is listing the absences again" );

			// a dialog somebody will actually read, even with a broken machine and two errors in it
			var lines = text.Split( '\n' ).Length;
			Assert.IsTrue( lines < 20, $"the closing message is {lines} lines long:\n{text}" );

			// and the absences alone, however many, add exactly one line to it
			var justAbsences = Message( filesCollected: 1284, notCollectedCount: 300 );
			var fewerAbsences = Message( filesCollected: 1284, notCollectedCount: 3 );

			Assert.AreEqual( fewerAbsences.Split( '\n' ).Length, justAbsences.Split( '\n' ).Length,
				$"three hundred absences take more room than three:\n{justAbsences}" );
		}

		[TestMethod()]
		public void AnAbsenceIsNotCalledAProblemTest()
		{
			// a folder that is not there is the ordinary state of affairs, and wording it as an error
			// is what teaches an operator to ignore the errors
			var text = Message( notCollectedCount: 23 );

			Assert.IsFalse( text.Contains( "problem", StringComparison.OrdinalIgnoreCase ), text );
			Assert.IsFalse( text.Contains( "error", StringComparison.OrdinalIgnoreCase ), text );
			Assert.IsFalse( text.Contains( "fail", StringComparison.OrdinalIgnoreCase ), text );
		}

		[TestMethod()]
		public void AMachineThatDeliveredNoArchiveIsNamedTest()
		{
			// The one absence that does not explain itself. Every other machine leaves an
			// _incomplete.txt saying what it was asked for and did not have; this one left nothing,
			// so if the dialog does not say it, nothing does.
			var text = Message(
				notCollectedCount: 300,
				machinesWithNoArchive: new[] { "m17" },
				errors: new[] { "m17: The network path was not found." } );

			StringAssert.Contains( text, "Nothing at all from m17" );
			StringAssert.Contains( text, "not even a record of what was there",
				"which is the part that makes it different from an empty collection" );
		}

		[TestMethod()]
		public void EveryMachineWithNoArchiveIsNamedHoweverManyTest()
		{
			// unlike the absences, these are never truncated: each one is a hole in the archive that
			// nothing else records
			var machines = Enumerable.Range( 0, 12 ).Select( i => $"m{i:00}" ).ToList();

			var text = Message( machinesWithNoArchive: machines );

			foreach( var machine in machines )
				StringAssert.Contains( text, machine, $"{machine} is not named" );
		}

		[TestMethod()]
		public void RealProblemsAreNamedButCappedTest()
		{
			var errors = Enumerable.Range( 0, 9 )
					.Select( i => $"m{i:00}: Access to the path 'C:\\logs\\svc{i}.log' is denied." )
					.ToList();

			var text = Message( errors: errors );

			StringAssert.Contains( text, "9 problems" );

			// the first few by name, so that the commonest case is actionable without opening anything
			for( int i = 0; i < 5; i++ )
				StringAssert.Contains( text, $"m{i:00}: Access to the path" );

			Assert.IsFalse( text.Contains( "m05:" ), "the list stops rather than growing without limit" );
			StringAssert.Contains( text, "and 4 more.", "and says how many it did not name" );
			StringAssert.Contains( text, "_comment.txt", "and where all of them are written down" );
		}

		[TestMethod()]
		public void OneProblemIsNotCalledOneProblemsTest()
		{
			var text = Message( errors: new[] { "m03: The device is not ready." } );

			StringAssert.Contains( text, "One problem:" );
			StringAssert.Contains( text, "m03: The device is not ready." );
		}

		[TestMethod()]
		public void AProblemsMessageIsOnlyOneLineEachTest()
		{
			// an exception message can carry a stack trace or several lines of remote detail, and one
			// of those turns the capped list back into the dialog this was meant to end
			var text = Message( errors: new[]
			{
				"m03: Could not open the file.\nAt some place.\nAnd another.",
			} );

			StringAssert.Contains( text, "m03: Could not open the file." );
			Assert.IsFalse( text.Contains( "At some place" ), text );
		}

		[TestMethod()]
		[DataRow( "one line only", "one line only" )]
		[DataRow( "windows\r\nsecond", "windows" )]
		[DataRow( "unix\nsecond", "unix" )]
		[DataRow( "old mac\rsecond", "old mac" )]
		[DataRow( "\nleading break", "" )]
		[DataRow( "", "" )]
		public void ShorteningAMessageWorksWhicheverLineBreakItUsesTest( string message, string expected )
		{
			// Not a detail of the wording: a message broken with "\n" and no "\r" - an exception
			// composed in code, or anything from a machine that is not Windows - used to throw here,
			// and here is inside the reporting of a problem that has already happened.
			Assert.AreEqual( expected, Tools.JustFirstLine( message ) );
		}

		[TestMethod()]
		public void ADownloadThatProducedNothingDoesNotPointIntoAnArchiveTest()
		{
			// there is no archive to read the detail in, so it must not be advertised
			var text = Message(
				archives: _none,
				notCollectedCount: 12,
				errors: new[] { "m01: The network path was not found." } );

			StringAssert.Contains( text, "No files downloaded." );
			StringAssert.Contains( text, "12 item(s) named by the package had nothing to collect" );

			Assert.IsFalse( text.Contains( "_incomplete.txt" ), text );
			Assert.IsFalse( text.Contains( "_comment.txt" ), text );
		}
	}

	/// <summary>
	/// One machine's failure being that machine's failure, rather than the download's.
	/// </summary>
	/// <remarks>
	/// The slaves used to be awaited together, and a `WhenAll` hands back the first exception and
	/// nothing else. So a single machine that could not write its archive - an unreachable share, a
	/// full disk - ended the whole download and threw away everything every other machine had
	/// already collected, which is the opposite of what the script intends: one machine or one file
	/// must not cost the rest.
	/// </remarks>
	[TestClass()]
	public class SlaveOutcomeTests
	{
		static Task<DownloadZippedSlave.TResult> Delivered()
			=> Task.FromResult( new DownloadZippedSlave.TResult() { ZipFileName = "part_m1.zip" } );

		static async Task<DownloadZippedSlave.TResult> Faulted()
		{
			await Task.Yield();
			throw new Exception( "The network path was not found." );
		}

		static async Task<DownloadZippedSlave.TResult> Cancelled()
		{
			await Task.Yield();
			throw new OperationCanceledException();
		}

		[TestMethod()]
		public async Task AMachineThatDeliveredIsReportedAsHavingDeliveredTest()
		{
			var (result, failure) = await DownloadZipped.Outcome( Delivered() );

			Assert.IsNotNull( result );
			Assert.AreEqual( "part_m1.zip", result!.ZipFileName );
			Assert.IsNull( failure );
		}

		[TestMethod()]
		public async Task AMachineThatFailedIsReportedRatherThanThrownTest()
		{
			var (result, failure) = await DownloadZipped.Outcome( Faulted() );

			Assert.IsNull( result );
			Assert.IsNotNull( failure, "the failure has to come back as this machine's, not be thrown" );
			StringAssert.Contains( failure!.Message, "The network path was not found." );
		}

		[TestMethod()]
		public async Task OneMachineFailingLeavesTheOthersTheirResultsTest()
		{
			// the whole point: what the other machines collected is still worth having
			var outcomes = new List<(DownloadZippedSlave.TResult? Result, Exception? Failure)>();

			foreach( var task in new[] { Delivered(), Faulted(), Delivered() } )
				outcomes.Add( await DownloadZipped.Outcome( task ) );

			Assert.AreEqual( 2, outcomes.Count( x => x.Result is not null ) );
			Assert.AreEqual( 1, outcomes.Count( x => x.Failure is not null ) );
		}

		[TestMethod()]
		public async Task ACancellationIsStillEverybodysBusinessTest()
		{
			// the operator stopped the download; that is not one machine's private problem, and
			// swallowing it here would leave the script finishing as though it had been asked to
			await Assert.ThrowsExceptionAsync<OperationCanceledException>(
				() => DownloadZipped.Outcome( Cancelled() ) );
		}
	}
}
