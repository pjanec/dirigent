using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// The feature the whole harness was built for: collecting the recent log files of several
	/// applications spread over several machines into one archive, and nothing else.
	/// </summary>
	/// <remarks>
	/// Every "machine" in a tier-1 bed is this one, so a slave finds the download folder on its own
	/// disk and writes to it directly - which is exactly what the scripts now do when no file share
	/// covers the folder. What this tier cannot cover is a real SMB hop; that is tier 3.
	/// </remarks>
	[TestClass()]
	public class LogDownloadTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 30 );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		[TestMethod()]
		public async Task PackageOfLogsArrivesAsASingleArchive()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = Worlds.LoggingWorld() } );

			await Worlds.StartLoggingApps( bed, Timeout );

			var package = await bed.Operator.GetVfsNodeAsync( "logs.all" );
			await bed.Operator.DownloadAsync( package, timeout: Timeout );

			var archives = Archive.In( bed.DownloadFolder );
			Assert.AreEqual( 1, archives.Count,
				$"one archive expected, found: {Archive.Describe( bed.DownloadFolder )}" );

			var entries = Archive.EntriesOf( archives[0] );

			// the live log of every application, each under its own machine and application folder
			Assert.IsTrue( Archive.HasEntryMatching( entries, "m1/", "camera/", "app.log" ),
				$"missing entry; entries: {string.Join( ", ", entries )}" );
			Assert.IsTrue( Archive.HasEntryMatching( entries, "m1/", "tracker/", "app.log" ),
				$"missing entry; entries: {string.Join( ", ", entries )}" );
			Assert.IsTrue( Archive.HasEntryMatching( entries, "m2/", "recorder/", "app.log" ),
				$"missing entry; entries: {string.Join( ", ", entries )}" );

			// yesterday's file is recent enough to be wanted
			Assert.IsTrue( Archive.HasEntryMatching( entries, "m1/", "camera/", "recent.log" ),
				$"missing entry; entries: {string.Join( ", ", entries )}" );

			// nine days old, and the node asks for nothing older than two days
			Assert.IsFalse( entries.Any( e => e.EndsWith( "ancient.log", StringComparison.OrdinalIgnoreCase ) ),
				$"the nine-day-old file should have been filtered out, entries: {string.Join( ", ", entries )}" );

			// the staging folder the machines uploaded their parts to is not left behind
			var leftovers = Directory.GetDirectories( bed.DownloadFolder );
			Assert.AreEqual( 0, leftovers.Length,
				$"no folder should be left in the download folder, found: {string.Join( ", ", leftovers.Select( Path.GetFileName ) )}" );

			// each part is built in place under a temporary name and renamed when complete, so no
			// half-written archive may be left over either
			var partials = Directory.GetFiles( bed.DownloadFolder, "*.part", SearchOption.AllDirectories );
			Assert.AreEqual( 0, partials.Length,
				$"no partial file should remain, found: {string.Join( ", ", partials.Select( Path.GetFileName ) )}" );

			// and the operator was told where the files went
			var messages = bed.Operator.Notifications.Select( n => n.Message ?? "" ).ToList();
			Assert.IsTrue( messages.Any( m => m.Contains( "downloaded", StringComparison.OrdinalIgnoreCase ) ),
				$"the operator should have been notified, saw: {string.Join( " | ", messages )}" );
			Assert.IsFalse( messages.Any( m => m.Contains( "failed", StringComparison.OrdinalIgnoreCase ) ),
				$"nothing should have failed, saw: {string.Join( " | ", messages )}" );
		}

		[TestMethod()]
		public async Task PerMachineDownloadKeepsTheArchivesApart()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = Worlds.LoggingWorld() } );

			await Worlds.StartLoggingApps( bed, Timeout );

			var package = await bed.Operator.GetVfsNodeAsync( "logs.all" );
			await bed.Operator.DownloadAsync( package, perMachine: true, timeout: Timeout );

			var archives = Archive.In( bed.DownloadFolder );
			Assert.AreEqual( 2, archives.Count,
				$"one archive per machine expected, found: {Archive.Describe( bed.DownloadFolder )}" );

			// the machine each archive came from is in its name, and nothing was merged
			foreach( var machine in new[] { "m1", "m2" } )
			{
				Assert.IsTrue(
					archives.Any( a => Path.GetFileNameWithoutExtension( a ).EndsWith( "_" + machine, StringComparison.Ordinal ) ),
					$"an archive from {machine} expected, found: {string.Join( ", ", archives.Select( Path.GetFileName ) )}" );
			}

			// per machine means no machine-name folder inside - the file name already says it
			var fromM2 = archives.Single( a => Path.GetFileNameWithoutExtension( a ).EndsWith( "_m2", StringComparison.Ordinal ) );
			var entries = Archive.EntriesOf( fromM2 );
			Assert.IsTrue( Archive.HasEntryMatching( entries, "recorder/", "app.log" ),
				$"missing entry; entries: {string.Join( ", ", entries )}" );
			Assert.IsFalse( entries.Any( e => e.Contains( "camera", StringComparison.OrdinalIgnoreCase ) ),
				$"m2's archive should hold m2's files only, entries: {string.Join( ", ", entries )}" );
		}

		[TestMethod()]
		public async Task TheOperatorIsToldWhereTheArchiveIsAndOfferedToSeeIt()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = Worlds.LoggingWorld() } );

			await Worlds.StartLoggingApps( bed, Timeout );

			var package = await bed.Operator.GetVfsNodeAsync( "logs.all" );
			await bed.Operator.DownloadAsync( package, timeout: Timeout );

			var archive = Archive.In( bed.DownloadFolder ).Single();

			var notification = bed.Operator.Notifications.Last(
				n => ( n.Message ?? "" ).Contains( "downloaded", StringComparison.OrdinalIgnoreCase ) );

			// the full path of the archive, so the GUI can select it without guessing where it went
			Assert.AreEqual( archive, notification.RevealFilePath,
				"the notification should name the archive that was produced" );

			// an OK/Cancel pair with no explanation is a dialog the user just dismisses
			StringAssert.Contains( notification.Message, "Explorer",
				"the message should say what confirming it does" );

			// the reveal is a plain file path, not a <Tool> action - it must work on a machine whose
			// LocalConfig defines no WinExplorer tool
			Assert.IsNull( notification.Action,
				"showing the download should not depend on a tool being configured" );
		}

		[TestMethod()]
		public async Task OnlyTheTailOfAHugeLogIsCollected()
		{
			// the 60 GB unrotated log, in miniature: the end of it is what an investigation needs,
			// and the whole of it is not transferable at all
			var scenario = Scenario.OneMachine()
				.App( "m1.camera", a => a
					.LongRunning()
					.WithFolderNode( "tailed", "{applogs}", mask: "*.log", tailBytes: 1024 ) );

			// 20 numbered lines of 500 bytes each, so both the cut and its line alignment are visible
			var lines = string.Concat( Enumerable.Range( 1, 20 )
							.Select( i => $"line {i:D2} " + new string( 'x', 480 ) + "\n" ) );
			scenario.Seed( "m1.camera", "huge.log", ageDays: 0, content: lines );
			scenario.Seed( "m1.camera", "small.log", ageDays: 1, content: "a small complete file\n" );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );

			var node = await bed.Operator.GetVfsNodeAsync( "tailed" );
			await bed.Operator.DownloadAsync( node, timeout: Timeout );

			var archives = Archive.In( bed.DownloadFolder );
			Assert.AreEqual( 1, archives.Count, $"found: {Archive.Describe( bed.DownloadFolder )}" );

			var entries = Archive.EntriesOf( archives[0] );

			// the truncated file is named for what it holds, so the listing alone shows it is partial
			Assert.IsTrue( entries.Any( e => e.EndsWith( "huge.last1KB.log", StringComparison.OrdinalIgnoreCase ) ),
				$"entries: {string.Join( ", ", entries )}" );
			Assert.IsFalse( entries.Any( e => e.EndsWith( "/huge.log", StringComparison.OrdinalIgnoreCase ) ),
				$"the whole file should not be there under its plain name, entries: {string.Join( ", ", entries )}" );

			// a file below the limit is collected whole, under its own name
			Assert.IsTrue( entries.Any( e => e.EndsWith( "small.log", StringComparison.OrdinalIgnoreCase ) ),
				$"entries: {string.Join( ", ", entries )}" );

			var tail = Archive.TextOf( archives[0], "huge.last1KB.log" );
			var tailLines = tail.Split( '\n', StringSplitOptions.RemoveEmptyEntries );

			// the first line explains what this file is
			StringAssert.Contains( tailLines[0], "Dirigent", "the entry says what it is" );
			StringAssert.Contains( tailLines[0], "huge.log", "and which file it came from" );

			// what follows is the end of the log, in whole lines
			Assert.IsTrue( tail.Contains( "line 20" ), "the last line of the log must be there" );
			Assert.IsFalse( tail.Contains( "line 01" ), "the beginning of the log must not be" );
			Assert.IsTrue( tailLines.Skip( 1 ).All( l => l.StartsWith( "line " ) ),
				$"every collected line should be a whole one, got: {string.Join( " | ", tailLines.Skip( 1 ).Select( l => l.Substring( 0, Math.Min( 12, l.Length ) ) ) )}" );

			// roughly the requested amount, never the whole file
			Assert.IsTrue( tail.Length < 2048, $"about 1 KB expected, got {tail.Length} bytes" );

			// and the archive carries the reason, for whoever opens it later
			var report = Archive.TextOf( archives[0], "_incomplete.txt" );
			StringAssert.Contains( report, "huge.log", "the report names the truncated file" );
			StringAssert.Contains( report, "TailBytes", "and the setting that caused it" );
		}

		[TestMethod()]
		public async Task SizeBudgetPassesOverAnOversizedFileAndSaysSo()
		{
			// a folder holding one unrotated giant among the rotated ones: the budget must keep the
			// files that fit rather than stop at the giant, and the archive must admit what is missing
			var scenario = Scenario.OneMachine()
				.App( "m1.camera", a => a
					.LongRunning()
					.WithFolderNode( "bounded", "{applogs}", mask: "*.log", maxTotalBytes: 1000 ) );

			scenario.Seed( "m1.camera", "small-new.log", ageDays: 0, sizeBytes: 100 );
			scenario.Seed( "m1.camera", "huge.log", ageDays: 1, sizeBytes: 5000 );
			scenario.Seed( "m1.camera", "small-old.log", ageDays: 2, sizeBytes: 100 );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );

			var node = await bed.Operator.GetVfsNodeAsync( "bounded" );
			await bed.Operator.DownloadAsync( node, timeout: Timeout );

			var archives = Archive.In( bed.DownloadFolder );
			Assert.AreEqual( 1, archives.Count, $"found: {Archive.Describe( bed.DownloadFolder )}" );

			var entries = Archive.EntriesOf( archives[0] );

			// the oversized file is left out...
			Assert.IsFalse( entries.Any( e => e.EndsWith( "huge.log", StringComparison.OrdinalIgnoreCase ) ),
				$"5000 bytes do not fit a 1000 byte budget, entries: {string.Join( ", ", entries )}" );

			// ...and both small ones are in, including the one behind it in the newest-first order
			Assert.IsTrue( entries.Any( e => e.EndsWith( "small-new.log", StringComparison.OrdinalIgnoreCase ) ),
				$"entries: {string.Join( ", ", entries )}" );
			Assert.IsTrue( entries.Any( e => e.EndsWith( "small-old.log", StringComparison.OrdinalIgnoreCase ) ),
				$"the file behind the oversized one still fits the budget, entries: {string.Join( ", ", entries )}" );

			// and the archive itself says it is incomplete, naming what was dropped
			Assert.IsTrue( entries.Any( e => e.EndsWith( "_incomplete.txt", StringComparison.OrdinalIgnoreCase ) ),
				$"an incomplete archive has to admit it, entries: {string.Join( ", ", entries )}" );

			var report = Archive.TextOf( archives[0], "_incomplete.txt" );
			var hugeSize = new FileInfo( Path.Combine( bed.RenderContext.AppLogsDir( "m1", "camera" ), "huge.log" ) ).Length;

			StringAssert.Contains( report, "huge.log", "the report names the file that was left out" );
			StringAssert.Contains( report, hugeSize.ToString(), "and its size" );
		}

		[TestMethod()]
		public async Task LogHeldOpenByItsWriterIsStillCollected()
		{
			// the normal state of a log file: the application producing it has it open for writing.
			// The archive is written by streaming the sources, so the share flags that open uses are
			// the difference between collecting a live log and reporting an error for it.
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = Worlds.LoggingWorld() } );

			await Worlds.StartLoggingApps( bed, Timeout );

			var heldOpen = Path.Combine( bed.RenderContext.AppLogsDir( "m1", "camera" ), "recent.log" );

			using( var writer = new FileStream( heldOpen, FileMode.Open, FileAccess.Write, FileShare.ReadWrite ) )
			{
				writer.Write( System.Text.Encoding.UTF8.GetBytes( "a line written while the file is open\n" ) );
				writer.Flush();

				var all = await bed.Operator.GetAllVfsNodesAsync();
				var cameraLogs = all.Single( n => n.Id == "log" && n.AppId == "camera" );

				await bed.Operator.DownloadAsync( cameraLogs, timeout: Timeout );
			}

			var archives = Archive.In( bed.DownloadFolder );
			Assert.AreEqual( 1, archives.Count, $"found: {Archive.Describe( bed.DownloadFolder )}" );

			var entries = Archive.EntriesOf( archives[0] );
			Assert.IsTrue( entries.Any( e => e.EndsWith( "recent.log", StringComparison.OrdinalIgnoreCase ) ),
				$"the log held open by its writer should have been collected, entries: {string.Join( ", ", entries )}" );

			Assert.IsTrue( Archive.SizeOf( archives[0], "recent.log" ) > 0,
				"the collected entry should not be empty" );

			var messages = bed.Operator.Notifications.Select( n => n.Message ?? "" ).ToList();
			Assert.IsFalse( messages.Any( m => m.Contains( "failed", StringComparison.OrdinalIgnoreCase ) ),
				$"nothing should have failed, saw: {string.Join( " | ", messages )}" );
		}

		[TestMethod()]
		public async Task CollectedFilesKeepTheirModificationTime()
		{
			// the age of a log is half its evidence, so the archive has to carry it. An entry created
			// by hand carries the time the archive was made unless the source time is set explicitly.
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = Worlds.LoggingWorld() } );

			await Worlds.StartLoggingApps( bed, Timeout );

			var seeded = Path.Combine( bed.RenderContext.AppLogsDir( "m1", "camera" ), "recent.log" );
			var sourceTime = File.GetLastWriteTime( seeded );

			var all = await bed.Operator.GetAllVfsNodesAsync();
			var cameraLogs = all.Single( n => n.Id == "log" && n.AppId == "camera" );

			await bed.Operator.DownloadAsync( cameraLogs, timeout: Timeout );

			var archives = Archive.In( bed.DownloadFolder );
			var entryTime = Archive.TimeOf( archives[0], "recent.log" );

			// the zip format keeps DOS timestamps, whose resolution is two seconds
			Assert.IsTrue( Math.Abs( ( entryTime - sourceTime ).TotalSeconds ) <= 3,
				$"the entry should carry the file's own time {sourceTime:s}, carries {entryTime:s}" );

			// which is a day ago, definitely not the moment the archive was made
			Assert.IsTrue( ( DateTime.Now - entryTime ).TotalHours > 1,
				$"the seeded file is a day old, the entry claims {entryTime:s}" );
		}

		[TestMethod()]
		public async Task DownloadOfOneApplicationsLogsTakesOnlyItsOwn()
		{
			// the everyday case: right-click one application, take its recent logs
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = Worlds.LoggingWorld() } );

			await Worlds.StartLoggingApps( bed, Timeout );

			var all = await bed.Operator.GetAllVfsNodesAsync();
			var cameraLogs = all.Single( n => n.Id == "log" && n.AppId == "camera" );

			await bed.Operator.DownloadAsync( cameraLogs, timeout: Timeout );

			var archives = Archive.In( bed.DownloadFolder );
			Assert.AreEqual( 1, archives.Count, $"found: {Archive.Describe( bed.DownloadFolder )}" );

			var entries = Archive.EntriesOf( archives[0] );
			Assert.IsTrue( entries.Any( e => e.EndsWith( "app.log", StringComparison.OrdinalIgnoreCase ) ),
				$"the camera's live log should be there, entries: {string.Join( ", ", entries )}" );
			Assert.IsFalse( entries.Any( e => e.Contains( "tracker", StringComparison.OrdinalIgnoreCase ) ),
				$"only the camera's logs were asked for, entries: {string.Join( ", ", entries )}" );
			Assert.IsFalse( entries.Any( e => e.EndsWith( "ancient.log", StringComparison.OrdinalIgnoreCase ) ),
				$"the nine-day-old file should have been filtered out, entries: {string.Join( ", ", entries )}" );
		}

	}
}
