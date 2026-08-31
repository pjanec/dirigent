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
		public async Task ADownloadCanBeGivenTheNodeDefinitionToResolveItself()
		{
			// What the GUI does now: hand over the definition rather than a resolved tree, so that
			// resolving - one remote round trip per node, seconds for a package spanning machines -
			// happens inside the operation being watched instead of in front of it.
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = Worlds.LoggingWorld() } );

			await Worlds.StartLoggingApps( bed, Timeout );

			// the definition as declared in the config, not resolved
			var package = await bed.Operator.GetVfsNodeAsync( "logs.all" );
			Assert.AreEqual( 0, package.Children.Count( c => !c.IsContainer && c.Path is not null ),
				"the definition should not carry resolved files yet" );

			var result = await bed.Operator.RunScriptAsync<
					Scripts.BuiltIn.DownloadZipped.TArgs, Scripts.BuiltIn.DownloadZipped.TResult>(
				Scripts.BuiltIn.DownloadZipped._Name,
				new Scripts.BuiltIn.DownloadZipped.TArgs()
				{
					VfsNode = package,
					VfsNodeNeedsResolving = true,
				},
				timeout: Timeout );

			Assert.IsNotNull( result );

			var archives = Archive.In( bed.DownloadFolder );
			Assert.AreEqual( 1, archives.Count, $"found: {Archive.Describe( bed.DownloadFolder )}" );

			var entries = Archive.EntriesOf( archives[0] );
			Assert.IsTrue( Archive.HasEntryMatching( entries, "m1/", "camera/", "app.log" ),
				$"the same result as a pre-resolved download; entries: {string.Join( ", ", entries )}" );
			Assert.IsTrue( Archive.HasEntryMatching( entries, "m2/", "recorder/", "app.log" ),
				$"entries: {string.Join( ", ", entries )}" );
		}

		[TestMethod()]
		public async Task TheArchiveCarriesACoverNote()
		{
			// The operator's reason for collecting, and enough context to make sense of the archive
			// a year later: which machines at which addresses, which package, when.
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = Worlds.LoggingWorld() } );

			await Worlds.StartLoggingApps( bed, Timeout );

			var package = await bed.Operator.GetVfsNodeAsync( "logs.all" );
			var resolved = await bed.Operator.ResolveAsync( package );

			await bed.Operator.RunScriptAsync<
					Scripts.BuiltIn.DownloadZipped.TArgs, Scripts.BuiltIn.DownloadZipped.TResult>(
				Scripts.BuiltIn.DownloadZipped._Name,
				new Scripts.BuiltIn.DownloadZipped.TArgs()
				{
					VfsNode = resolved,
					Comment = "Symphony froze during the 14:20 run.\nRestarted IgManager twice.",
				},
				timeout: Timeout );

			var archive = Archive.In( bed.DownloadFolder ).Single();
			var note = Archive.TextOf( archive, "_comment.txt" );

			// what the operator said, as they said it
			StringAssert.Contains( note, "Symphony froze during the 14:20 run." );
			StringAssert.Contains( note, "Restarted IgManager twice." );

			// and the context that makes it useful
			StringAssert.Contains( note, "Logs/All apps", "the package it came from" );
			StringAssert.Contains( note, "logs.all", "and its id" );
			StringAssert.Contains( note, DateTime.Now.ToString( "yyyy-MM-dd" ), "when" );

			// every machine that took part
			foreach( var machine in new[] { "m1", "m2" } )
			{
				StringAssert.Contains( note, bed.RenderContext.MachineId( machine ),
					$"{machine} should be named in the note: {note}" );
			}
		}

		[TestMethod()]
		public async Task TheCoverNoteNamesTheAddressThatIdentifiesTheMachine()
		{
			// The address a connection came from identifies nothing when the client sits on the
			// master's own machine - it is loopback, which was what the note used to print. The
			// configured address is the one that says where the machine is.
			var scenario = Worlds.LoggingWorld()
				.DeclaredIp( "m1", "192.168.0.150" )
				.DeclaredIp( "m2", "192.168.0.120" );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );

			await Worlds.StartLoggingApps( bed, Timeout );

			var package = await bed.Operator.GetVfsNodeAsync( "logs.all" );
			await bed.Operator.DownloadAsync( package, timeout: Timeout );

			var note = Archive.TextOf( Archive.In( bed.DownloadFolder ).Single(), "_comment.txt" );

			StringAssert.Contains( note, "192.168.0.150", $"the declared address of m1: {note}" );
			StringAssert.Contains( note, "192.168.0.120", $"the declared address of m2: {note}" );

			// the machines really did connect over loopback here, and saying so would only be noise
			Assert.IsFalse( note.Contains( "127.0.0.1" ),
				$"a loopback connection address identifies nothing and does not belong in the note: {note}" );
		}

		[TestMethod()]
		public async Task ACollectionWithNoCommentStillExplainsItself()
		{
			// The header answers the questions an archive raises on its own, so it is written even
			// when the operator had nothing to add.
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = Worlds.LoggingWorld() } );

			await Worlds.StartLoggingApps( bed, Timeout );

			var package = await bed.Operator.GetVfsNodeAsync( "logs.all" );
			await bed.Operator.DownloadAsync( package, timeout: Timeout );

			var note = Archive.TextOf( Archive.In( bed.DownloadFolder ).Single(), "_comment.txt" );

			StringAssert.Contains( note, "Machines" );
			StringAssert.Contains( note, "(no comment)" );
		}

		[TestMethod()]
		public async Task ATitleThatIsNoFileNameStillProducesAnArchive()
		{
			// The archive is named after the node, and a title is free text nobody writes with file
			// naming in mind. A colon in one used to fail the write at the very end - after every
			// machine had already collected and uploaded its part.
			var scenario = Scenario.OneMachine()
				.App( "m1.camera", a => a.LongRunning().WithLogNode( title: "Logs: today 12:00" ) );

			scenario.Seed( "m1.camera", "app.log", ageDays: 0 );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );

			var node = await bed.Operator.GetVfsNodeAsync( "log" );
			await bed.Operator.DownloadAsync( node, timeout: Timeout );

			var archives = Archive.In( bed.DownloadFolder );
			Assert.AreEqual( 1, archives.Count, $"found: {Archive.Describe( bed.DownloadFolder )}" );

			var name = Path.GetFileName( archives[0] );
			Assert.IsFalse( name.Contains( ':' ), $"the archive name must be openable: {name}" );
			StringAssert.StartsWith( name, "Logs_ today 12_00",
				$"the title should still be recognisable in the name: {name}" );

			var entries = Archive.EntriesOf( archives[0] );
			Assert.IsTrue( entries.Any( e => e.EndsWith( "app.log", StringComparison.OrdinalIgnoreCase ) ),
				$"entries: {string.Join( ", ", entries )}" );
		}

		[TestMethod()]
		public async Task ApplicationNameIsNotRepeatedWhenTheContainerCarriesItAlready()
		{
			// log nodes titled after the applications they belong to. Collected through a package, each
			// node's own container is then a folder called "camera" / "tracker", and the per-application
			// subfolder used to say it again: "log/camera/camera/app.log".
			var scenario = Scenario.OneMachine()
				.App( "m1.camera", a => a.LongRunning().WithLogNode( title: "camera" ) )
				.App( "m1.tracker", a => a.LongRunning().WithLogNode( title: "tracker" ) )
				.Package( "logs.all", "Logs/All apps", p => p.RefAll( "log" ) );

			scenario.Seed( "m1.camera", "app.log", ageDays: 0 );
			scenario.Seed( "m1.tracker", "app.log", ageDays: 0 );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );

			var package = await bed.Operator.GetVfsNodeAsync( "logs.all" );
			await bed.Operator.DownloadAsync( package, timeout: Timeout );

			var entries = Archive.EntriesOf( Archive.In( bed.DownloadFolder ).Single() );

			Assert.IsFalse( entries.Any( e => e.Contains( "camera/camera", StringComparison.OrdinalIgnoreCase ) ),
				$"the application name should appear once, entries: {string.Join( ", ", entries )}" );
			Assert.IsTrue( entries.Any( e => e.EndsWith( "camera/app.log", StringComparison.OrdinalIgnoreCase ) ),
				$"the file still belongs under its application's folder, entries: {string.Join( ", ", entries )}" );
			Assert.IsTrue( entries.Any( e => e.EndsWith( "tracker/app.log", StringComparison.OrdinalIgnoreCase ) ),
				$"entries: {string.Join( ", ", entries )}" );
		}

		[TestMethod()]
		public async Task ApplicationNameIsNotRepeatedFromFurtherUpThePath()
		{
			// an untitled <Folder> over the application's own directory: the folder is named after the
			// directory, which is named after the application, and the file sits one level deeper - so
			// the repetition was not adjacent: "camera/logs/camera/app.log".
			var scenario = Scenario.OneMachine()
				.App( "m1.camera", a => a.LongRunning().WithFolderNode( "tree", "{appdir}", mask: "*.log" ) )
				.Package( "logs.all", "Logs/All apps", p => p.RefAll( "tree" ) );

			scenario.Seed( "m1.camera", "app.log", ageDays: 0 );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );

			var package = await bed.Operator.GetVfsNodeAsync( "logs.all" );
			await bed.Operator.DownloadAsync( package, timeout: Timeout );

			var entries = Archive.EntriesOf( Archive.In( bed.DownloadFolder ).Single() );

			Assert.AreEqual( 1, entries.Count( e => e.EndsWith( "app.log", StringComparison.OrdinalIgnoreCase ) ),
				$"entries: {string.Join( ", ", entries )}" );
			Assert.IsTrue( entries.Any( e => e.EndsWith( "camera/logs/app.log", StringComparison.OrdinalIgnoreCase ) ),
				$"the application name belongs on the path once, entries: {string.Join( ", ", entries )}" );
		}

		[TestMethod()]
		public async Task ApplicationNameStillSeparatesTheSameNamedLogs()
		{
			// the reason the per-application subfolder exists: two applications whose log nodes carry
			// the same generic title resolve into one path, and only the application name keeps their
			// identically named files apart
			var scenario = Scenario.OneMachine()
				.App( "m1.camera", a => a.LongRunning().WithLogNode( title: "Recent logs" ) )
				.App( "m1.tracker", a => a.LongRunning().WithLogNode( title: "Recent logs" ) )
				.Package( "logs.all", "Logs/All apps", p => p.RefAll( "log" ) );

			scenario.Seed( "m1.camera", "app.log", ageDays: 0 );
			scenario.Seed( "m1.tracker", "app.log", ageDays: 0 );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );

			var package = await bed.Operator.GetVfsNodeAsync( "logs.all" );
			await bed.Operator.DownloadAsync( package, timeout: Timeout );

			var entries = Archive.EntriesOf( Archive.In( bed.DownloadFolder ).Single() );

			Assert.IsTrue( entries.Any( e => e.EndsWith( "camera/app.log", StringComparison.OrdinalIgnoreCase ) ),
				$"entries: {string.Join( ", ", entries )}" );
			Assert.IsTrue( entries.Any( e => e.EndsWith( "tracker/app.log", StringComparison.OrdinalIgnoreCase ) ),
				$"entries: {string.Join( ", ", entries )}" );

			// neither file was renamed out of the way of the other
			Assert.IsFalse( entries.Any( e => e.Contains( "app_2", StringComparison.OrdinalIgnoreCase ) ),
				$"the two logs should not have collided, entries: {string.Join( ", ", entries )}" );
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
