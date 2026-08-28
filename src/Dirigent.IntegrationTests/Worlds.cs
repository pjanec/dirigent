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

		static (string Machine, string App) Split( string idTuple )
		{
			var parts = idTuple.Split( '.', 2 );
			return ( parts[0], parts[1] );
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
