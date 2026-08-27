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

		/// <summary>
		/// Three logging applications on two machines, each exposing its recent logs, plus a package
		/// collecting all of them. Each application's log folder is seeded with a file from
		/// yesterday and one from nine days ago; the node's limit is two days.
		/// </summary>
		static Scenario LoggingWorld()
		{
			var scenario = Scenario.LoggingApps();

			foreach( var app in new[] { "m1.camera", "m1.tracker", "m2.recorder" } )
			{
				scenario.Seed( app, "recent.log", ageDays: 1 );
				scenario.Seed( app, "ancient.log", ageDays: 9 );
			}

			return scenario;
		}

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		[TestMethod()]
		public async Task PackageOfLogsArrivesAsASingleArchive()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = LoggingWorld() } );

			await StartLoggingApps( bed );

			var package = await bed.Operator.GetVfsNodeAsync( "logs.all" );
			await bed.Operator.DownloadAsync( package, timeout: Timeout );

			var archives = ArchivesIn( bed.DownloadFolder );
			Assert.AreEqual( 1, archives.Count,
				$"one archive expected, found: {Describe( bed.DownloadFolder )}" );

			var entries = EntriesOf( archives[0] );

			// the live log of every application, each under its own machine and application folder
			AssertHasEntryMatching( entries, "m1/", "camera/", "app.log" );
			AssertHasEntryMatching( entries, "m1/", "tracker/", "app.log" );
			AssertHasEntryMatching( entries, "m2/", "recorder/", "app.log" );

			// yesterday's file is recent enough to be wanted
			AssertHasEntryMatching( entries, "m1/", "camera/", "recent.log" );

			// nine days old, and the node asks for nothing older than two days
			Assert.IsFalse( entries.Any( e => e.EndsWith( "ancient.log", StringComparison.OrdinalIgnoreCase ) ),
				$"the nine-day-old file should have been filtered out, entries: {string.Join( ", ", entries )}" );

			// the staging folder the machines uploaded their parts to is not left behind
			var leftovers = Directory.GetDirectories( bed.DownloadFolder );
			Assert.AreEqual( 0, leftovers.Length,
				$"no folder should be left in the download folder, found: {string.Join( ", ", leftovers.Select( Path.GetFileName ) )}" );

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
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = LoggingWorld() } );

			await StartLoggingApps( bed );

			var package = await bed.Operator.GetVfsNodeAsync( "logs.all" );
			await bed.Operator.DownloadAsync( package, perMachine: true, timeout: Timeout );

			var archives = ArchivesIn( bed.DownloadFolder );
			Assert.AreEqual( 2, archives.Count,
				$"one archive per machine expected, found: {Describe( bed.DownloadFolder )}" );

			// the machine each archive came from is in its name, and nothing was merged
			foreach( var machine in new[] { "m1", "m2" } )
			{
				Assert.IsTrue(
					archives.Any( a => Path.GetFileNameWithoutExtension( a ).EndsWith( "_" + machine, StringComparison.Ordinal ) ),
					$"an archive from {machine} expected, found: {string.Join( ", ", archives.Select( Path.GetFileName ) )}" );
			}

			// per machine means no machine-name folder inside - the file name already says it
			var fromM2 = archives.Single( a => Path.GetFileNameWithoutExtension( a ).EndsWith( "_m2", StringComparison.Ordinal ) );
			var entries = EntriesOf( fromM2 );
			AssertHasEntryMatching( entries, "recorder/", "app.log" );
			Assert.IsFalse( entries.Any( e => e.Contains( "camera", StringComparison.OrdinalIgnoreCase ) ),
				$"m2's archive should hold m2's files only, entries: {string.Join( ", ", entries )}" );
		}

		[TestMethod()]
		public async Task DownloadOfOneApplicationsLogsTakesOnlyItsOwn()
		{
			// the everyday case: right-click one application, take its recent logs
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = LoggingWorld() } );

			await StartLoggingApps( bed );

			var all = await bed.Operator.GetAllVfsNodesAsync();
			var cameraLogs = all.Single( n => n.Id == "log" && n.AppId == "camera" );

			await bed.Operator.DownloadAsync( cameraLogs, timeout: Timeout );

			var archives = ArchivesIn( bed.DownloadFolder );
			Assert.AreEqual( 1, archives.Count, $"found: {Describe( bed.DownloadFolder )}" );

			var entries = EntriesOf( archives[0] );
			Assert.IsTrue( entries.Any( e => e.EndsWith( "app.log", StringComparison.OrdinalIgnoreCase ) ),
				$"the camera's live log should be there, entries: {string.Join( ", ", entries )}" );
			Assert.IsFalse( entries.Any( e => e.Contains( "tracker", StringComparison.OrdinalIgnoreCase ) ),
				$"only the camera's logs were asked for, entries: {string.Join( ", ", entries )}" );
			Assert.IsFalse( entries.Any( e => e.EndsWith( "ancient.log", StringComparison.OrdinalIgnoreCase ) ),
				$"the nine-day-old file should have been filtered out, entries: {string.Join( ", ", entries )}" );
		}

		// ---- helpers -------------------------------------------------------------------

		/// <summary>
		/// Starts the three logging applications and waits until each has really written its log,
		/// so that what the download picks up is a live file and not just the seeded ones.
		/// </summary>
		static async Task StartLoggingApps( TestBed.TestBed bed )
		{
			var apps = new[] { ( "m1", "camera" ), ( "m1", "tracker" ), ( "m2", "recorder" ) };

			foreach( var (machine, app) in apps )
				await bed.Operator.StartAppAsync( bed.App( machine, app ) );

			await bed.WaitUntilAsync(
				async () =>
				{
					foreach( var (machine, app) in apps )
					{
						var state = await bed.Operator.GetAppStateAsync( bed.App( machine, app ) );
						if( !( state?.Running ?? false ) ) return false;

						var log = Path.Combine( bed.RenderContext.AppLogsDir( machine, app ), "app.log" );
						if( !File.Exists( log ) ) return false;
					}
					return true;
				},
				Timeout, "all three applications run and have written their log" );
		}

		static List<string> ArchivesIn( string folder )
			=> Directory.GetFiles( folder, "*.zip" ).OrderBy( x => x ).ToList();

		static List<string> EntriesOf( string archivePath )
		{
			using var zip = ZipFile.OpenRead( archivePath );
			// forward slashes throughout, which is what the zip format stores anyway
			return zip.Entries.Select( e => e.FullName.Replace( '\\', '/' ) ).ToList();
		}

		/// <summary>An entry whose path contains all the given fragments, in order.</summary>
		static void AssertHasEntryMatching( List<string> entries, params string[] fragments )
		{
			bool Matches( string entry )
			{
				int at = 0;
				foreach( var fragment in fragments )
				{
					at = entry.IndexOf( fragment, at, StringComparison.OrdinalIgnoreCase );
					if( at < 0 ) return false;
					at += fragment.Length;
				}
				return true;
			}

			Assert.IsTrue( entries.Any( Matches ),
				$"no archive entry matching '{string.Join( "..", fragments )}'; entries: {string.Join( ", ", entries )}" );
		}

		static string Describe( string folder )
			=> string.Join( ", ", Directory.GetFileSystemEntries( folder ).Select( Path.GetFileName ) );
	}
}
