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
		/// Three logging applications on two machines, each exposing its recent logs, plus a package
		/// collecting all of them. Every log folder is seeded with a file from yesterday and one from
		/// nine days ago; the node's limit is two days, so only the first is ever wanted.
		/// </summary>
		public static Scenario LoggingWorld()
		{
			var scenario = Scenario.LoggingApps();

			foreach( var app in LoggingApps )
			{
				scenario.Seed( app, "recent.log", ageDays: 1 );
				scenario.Seed( app, "ancient.log", ageDays: 9 );
			}

			return scenario;
		}

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
	}
}
