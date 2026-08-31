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
	/// Two clicks around a test run: Clear or Mark before it, Download after it, and an archive
	/// holding that run rather than the whole afternoon.
	/// </summary>
	/// <remarks>
	/// What each test is really about is the boundary - which bytes end up in the archive and which
	/// do not - so the files are seeded and appended to by the test itself. The applications are
	/// never started: a live writer would turn the content into a race, and none of this is about a
	/// running application.
	/// </remarks>
	[TestClass()]
	public class MarkAndClearTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 30 );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		static Task<TestBed.TestBed> StartBed()
			=> TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = Worlds.MarkableWorld() } );

		/// <summary>The one archive the download produced, or a failure naming what is there instead.</summary>
		static string TheArchive( TestBed.TestBed bed )
		{
			var archives = Archive.In( bed.DownloadFolder );
			Assert.AreEqual( 1, archives.Count, $"found: {Archive.Describe( bed.DownloadFolder )}" );
			return archives[0];
		}

		[TestMethod()]
		public async Task MarkThenCollectDeliversOnlyWhatTheRunWrote()
		{
			using var bed = await StartBed();

			var package = await bed.Operator.GetVfsNodeAsync( "run.pkg" );

			var marked = await bed.Operator.MarkFilesAsync( package, timeout: Timeout );
			Assert.AreEqual( 2, marked.Marked, "one log per application" );
			Assert.AreEqual( 0, marked.Cleared, "Mark destroys nothing" );

			// the test run
			File.AppendAllText( Worlds.LogOf( bed, "m1", "camera" ), "camera: THE RUN\n" );
			File.AppendAllText( Worlds.LogOf( bed, "m2", "recorder" ), "recorder: THE RUN\n" );

			await bed.Operator.DownloadAsync( package, timeout: Timeout );

			var archive = TheArchive( bed );
			var entries = Archive.EntriesOf( archive );

			// named for what it is, so the archive listing alone shows which files are partial
			Assert.IsTrue( entries.Any( e => e.EndsWith( "app.since-mark.log", StringComparison.OrdinalIgnoreCase ) ),
				$"entries: {string.Join( ", ", entries )}" );

			var camera = Archive.TextOf( archive, "camera/app.since-mark.log" );
			StringAssert.Contains( camera, "camera: THE RUN", "the run has to be in there" );
			Assert.IsFalse( camera.Contains( "yesterday" ),
				$"and nothing from before the mark; got:\n{camera}" );

			var recorder = Archive.TextOf( archive, "recorder/app.since-mark.log" );
			StringAssert.Contains( recorder, "recorder: THE RUN" );
			Assert.IsFalse( recorder.Contains( "yesterday" ), $"got:\n{recorder}" );

			// the config file travels in the same archive, whole, having been marked by nothing
			var config = Archive.TextOf( archive, "camera/app.cfg" );
			Assert.AreEqual( Worlds.CameraConfig, config,
				"a file that is not Clearable is always collected in full" );
		}

		[TestMethod()]
		public async Task TheEntryHeaderSaysWhichPartOfTheFileItIs()
		{
			// whoever opens the archive months later has only what is inside it to go by
			using var bed = await StartBed();

			var package = await bed.Operator.GetVfsNodeAsync( "run.pkg" );
			await bed.Operator.MarkFilesAsync( package, timeout: Timeout );

			File.AppendAllText( Worlds.LogOf( bed, "m1", "camera" ), "camera: THE RUN\n" );

			await bed.Operator.DownloadAsync( package, timeout: Timeout );

			var text = Archive.TextOf( TheArchive( bed ), "camera/app.since-mark.log" );
			var header = text.Split( '\n' )[0];

			StringAssert.Contains( header, "Dirigent", $"header line: {header}" );
			StringAssert.Contains( header, "mark", $"header line: {header}" );
			StringAssert.Contains( header, "not included", $"header line: {header}" );
		}

		[TestMethod()]
		public async Task ClearEmptiesWhatNobodyHoldsAndMarksTheRest()
		{
			using var bed = await StartBed();

			var package = await bed.Operator.GetVfsNodeAsync( "run.pkg" );

			var cameraLog = Worlds.LogOf( bed, "m1", "camera" );
			var recorderLog = Worlds.LogOf( bed, "m2", "recorder" );

			// what a logger does to its file on a running system. An exclusive open fails while any
			// other handle is open, whatever that handle permits - which is what Clear measures.
			using( var held = new FileStream( cameraLog, FileMode.Open, FileAccess.Write,
											FileShare.ReadWrite | FileShare.Delete ) )
			{
				var cleared = await bed.Operator.ClearFilesAsync( package, timeout: Timeout );

				Assert.AreEqual( 1, cleared.Cleared, "the log nobody holds is really emptied" );
				Assert.AreEqual( 1, cleared.Marked, "the one in use gets a mark instead" );
				Assert.AreEqual( 0, cleared.Failed, "and neither is a failure" );
			}

			Assert.IsFalse( File.Exists( recorderLog ), "the free log was deleted, not just truncated" );
			Assert.IsTrue( File.Exists( cameraLog ), "the held one was left alone" );

			// both configuration files are untouched, which is the whole point of the flag
			Assert.AreEqual( Worlds.CameraConfig, File.ReadAllText( Worlds.ConfigOf( bed, "m1", "camera" ) ) );
			Assert.AreEqual( Worlds.RecorderConfig, File.ReadAllText( Worlds.ConfigOf( bed, "m2", "recorder" ) ) );

			// the run
			File.AppendAllText( cameraLog, "camera: THE RUN\n" );
			File.WriteAllText( recorderLog, "recorder: THE RUN\n" );

			await bed.Operator.DownloadAsync( package, timeout: Timeout );

			var archive = TheArchive( bed );

			var camera = Archive.TextOf( archive, "camera/app.since-mark.log" );
			StringAssert.Contains( camera, "camera: THE RUN" );
			Assert.IsFalse( camera.Contains( "yesterday" ), $"cut at the mark; got:\n{camera}" );

			// a file that was deleted and written again is new, carries no mark, and arrives whole
			var recorder = Archive.TextOf( archive, "recorder/app.log" );
			Assert.AreEqual( "recorder: THE RUN\n", recorder.Replace( "\r\n", "\n" ),
				"a cleared file needs no header - all of it is the run" );
		}

		[TestMethod()]
		public async Task ANonClearableNodeIsSkippedAndCounted()
		{
			// the count is what makes a forgotten Clearable="1" discoverable: without it a log would
			// quietly keep its old contents and the archive would look wrong for no visible reason
			using var bed = await StartBed();

			var package = await bed.Operator.GetVfsNodeAsync( "run.pkg" );

			var result = await bed.Operator.ClearFilesAsync( package, timeout: Timeout );

			Assert.AreEqual( 2, result.Skipped, "the two configuration files" );
			Assert.AreEqual( 2, result.Cleared, "the two logs" );

			Assert.IsTrue( File.Exists( Worlds.ConfigOf( bed, "m1", "camera" ) ) );
			Assert.IsTrue( File.Exists( Worlds.ConfigOf( bed, "m2", "recorder" ) ) );
		}

		[TestMethod()]
		public async Task UnmarkRestoresTheWholeHistory()
		{
			using var bed = await StartBed();

			var package = await bed.Operator.GetVfsNodeAsync( "run.pkg" );
			await bed.Operator.MarkFilesAsync( package, timeout: Timeout );

			File.AppendAllText( Worlds.LogOf( bed, "m1", "camera" ), "camera: THE RUN\n" );

			var unmarked = await bed.Operator.UnmarkFilesAsync( package, timeout: Timeout );
			Assert.AreEqual( 2, unmarked.Unmarked );

			await bed.Operator.DownloadAsync( package, timeout: Timeout );

			var archive = TheArchive( bed );
			var camera = Archive.TextOf( archive, "camera/app.log" );

			StringAssert.Contains( camera, "yesterday", "the history is back" );
			StringAssert.Contains( camera, "camera: THE RUN" );
		}

		[TestMethod()]
		public async Task TheArgumentNarrowsWhatIsActedOn()
		{
			// one package can carry a Clear that touches the logs and nothing else
			using var bed = await StartBed();

			var package = await bed.Operator.GetVfsNodeAsync( "run.pkg" );

			var result = await bed.Operator.ClearFilesAsync( package, args: "log", timeout: Timeout );

			Assert.AreEqual( 2, result.Cleared, "the log nodes" );
			Assert.AreEqual( 0, result.Skipped,
				"the configuration nodes were not in scope at all - a different thing from being refused" );
		}

		[TestMethod()]
		public async Task AFileReplacedAfterTheMarkArrivesWholeWithANote()
		{
			// rotation, as it looks from here: app.log is moved aside and a fresh one takes its name.
			// The mark's offset then points into a file that no longer exists, so the whole of what is
			// there now is collected - slightly more than the run rather than nothing of it.
			using var bed = await StartBed();

			var package = await bed.Operator.GetVfsNodeAsync( "run.pkg" );
			await bed.Operator.MarkFilesAsync( package, timeout: Timeout );

			var cameraLog = Worlds.LogOf( bed, "m1", "camera" );
			File.Move( cameraLog, cameraLog + ".1" );
			File.WriteAllText( cameraLog, "camera: after the rotation, a whole new file of its own\n" );

			await bed.Operator.DownloadAsync( package, timeout: Timeout );

			var archive = TheArchive( bed );
			var entries = Archive.EntriesOf( archive );

			Assert.IsTrue( entries.Any( e => e.EndsWith( "camera/app.log", StringComparison.OrdinalIgnoreCase ) ),
				$"the rotated file arrives whole, under its own name; entries: {string.Join( ", ", entries )}" );

			var camera = Archive.TextOf( archive, "camera/app.log" );
			StringAssert.Contains( camera, "after the rotation" );

			// and the archive says why it holds more than was asked for
			var report = Archive.TextOf( archive, "_incomplete.txt" );
			StringAssert.Contains( report, "replaced", $"the report should explain the stale mark:\n{report}" );
		}

		[TestMethod()]
		public async Task TheCoverNoteSaysWhichRunTheArchiveCovers()
		{
			using var bed = await StartBed();

			var package = await bed.Operator.GetVfsNodeAsync( "run.pkg" );
			await bed.Operator.MarkFilesAsync( package, timeout: Timeout );

			File.AppendAllText( Worlds.LogOf( bed, "m1", "camera" ), "camera: THE RUN\n" );

			await bed.Operator.DownloadAsync( package, timeout: Timeout );

			var note = Archive.TextOf( TheArchive( bed ), "_comment.txt" );

			StringAssert.Contains( note, "Since",
				$"the difference between 'the logs' and 'the logs of one run' belongs at the top:\n{note}" );
		}

		[TestMethod()]
		public async Task MarkingTwiceMovesTheLineForward()
		{
			// what somebody who runs two cases in a row expects: the second mark delimits the second
			// run, and the first one is not in the way
			using var bed = await StartBed();

			var package = await bed.Operator.GetVfsNodeAsync( "run.pkg" );
			var cameraLog = Worlds.LogOf( bed, "m1", "camera" );

			await bed.Operator.MarkFilesAsync( package, timeout: Timeout );
			File.AppendAllText( cameraLog, "camera: THE FIRST RUN\n" );

			await bed.Operator.MarkFilesAsync( package, timeout: Timeout );
			File.AppendAllText( cameraLog, "camera: THE SECOND RUN\n" );

			await bed.Operator.DownloadAsync( package, timeout: Timeout );

			var camera = Archive.TextOf( TheArchive( bed ), "camera/app.since-mark.log" );

			StringAssert.Contains( camera, "THE SECOND RUN" );
			Assert.IsFalse( camera.Contains( "THE FIRST RUN" ), $"got:\n{camera}" );
		}

		[TestMethod()]
		public async Task CollectingDoesNotClearTheMark()
		{
			// somebody re-downloading after a failed transfer expects the same window again
			using var bed = await StartBed();

			var package = await bed.Operator.GetVfsNodeAsync( "run.pkg" );
			await bed.Operator.MarkFilesAsync( package, timeout: Timeout );

			File.AppendAllText( Worlds.LogOf( bed, "m1", "camera" ), "camera: THE RUN\n" );

			await bed.Operator.DownloadAsync( package, timeout: Timeout );
			await bed.Operator.DownloadAsync( package, timeout: Timeout );

			var archives = Archive.In( bed.DownloadFolder );
			Assert.AreEqual( 2, archives.Count, $"found: {Archive.Describe( bed.DownloadFolder )}" );

			foreach( var archive in archives )
			{
				var camera = Archive.TextOf( archive, "camera/app.since-mark.log" );
				StringAssert.Contains( camera, "camera: THE RUN" );
				Assert.IsFalse( camera.Contains( "yesterday" ),
					$"the second download must cover the same run; got:\n{camera}" );
			}
		}
	}
}
