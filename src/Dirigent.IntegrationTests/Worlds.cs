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
	/// Worlds more than one test class needs. A scenario is cheap to describe and expensive to
	/// duplicate, so anything two tests share belongs here rather than in either of them.
	/// </summary>
	static class Worlds
	{
		/// <summary>
		/// The seeded logging world, described once in the harness so that tier 2 and tier 3 generate
		/// the very same config from the very same source.
		/// </summary>
		public static Scenario LoggingWorld() => Scenario.LoggingWorld();
		public static readonly string[] LoggingApps = { "m1.camera", "m1.tracker", "m2.recorder" };

		/// <summary>
		/// Starts the logging applications and waits until each has really written its log, so that
		/// what a download picks up is a live file and not only the seeded ones.
		/// </summary>
		public static async Task StartLoggingApps( TestBed.TestBed bed, TimeSpan timeout )
		{
			var apps = LoggingApps.Select( Split ).ToList();

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
				timeout, "all logging applications run and have written their log" );
		}

		/// <summary>
		/// Two applications, each with a log the configuration allows to be cleared and a
		/// configuration file it does not, and one package holding both - the shape the whole
		/// Clear/Mark feature exists for.
		/// </summary>
		/// <remarks>
		/// The applications are defined but never started, and the files are seeded, so that what an
		/// archive holds is exactly what a test put there. A live writer would make the content a
		/// race, and nothing here is about a running application.
		/// </remarks>
		public static Scenario MarkableWorld()
			=> Scenario.TwoMachines()
				.App( "m1.camera", a => a.LongRunning()
						.WithLogNode( clearable: true )
						.WithFileNode( "cfg", "app.cfg", "Config" ) )
				.App( "m2.recorder", a => a.LongRunning()
						.WithLogNode( clearable: true )
						.WithFileNode( "cfg", "app.cfg", "Config" ) )
				.Seed( "m1.camera", "app.log", content: CameraBefore )
				.Seed( "m2.recorder", "app.log", content: RecorderBefore )
				.Seed( "m1.camera", "app.cfg", content: CameraConfig )
				.Seed( "m2.recorder", "app.cfg", content: RecorderConfig )
				.Package( "run.pkg", "Logs/Test run", p => p.RefAll( "log" ).RefAll( "cfg" ) );

		public const string CameraBefore = "camera: yesterday's run, line one\ncamera: line two\n";
		public const string RecorderBefore = "recorder: yesterday's run\n";
		public const string CameraConfig = "camera config - must survive every Clear\n";
		public const string RecorderConfig = "recorder config - must survive every Clear\n";

		/// <summary>The log file of an application of the markable world.</summary>
		public static string LogOf( TestBed.TestBed bed, string machine, string app )
			=> Path.Combine( bed.RenderContext.AppLogsDir( machine, app ), "app.log" );

		/// <summary>Its configuration file, the one nothing may touch.</summary>
		public static string ConfigOf( TestBed.TestBed bed, string machine, string app )
			=> Path.Combine( bed.RenderContext.AppLogsDir( machine, app ), "app.cfg" );

		static (string Machine, string App) Split( string idTuple )
		{
			var parts = idTuple.Split( '.', 2 );
			return ( parts[0], parts[1] );
		}
	}

	/// <summary>
	/// Reading the files an application under test has just produced.
	/// </summary>
	static class Files
	{
		/// <summary>
		/// Reads a file that has only just appeared, retrying while Windows says somebody else has it
		/// open.
		/// </summary>
		/// <remarks>
		/// A file can be briefly unopenable after its writer has finished with it: a virus scanner or
		/// the search indexer gets to the new file first, and the open then fails with a sharing
		/// violation. There is nothing for a test to wait for - the application is done, and the state
		/// the test asked about has arrived - so this is a retry rather than a WaitUntilAsync.
		///
		/// It shows up as a rare failure in whichever test happens to read a file at that moment,
		/// which is the kind of flake that teaches people to re-run the suite instead of reading it.
		/// </remarks>
		public static async Task<string[]> ReadAllLinesAsync( string path, TimeSpan? within = null )
		{
			var deadline = DateTime.UtcNow + ( within ?? TimeSpan.FromSeconds( 5 ) );

			while( true )
			{
				try
				{
					return File.ReadAllLines( path );
				}
				catch( IOException ) when ( DateTime.UtcNow < deadline )
				{
					await Task.Delay( 20 );
				}
			}
		}
	}

	static class Archive
	{
		public static List<string> In( string folder )
			=> Directory.GetFiles( folder, "*.zip" ).OrderBy( x => x ).ToList();

		public static List<string> EntriesOf( string archivePath )
		{
			using var zip = ZipFile.OpenRead( archivePath );
			// forward slashes throughout, which is what the zip format stores anyway
			return zip.Entries.Select( e => e.FullName.Replace( '\\', '/' ) ).ToList();
		}

		/// <summary>An entry whose path contains all the given fragments, in order.</summary>
		public static bool HasEntryMatching( List<string> entries, params string[] fragments )
			=> entries.Any( entry =>
			{
				int at = 0;
				foreach( var fragment in fragments )
				{
					at = entry.IndexOf( fragment, at, StringComparison.OrdinalIgnoreCase );
					if( at < 0 ) return false;
					at += fragment.Length;
				}
				return true;
			} );

		public static string Describe( string folder )
			=> string.Join( ", ", Directory.GetFileSystemEntries( folder ).Select( Path.GetFileName ) );

		/// <summary>Uncompressed size of the first entry whose name ends with the given file name.</summary>
		public static long SizeOf( string archivePath, string entryFileName )
			=> EntryNamed( archivePath, entryFileName, e => e.Length );

		/// <summary>Modification time stored for the first entry whose name ends with the given file name.</summary>
		public static DateTime TimeOf( string archivePath, string entryFileName )
			=> EntryNamed( archivePath, entryFileName, e => e.LastWriteTime.DateTime );

		/// <summary>Text content of the first entry whose name ends with the given file name.</summary>
		public static string TextOf( string archivePath, string entryFileName )
			=> EntryNamed( archivePath, entryFileName, e =>
			{
				using var stream = e.Open();
				using var reader = new StreamReader( stream );
				return reader.ReadToEnd();
			} );

		static T EntryNamed<T>( string archivePath, string entryFileName, Func<ZipArchiveEntry, T> read )
		{
			using var zip = ZipFile.OpenRead( archivePath );
			var entry = zip.Entries.FirstOrDefault(
							e => e.FullName.EndsWith( entryFileName, StringComparison.OrdinalIgnoreCase ) )
						?? throw new AssertFailedException(
							$"no entry ending with '{entryFileName}' in {Path.GetFileName( archivePath )}; "
							+ $"entries: {string.Join( ", ", zip.Entries.Select( e => e.FullName ) )}" );
			return read( entry );
		}
	}
}
