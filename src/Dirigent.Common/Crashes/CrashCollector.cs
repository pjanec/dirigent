using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Dirigent.Crashes
{
	/// <summary>What the master hands to the slave of a machine: what was asked for and what only the master knows.</summary>
	public class CrashSlaveArgs
	{
		/// <summary>The crashes asked for; null when the request did not ask - the records of the window are then collected all the same.</summary>
		public CrashSelection? Asked;

		/// <summary>The window of the records, resolved (UTC); the one of <see cref="Asked"/> when that is given.</summary>
		public CrashFilter Window = new();

		/// <summary>What the master knows of the processes it launched: exits, running processes, components.</summary>
		public CrashMerger Merger = new();

		/// <summary>The dump folders of the applications (their "dump*" file definitions, resolved), by app id.</summary>
		public Dictionary<string, List<string>>? DumpFolders;
	}

	/// <summary>The crashes of an application in the window, for <c>_sitestate.json</c>.</summary>
	public class CrashSummaryItem
	{
		public string App = string.Empty;
		public int Count;
		public DateTime Last;
		public string LastKind = string.Empty;
	}

	/// <summary>What a slave reports of its crashes to the script that started it.</summary>
	public class CrashSlaveReport
	{
		public int Count;
		public List<CrashSummaryItem> PerApp = new();

		/// <summary>For the cover note: one line per crash folder - what is in it and what is missing, and why.</summary>
		public List<string> Lines = new();

		public long DumpBytes;
		public string? Problem;
	}

	/// <summary>One crash folder of the archive, prepared: small files as text, the dump and the report to be copied.</summary>
	public class CrashFolder
	{
		public string Name = string.Empty;
		public CrashEntry Entry = new();
		public string? DumpPath;
		public long DumpSize;
		public string? ReportPath;
		public SortedDictionary<string, string> Texts = new( StringComparer.Ordinal );
		public List<string> Missing = new();
	}

	/// <summary>The crash part of one machine's archive, ready to be written.</summary>
	public class CrashPlan
	{
		public string RecordsJson = "{}";
		public List<CrashFolder> Folders = new();
		public CrashSlaveReport Report = new();
	}

	/// <summary>
	/// Collecting crashes into an archive: the records of the window, and per crash its folder with the dump and the context.
	/// Looks the crashes up again on the agent with the filters of the request - the request names crashes, never paths.
	/// </summary>
	public static class CrashCollector
	{
		public const string RecordsEntry = "_crashes.json";
		public const int MaxRecords = 500;

		// ----- the rules, each in one place --------------------------------------------------------------

		/// <summary>Whether the name of a variable says its value is a secret: it contains SECRET, TOKEN, PASSWORD or KEY, or ends with _PAT; any case.</summary>
		public static bool IsSecretName( string name )
		{
			var n = name.ToUpperInvariant();
			return n.Contains( "SECRET" ) || n.Contains( "TOKEN" ) || n.Contains( "PASSWORD" ) || n.Contains( "KEY" ) || n.EndsWith( "_PAT" );
		}

		public const string Masked = "***";

		/// <summary>The variables with the values of the secret ones replaced by "***".</summary>
		public static SortedDictionary<string, string> MaskSecrets( IEnumerable<KeyValuePair<string, string>> vars )
		{
			var res = new SortedDictionary<string, string>( StringComparer.OrdinalIgnoreCase );
			foreach( var kv in vars ) res[kv.Key] = IsSecretName( kv.Key ) ? Masked : kv.Value;
			return res;
		}

		/// <summary>"crashes/2026-11-21_14-03-11_m1.viewer": the local time of the crash and the app, else the exe, else the kind. Unique among <paramref name="used"/>.</summary>
		public static string FolderName( CrashEntry e, ISet<string> used )
		{
			var who = e.app
					?? ( e.exe is null ? null : Path.GetFileNameWithoutExtension( e.exe.Replace( '/', '\\' ).Substring( e.exe.Replace( '/', '\\' ).LastIndexOf( '\\' ) + 1 ) ) )
					?? e.kind;
			var baseName = $"crashes/{e.time.ToLocalTime():yyyy-MM-dd_HH-mm-ss}_{Tools.SanitizeFileName( who )}";

			var name = baseName;
			for( int i = 2; !used.Add( name ); i++ ) name = $"{baseName}_{i}";
			return name;
		}

		static readonly JsonSerializerOptions _json = new()
		{
			WriteIndented = true,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
		};

		static string ToJson( object o ) => JsonSerializer.Serialize( o, o.GetType(), _json );

		static string Size( long bytes )
			=> bytes >= 1L << 30 ? $"{bytes / (double) ( 1L << 30 ):0.#} GB" : bytes >= 1L << 20 ? $"{bytes / (double) ( 1L << 20 ):0.#} MB" : $"{bytes / 1024.0:0.#} kB";

		// ----- the plan ------------------------------------------------------------------------------------

		/// <summary>Looks the crashes up and prepares everything but the copying of the big files.</summary>
		public static async Task<CrashPlan> PlanAsync( IDirigAsync dirig, CrashSlaveArgs args, CrashEnvironment env, DateTime nowUtc )
		{
			var plan = new CrashPlan();
			var machine = dirig.MachineId;
			var window = args.Window.Resolve( nowUtc );

			if( !env.IsSupported )
			{
				plan.RecordsJson = ToJson( new { machine, status = "not supported", window = new { since = window.since, until = window.until }, crashes = Array.Empty<CrashEntry>() } );
				plan.Report.Problem = "no event log here: no crash records";
				return plan;
			}

			var gathered = await CrashGatherer.ReadAsync( dirig, env, window.since!.Value, window.until, true, args.Asked is null ? null : args.DumpFolders );

			var all = gathered.Entries.ToList();
			foreach( var e in all ) args.Merger.Enrich( e );
			all.AddRange( args.Merger.ExitEntries( all, m => string.Equals( m, machine, StringComparison.OrdinalIgnoreCase ) ).Where( e => window.Matches( e ) ) );
			all = all.OrderByDescending( e => e.time ).ThenBy( e => e.id, StringComparer.Ordinal ).ToList();

			// what the site state says per app: everything of the window
			plan.Report.PerApp = all.Where( e => e.app is not null ).GroupBy( e => e.app!, StringComparer.OrdinalIgnoreCase )
				.Select( g => { var last = g.OrderByDescending( e => e.time ).First(); return new CrashSummaryItem { App = g.Key, Count = g.Count(), Last = last.time, LastKind = last.kind }; } ).ToList();

			var filter = args.Asked is null ? window : args.Asked.Resolve( nowUtc );
			var records = CrashFilter.NewestPerApp( all.Where( e => filter.Matches( e ) ), args.Asked?.newestPerApp );
			var cut = gathered.Cut;
			if( records.Count > MaxRecords ) { records = records.Take( MaxRecords ).ToList(); cut = true; }
			plan.Report.Count = records.Count;

			var notes = new List<string>();

			if( args.Asked is { } asked && ( asked.dumps || asked.context ) )
				await PlanFolders( dirig, env, args, asked, gathered, records, plan, notes );

			plan.RecordsJson = ToJson( new
			{
				machine,
				status = "ok",
				window = new { since = window.since, until = window.until },
				oldestEvent = gathered.OldestEvent,
				cut,
				notes = notes.Count == 0 ? null : notes,
				crashes = records,
			} );

			return plan;
		}

		static async Task PlanFolders( IDirigAsync dirig, CrashEnvironment env, CrashSlaveArgs args, CrashSelection asked, CrashGathered gathered,
				List<CrashEntry> records, CrashPlan plan, List<string> notes )
		{
			var used = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
			var machineJson = asked.context ? ToJson( env.ReadMachineInfo() ) : null;

			// the errors and warnings around the crashes: read once for all of them
			Dictionary<string, List<RawEvent>>? around = null;
			if( asked.context && records.Count > 0 )
				around = ReadAround( env, records.Min( e => e.time ).AddMinutes( -2 ), records.Max( e => e.time ) );

			// one file is one dump: it goes into the archive, and into the totals, once
			var dumpTakenBy = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );

			var specs = new Dictionary<string, AppLaunchSpec?>( StringComparer.OrdinalIgnoreCase );
			var versions = new Dictionary<string, CrashVersions?>( StringComparer.OrdinalIgnoreCase );

			foreach( var e in records )
			{
				var folder = new CrashFolder { Name = FolderName( e, used ), Entry = e };
				var parts = new List<string>();

				if( asked.dumps )
				{
					var (path, missing, tooBig) = ChooseDump( e, asked );
					if( missing is not null ) folder.Missing.Add( missing );
					if( tooBig ) notes.Add( $"{e.id}: {missing}" );
					if( path is not null && dumpTakenBy.TryGetValue( path, out var firstFolder ) )
					{
						folder.Missing.Add( $"dump {path} is the one of {firstFolder}: it is in the archive once" );
						path = null;
					}
					if( path is not null )
					{
						dumpTakenBy[path] = folder.Name;
						folder.DumpPath = path;
						folder.DumpSize = e.dump!.size ?? 0;
						plan.Report.DumpBytes += folder.DumpSize;
						parts.Add( $"dump {Size( folder.DumpSize )}" );
					}
				}

				if( asked.context )
				{
					await AddContext( dirig, env, args, gathered, e, folder, around, machineJson!, specs, versions );
					parts.AddRange( folder.Texts.Keys );
					if( folder.ReportPath is not null ) parts.Add( "Report.wer" );
				}

				plan.Folders.Add( folder );
				var line = $"{folder.Name}: " + ( parts.Count == 0 ? "nothing" : string.Join( ", ", parts ) );
				if( folder.Missing.Count > 0 ) line += " - missing: " + string.Join( "; ", folder.Missing );
				plan.Report.Lines.Add( line );
			}
		}

		/// <summary>
		/// Whether the dump of the entry goes into the archive: the path when it does, else why not. Dumps are never cut - a dump bigger than
		/// the limit is left out, and <c>tooBig</c> says the archive has to tell.
		/// </summary>
		public static (string? path, string? missing, bool tooBig) ChooseDump( CrashEntry e, CrashSelection asked )
		{
			if( e.dump is null )
				return (null, "no dump: Windows recorded none and none is in the application's dump folders", false);
			if( !e.dump.exists )
				return (null, e.dump.note is null ? $"no dump: {e.dump.path} is not there" : $"no dump: {e.dump.path} - {e.dump.note}", false);
			if( asked.maxDumpBytes is long max && ( e.dump.size ?? 0 ) > max )
				return (null, $"dump {e.dump.path} left out: {Size( e.dump.size ?? 0 )} is more than the limit of {Size( max )}", true);
			return (e.dump.path, null, false);
		}

		static async Task AddContext( IDirigAsync dirig, CrashEnvironment env, CrashSlaveArgs args, CrashGathered gathered, CrashEntry e, CrashFolder folder,
				Dictionary<string, List<RawEvent>>? around, string machineJson, Dictionary<string, AppLaunchSpec?> specs, Dictionary<string, CrashVersions?> versions )
		{
			// the entry, with what Dirigent knew of the process
			AppDef? def = e.app is not null && gathered.Defs.TryGetValue( e.app, out var d ) ? d : null;
			AppExit? exit = e.app is not null && args.Merger.Exits.TryGetValue( e.app, out var exits ) ? exits.FirstOrDefault( x => x.pid == e.processId && e.processId is not null ) : null;
			RunningProcess? running = e.app is not null && args.Merger.Running.TryGetValue( e.app, out var run ) && run.Pid == e.processId ? run : null;

			folder.Texts["crash.json"] = ToJson( new
			{
				entry = e,
				dirigent = new
				{
					startedAt = e.startedAt,
					uptimeSeconds = e.uptimeSeconds,
					plan = e.plan,
					exitCode = exit?.exitCode,
					restartsRemaining = exit?.restartsRemaining ?? running?.RestartsRemaining,
					restartOnCrash = def?.RestartOnCrash,
				},
			} );

			// versions: the build that crashed
			if( e.exe is not null && e.exe.Contains( '\\' ) )
			{
				if( !versions.TryGetValue( e.exe, out var v ) ) versions[e.exe] = v = env.ReadVersionsWithHashes( e.exe );
				if( v is not null )
				{
					var loc = new Scripts.BuiltIn.ResolveAppLocations.TPath { LocalPath = e.exe };
					Scripts.BuiltIn.ResolveAppLocations.FillFileFacts( loc );
					folder.Texts["versions.json"] = ToJson( new
					{
						dirigent = AppConfig.VersionText(),
						app = e.app is null ? null : new { id = e.app, version = loc.FileVersion?.Split( '\n' ).Select( x => x.TrimEnd( '\r' ) ).ToList(), source = loc.VersionSource },
						exe = MarkChanged( CrashQueryHelpers.FaultingFirst( v, e.faultingModule ), e.time, DateTime.UtcNow ),
					} );
				}
				else folder.Missing.Add( "no versions.json: the program is not there any more" );
			}
			else folder.Missing.Add( "no versions.json: no program path" );

			// launch: how it was started, only what the shared config sets
			if( def is not null )
			{
				if( !specs.TryGetValue( e.app!, out var spec ) ) specs[e.app!] = spec = await SafeSpec( dirig, def );
				var pe = spec?.ParseExe();
				folder.Texts["launch.json"] = ToJson( new
				{
					asOf = "collection",
					collectedAt = DateTime.UtcNow,
					note = "This is the configuration as it is now, at the collection - not necessarily as it was at the crash.",
					app = e.app,
					plan = e.plan ?? def.PlanName,
					exe = new { declared = def.ExeFullPath, expanded = spec is null || pe is null ? null : spec.GetFileName( pe.Value ) },
					startDir = new { declared = def.StartupDir, expanded = spec?.GetWorkingDirectory() },
					cmdLine = new { declared = def.CmdLineArgs, expanded = pe?.CmdLine },
					vars = spec is null ? null : MaskSecrets( spec.PublishedVars ),
					env = MaskSecrets( def.EnvVarsToSet ),
					locals = MaskSecrets( def.LocalVarsToSet ),
				} );
			}
			else folder.Missing.Add( e.app is null ? "no launch.json: the application is not known" : "no launch.json: the application has no definition here" );

			folder.Texts["events.txt"] = EventsText( gathered, e, around );
			folder.Texts["machine.json"] = machineJson;

			if( e.werReport is not null && File.Exists( e.werReport ) ) folder.ReportPath = e.werReport;
			else if( e.kind is CrashKind.Crash or CrashKind.Managed or CrashKind.Hang ) folder.Missing.Add( "no Report.wer: Windows Error Reporting wrote none" );
		}

		/// <summary>
		/// The versions as they are at the collection, each file flagged when it was written after the crash - such a file is not what
		/// crashed - and a note saying how many.
		/// </summary>
		public static CrashVersions MarkChanged( CrashVersions v, DateTime crashUtc, DateTime nowUtc )
		{
			var files = v.files.Select( f => new CrashFileVersion
			{
				name = f.name, version = f.version, size = f.size, modified = f.modified, sha256 = f.sha256,
				modifiedAfterCrash = f.modified.ToUniversalTime() > crashUtc ? true : null,
			} ).ToList();

			var changed = files.Count( f => f.modifiedAfterCrash == true );
			return new CrashVersions
			{
				exeFileVersion = v.exeFileVersion,
				exeProductVersion = v.exeProductVersion,
				files = files,
				collectedAt = nowUtc,
				note = $"{changed} file{( changed == 1 ? "" : "s" )} changed after the crash. The files are described as they are at the collection, not at the crash.",
			};
		}

		static async Task<AppLaunchSpec?> SafeSpec( IDirigAsync dirig, AppDef def )
		{
			try { return await dirig.ResolveAppLaunchSpecAsync( def ); }
			catch( Exception ) { return null; }
		}

		/// <summary>The errors and warnings of the Application and System logs in the window, by log.</summary>
		static Dictionary<string, List<RawEvent>> ReadAround( CrashEnvironment env, DateTime fromUtc, DateTime toUtc )
		{
			var res = new Dictionary<string, List<RawEvent>>();
			foreach( var log in new[] { "Application", "System" } )
			{
				try
				{
					res[log] = env.Source!.Read( new CrashEventQuery { Log = log, FromUtc = fromUtc, ToUtc = toUtc.AddSeconds( 1 ), MaxLevel = 3 } ).Events;
				}
				catch( Exception )
				{
					res[log] = new();
				}
			}
			return res;
		}

		static void AppendEvent( StringBuilder sb, RawEvent ev )
		{
			sb.AppendLine( $"{ev.TimeUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss} (UTC {ev.TimeUtc:HH:mm:ss}) [{ev.Log}] {ev.Provider} {ev.Id} level {ev.Level}" );
			foreach( var d in ev.Data.Where( x => !string.IsNullOrWhiteSpace( x ) ) )
				foreach( var line in d.Replace( "\r\n", "\n" ).Split( '\n' ).Take( 12 ) )
					sb.AppendLine( "    " + ( line.Length > 400 ? line.Substring( 0, 400 ) + "..." : line ) );
		}

		static string EventsText( CrashGathered gathered, CrashEntry e, Dictionary<string, List<RawEvent>>? around )
		{
			var sb = new StringBuilder();
			sb.AppendLine( "The events of this crash:" );
			if( gathered.Matched.TryGetValue( e.id, out var matched ) && matched.Count > 0 )
				foreach( var ev in matched.OrderBy( x => x.TimeUtc ) ) AppendEvent( sb, ev );
			else
				sb.AppendLine( "    (none: Windows wrote no event for it)" );

			sb.AppendLine();
			sb.AppendLine( "Errors and warnings of the Application and System logs in the 2 minutes before it:" );
			var count = 0;
			foreach( var ev in ( around?.Values.SelectMany( x => x ) ?? Enumerable.Empty<RawEvent>() )
								.Where( x => x.TimeUtc >= e.time.AddMinutes( -2 ) && x.TimeUtc <= e.time )
								.Where( x => matched is null || !matched.Any( m => m.TimeUtc == x.TimeUtc && m.Id == x.Id && m.Provider == x.Provider ) )
								.OrderBy( x => x.TimeUtc ) )
			{
				AppendEvent( sb, ev );
				count++;
			}
			if( count == 0 ) sb.AppendLine( "    (none)" );
			return sb.ToString();
		}

		// ----- the archive -------------------------------------------------------------------------------

		/// <summary>The bytes the plan will add - for the progress.</summary>
		public static long BytesOf( CrashPlan plan )
			=> plan.RecordsJson.Length + plan.Folders.Sum( f => f.DumpSize + f.Texts.Values.Sum( t => (long) t.Length ) );

		/// <summary>Writes the crash part into the archive; returns how many files went in.</summary>
		/// <param name="unique">makes an entry name unique in the archive</param>
		/// <param name="progress">told the bytes of each file as it is copied</param>
		public static int Write( ZipArchive zip, CrashPlan plan, Func<string, string> unique, Action<long> progress, System.Threading.CancellationToken cancel )
		{
			var files = 0;

			void Text( string name, string text )
			{
				var entry = zip.CreateEntry( unique( name ), CompressionLevel.Fastest );
				using var stream = entry.Open();
				using var writer = new StreamWriter( stream, new UTF8Encoding( false ) );
				writer.Write( text );
				files++;
			}

			void Copy( string name, string path )
			{
				using var source = new FileStream( path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 256 * 1024 );
				var entry = zip.CreateEntry( unique( name ), CompressionLevel.Fastest );
				using var stream = entry.Open();
				var buffer = new byte[256 * 1024];
				int n;
				while( ( n = source.Read( buffer, 0, buffer.Length ) ) > 0 )
				{
					cancel.ThrowIfCancellationRequested();
					stream.Write( buffer, 0, n );
					progress( n );
				}
				files++;
			}

			Text( RecordsEntry, plan.RecordsJson );

			foreach( var f in plan.Folders )
			{
				foreach( var (name, text) in f.Texts ) Text( $"{f.Name}/{name}", text );

				if( f.ReportPath is not null )
				{
					try { Copy( $"{f.Name}/Report.wer", f.ReportPath ); }
					catch( Exception e ) when( e is IOException or UnauthorizedAccessException ) { f.Missing.Add( $"no Report.wer: {e.Message}" ); }
				}

				if( f.DumpPath is not null )
					Copy( $"{f.Name}/{Path.GetFileName( f.DumpPath )}", f.DumpPath );
			}

			return files;
		}
	}

	/// <summary>Small helpers shared with the master's listing.</summary>
	public static class CrashQueryHelpers
	{
		/// <summary>A copy of the versions with the faulting module's file first.</summary>
		public static CrashVersions FaultingFirst( CrashVersions v, string? module )
		{
			var files = v.files.ToList();
			var first = module is null ? null : files.FirstOrDefault( f => string.Equals( f.name, module, StringComparison.OrdinalIgnoreCase ) );
			if( first is not null )
			{
				files.Remove( first );
				files.Insert( 0, first );
			}
			return new CrashVersions { exeFileVersion = v.exeFileVersion, exeProductVersion = v.exeProductVersion, files = files };
		}
	}

	/// <summary>What the master hands to the script that collects: the asked crashes (or null) and what only the master knows.</summary>
	public class CrashDownloadArgs
	{
		public CrashSelection? Asked;

		/// <summary>The window of the records of every machine, resolved (UTC).</summary>
		public CrashFilter Window = new();

		public CrashMerger Merger = new();
	}

	/// <summary>The dump folders of an application: its file definitions whose id starts with "dump" - the documented prefix convention.</summary>
	public static class CrashDumpFolders
	{
		public const string Prefix = "dump";

		static IEnumerable<VfsNodeDef> DumpNodes( IEnumerable<VfsNodeDef> nodes )
		{
			foreach( var n in nodes )
			{
				// a folder definition is a container too - it counts by its id; a virtual folder is looked into
				if( n.Id is not null && n.Id.StartsWith( Prefix, StringComparison.OrdinalIgnoreCase ) ) yield return n;
				else if( n.IsContainer ) { foreach( var c in DumpNodes( n.Children ) ) yield return c; }
			}
		}

		/// <summary>The folder a resolved node stands for: its path, or the folder of a path with a file name or a mask.</summary>
		public static string? FolderOf( string? path )
		{
			if( string.IsNullOrWhiteSpace( path ) ) return null;
			path = path.Trim();
			if( path.IndexOfAny( new[] { '*', '?' } ) >= 0 || path.EndsWith( ".dmp", StringComparison.OrdinalIgnoreCase ) )
				return Path.GetDirectoryName( path );
			return path;
		}

		/// <summary>The dump folders per app ("m1.a") of the definitions, resolved by <paramref name="resolve"/> (which knows the machine of the node).</summary>
		public static async Task<Dictionary<string, List<string>>> ResolveAsync( IEnumerable<AppDef> defs, Func<VfsNodeDef, Task<VfsNodeDef?>> resolve )
		{
			var jobs = new List<(string app, Task<VfsNodeDef?> task)>();
			foreach( var d in defs )
				foreach( var n in DumpNodes( d.VfsNodes ) )
				{
					Task<VfsNodeDef?> t;
					try { t = resolve( n ); }
					catch( Exception ) { continue; }
					jobs.Add( (d.Id.ToString(), t) );
				}

			var res = new Dictionary<string, List<string>>( StringComparer.OrdinalIgnoreCase );
			foreach( var (app, task) in jobs )
			{
				try
				{
					var folder = FolderOf( ( await task )?.Path );
					if( folder is null ) continue;
					if( !res.TryGetValue( app, out var list ) ) res[app] = list = new();
					if( !list.Contains( folder, StringComparer.OrdinalIgnoreCase ) ) list.Add( folder );
				}
				catch( Exception )
				{
					// a definition that cannot be resolved has no folder to offer
				}
			}
			return res;
		}
	}
}
