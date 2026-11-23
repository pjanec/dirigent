using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Dirigent.Crashes
{
	/// <summary>What the builder needs from the machine besides the events; a test supplies its own.</summary>
	public class CrashContext
	{
		public string Machine = string.Empty;
		public CrashAppTable Table = new();

		/// <summary>The size of a file, or null when there is no such file.</summary>
		public Func<string, long?> FileSize = _ => null;

		/// <summary>The folders a LocalDumps setting names for the exe (global and its own) - empty when none is set.</summary>
		public Func<string, IReadOnlyList<string>> LocalDumpFolders = _ => Array.Empty<string>();

		/// <summary>The path of the first existing minidump written around a time, or null.</summary>
		public Func<DateTime, string?> FindMiniDump = _ => null;

		/// <summary>The dump folders of an application (its file definitions whose id starts with "dump"), resolved; empty for none.</summary>
		public Func<string, IReadOnlyList<string>> AppDumpFolders = _ => Array.Empty<string>();

		/// <summary>The dump files of a folder: path, time written (UTC), size.</summary>
		public Func<string, IEnumerable<(string path, DateTime writtenUtc, long size)>> ListDumps = _ => Array.Empty<(string, DateTime, long)>();

		/// <summary>The report of a crash found by what it says (program, event time): exe name, exe path, crash time (UTC). Null when none.</summary>
		public Func<string?, string?, DateTime, string?> FindWerReport = ( _, _, _ ) => null;

		/// <summary>The FileVersion string of a file, or null when it has none or is not there.</summary>
		public Func<string, string?> FileVersion = _ => null;

		/// <summary>When a file was last written (UTC), or null when there is no such file.</summary>
		public Func<string, DateTime?> FileWritten = _ => null;

		/// <summary>The Windows folder - where MEMORY.DMP is.</summary>
		public string WindowsDir = @"C:\Windows";
	}

	/// <summary>
	/// The events of one machine to crash entries: the 1000, 1001 and 1026 of one crash into one entry, the programs
	/// that are not the site's left out, the machine-level events kept. Pure - no log, no disk (see <see cref="CrashContext"/>).
	/// </summary>
	public static class CrashBuilder
	{
		public const string ProviderAppError = "Application Error";
		public const string ProviderWer = "Windows Error Reporting";
		public const string ProviderDotNet = ".NET Runtime";
		public const string ProviderHang = "Application Hang";
		public const string ProviderKernelPower = "Microsoft-Windows-Kernel-Power";
		public const string ProviderEventLog = "EventLog";
		public const string ProviderBugCheck = "Microsoft-Windows-WER-SystemErrorReporting";
		public const string ProviderBugCheckShort = "BugCheck";
		public const string ProviderDisplay = "Display";

		/// <summary>Events of one crash lie within this of each other (1000 and 1026).</summary>
		public static readonly TimeSpan CorrelationWindow = TimeSpan.FromSeconds( 15 );

		/// <summary>The report of Windows Error Reporting follows its crash by up to this.</summary>
		public static readonly TimeSpan WerDelay = TimeSpan.FromMinutes( 10 );

		/// <summary>Two reboot events (41, 6008) of one reboot lie within this.</summary>
		public static readonly TimeSpan RebootWindow = TimeSpan.FromMinutes( 10 );

		/// <summary>The exception code of an unhandled exception of the CLR.</summary>
		public const string ClrExceptionCode = "0xe0434352";

		/// <summary>A dump with no name of the crash is the one written within this after it.</summary>
		public static readonly TimeSpan DumpAfterCrash = TimeSpan.FromMinutes( 1 );

		/// <summary>A bugcheck's dump was written within this of the bugcheck event.</summary>
		public static readonly TimeSpan BugCheckDumpWindow = TimeSpan.FromMinutes( 5 );

		public const int MaxExceptionLines = 5;
		const int _maxLineChars = 300;

		/// <summary>The providers and event ids to read, per log - what <see cref="Build"/> understands.</summary>
		public static readonly List<(string provider, int id)> ApplicationWanted = new()
		{
			( ProviderAppError, 1000 ), ( ProviderWer, 1001 ), ( ProviderDotNet, 1026 ), ( ProviderHang, 1002 ),
		};

		public static readonly List<(string provider, int id)> SystemWanted = new()
		{
			( ProviderKernelPower, 41 ), ( ProviderEventLog, 6008 ), ( ProviderBugCheck, 1001 ), ( ProviderBugCheckShort, 1001 ), ( ProviderDisplay, 4101 ),
		};

		static bool Is( RawEvent e, string provider, int id ) => e.Id == id && string.Equals( e.Provider, provider, StringComparison.OrdinalIgnoreCase );

		// ----- small parsers ------------------------------------------------------------------------------

		/// <summary>"0x1f4c" or "1f4c" (hex, as Application Error writes it) or plain decimal; null when it is nothing.</summary>
		public static int? ParsePid( string? s )
		{
			if( string.IsNullOrWhiteSpace( s ) ) return null;
			s = s.Trim();
			if( s.StartsWith( "0x", StringComparison.OrdinalIgnoreCase ) )
				return int.TryParse( s.Substring( 2 ), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var h ) ? h : null;
			if( Regex.IsMatch( s, "^[0-9]+$" ) && !Regex.IsMatch( s, "[a-fA-F]" ) )
				return int.TryParse( s, NumberStyles.None, CultureInfo.InvariantCulture, out var d ) ? d : null;
			return int.TryParse( s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var x ) ? x : null;
		}

		/// <summary>"c0000005" to "0xc0000005"; leaves what is not hex alone.</summary>
		public static string? NormalizeCode( string? s )
		{
			if( string.IsNullOrWhiteSpace( s ) ) return null;
			s = s.Trim();
			var hex = s.StartsWith( "0x", StringComparison.OrdinalIgnoreCase ) ? s.Substring( 2 ) : s;
			return Regex.IsMatch( hex, "^[0-9a-fA-F]+$" ) ? "0x" + hex.ToLowerInvariant() : s;
		}

		/// <summary>"0000000000012345" to "0x12345".</summary>
		public static string? TrimmedHex( string? s )
		{
			var n = NormalizeCode( s );
			if( n is null || !n.StartsWith( "0x" ) ) return n;
			var trimmed = n.Substring( 2 ).TrimStart( '0' );
			return "0x" + ( trimmed.Length == 0 ? "0" : trimmed );
		}

		static string? Clean( string? s ) => string.IsNullOrWhiteSpace( s ) ? null : s.Trim().Trim( '\0' );

		static string? ExeNameOf( string? path )
		{
			var p = CrashAppTable.NormalizePath( path );
			return p is null ? null : p.Substring( p.LastIndexOfAny( new[] { '\\', '/' } ) + 1 );
		}

		static string NormGuid( string? s ) => ( s ?? "" ).Trim().Trim( '{', '}' ).ToLowerInvariant();

		/// <summary>The unhandled exception of a .NET Runtime 1026 text: "Application: x.exe ... Exception Info: type ... at ...". Cut.</summary>
		public static (string? exe, string? text) ParseDotNetText( string? text )
		{
			if( string.IsNullOrWhiteSpace( text ) ) return (null, null);

			var lines = text.Replace( "\r\n", "\n" ).Split( '\n' );
			string? exe = null;
			var info = new List<string>();
			var inInfo = false;

			foreach( var raw in lines )
			{
				var line = raw.TrimEnd();
				if( !inInfo )
				{
					if( line.StartsWith( "Application:", StringComparison.OrdinalIgnoreCase ) )
						exe = Clean( line.Substring( "Application:".Length ) );
					else if( line.StartsWith( "Exception Info:", StringComparison.OrdinalIgnoreCase ) )
					{
						inInfo = true;
						var first = line.Substring( "Exception Info:".Length ).Trim();
						if( first.Length > 0 ) info.Add( first );
					}
				}
				else if( info.Count < MaxExceptionLines && line.Trim().Length > 0 )
				{
					info.Add( line.Trim() );
				}
			}

			var cut = info.Take( MaxExceptionLines ).Select( l => l.Length > _maxLineChars ? l.Substring( 0, _maxLineChars ) + "..." : l );
			var joined = string.Join( "\n", cut );
			return (exe, joined.Length == 0 ? null : joined);
		}

		static IEnumerable<string> AttachedFiles( RawEvent wer )
			=> wer.D( 14 ).Split( new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries )
				.Select( x => CrashAppTable.NormalizePath( x ) ).Where( x => x is not null ).Select( x => x! );

		// ----- the build --------------------------------------------------------------------------------

		class Crash
		{
			public RawEvent? E1000;
			public RawEvent? E1026;
			public RawEvent? Hang;
			public RawEvent? Wer;
		}

		/// <summary>All the entries the events make, newest first; only the site's programs and the machine-level ones.</summary>
		public static List<CrashEntry> Build( IEnumerable<RawEvent> events, CrashContext ctx )
			=> Build( events, ctx, null );

		/// <param name="matched">when given, receives the events of each entry (by its id): the 1000, the 1001, the 1026, ...</param>
		public static List<CrashEntry> Build( IEnumerable<RawEvent> events, CrashContext ctx, Dictionary<string, List<RawEvent>>? matched )
		{
			var all = events.OrderBy( e => e.TimeUtc ).ToList();
			var res = new List<CrashEntry>();

			var crashes = all.Where( e => Is( e, ProviderAppError, 1000 ) ).Select( e => new Crash { E1000 = e } ).ToList();
			var dotnet = all.Where( e => Is( e, ProviderDotNet, 1026 ) ).ToList();
			var hangs = all.Where( e => Is( e, ProviderHang, 1002 ) ).Select( e => new Crash { Hang = e } ).ToList();
			var wers = all.Where( e => Is( e, ProviderWer, 1001 ) ).ToList();

			// 1026 into the 1000 of the same exe, the nearest in time
			var standalone = new List<Crash>();
			foreach( var ev in dotnet )
			{
				var (exe, text) = ParseDotNetText( ev.D( 0 ) );
				var name = ExeNameOf( exe );

				// The 1026 has no pid, so two crashes of one exe at once are told apart by what they are: an exception the CLR
				// raised is a 1000 with its own exception code, a fatal access violation is a 1000 with the Windows one.
				var accessViolation = text is not null && text.Contains( "AccessViolationException", StringComparison.Ordinal );
				bool Fits( Crash c ) => ( NormalizeCode( c.E1000!.D( 6 ) ) == ClrExceptionCode ) != accessViolation;

				var best = crashes.Where( c => c.E1026 is null && name is not null
								&& string.Equals( ExeNameOf( c.E1000!.D( 0 ) ), name, StringComparison.OrdinalIgnoreCase )
								&& ( c.E1000.TimeUtc - ev.TimeUtc ).Duration() <= CorrelationWindow )
							.OrderBy( c => Fits( c ) ? 0 : 1 )
							.ThenBy( c => ( c.E1000!.TimeUtc - ev.TimeUtc ).Duration() ).FirstOrDefault();
				if( best is not null ) best.E1026 = ev;
				else standalone.Add( new Crash { E1026 = ev } );
			}

			// 1001 into the crash of its report id, or of the same exe, code and offset shortly before it
			var freeWers = new List<RawEvent>( wers );
			foreach( var c in crashes.Concat( hangs ).OrderBy( c => ( c.E1000 ?? c.Hang )!.TimeUtc ) )
			{
				var ev = ( c.E1000 ?? c.Hang )!;
				var reportId = NormGuid( c.E1000 is not null ? ev.D( 12 ) : ev.D( 6 ) );
				var name = ExeNameOf( ev.D( 0 ) );

				RawEvent? match = reportId.Length > 0 ? freeWers.FirstOrDefault( w => NormGuid( w.D( 18 ) ) == reportId ) : null;
				match ??= freeWers.Where( w => w.TimeUtc >= ev.TimeUtc.AddSeconds( -5 ) && w.TimeUtc - ev.TimeUtc <= WerDelay
								&& string.Equals( ExeNameOf( w.D( 4 ) ), name, StringComparison.OrdinalIgnoreCase )
								&& ( c.E1000 is null
									? w.D( 1 ).Contains( "HANG", StringComparison.OrdinalIgnoreCase )
									: TrimmedHex( w.D( 10 ) ) == TrimmedHex( ev.D( 6 ) ) && TrimmedHex( w.D( 11 ) ) == TrimmedHex( ev.D( 7 ) ) ) )
							.OrderBy( w => w.TimeUtc ).FirstOrDefault();

				if( match is not null )
				{
					c.Wer = match;
					freeWers.Remove( match );
				}
			}

			foreach( var c in crashes.Concat( standalone ).Concat( hangs ) )
			{
				var entry = MakeAppEntry( c, ctx );
				if( entry is null ) continue;
				res.Add( entry );
				matched?.TryAdd( entry.id, new[] { c.E1000, c.E1026, c.Hang, c.Wer }.Where( x => x is not null ).Select( x => x! ).ToList() );
			}

			res.AddRange( MakeReboots( all, ctx ) );

			foreach( var ev in all )
			{
				CrashEntry? machineLevel = null;
				if( ( Is( ev, ProviderBugCheck, 1001 ) || Is( ev, ProviderBugCheckShort, 1001 ) ) )
					machineLevel = MakeBugCheck( ev, ctx );
				else if( Is( ev, ProviderDisplay, 4101 ) )
					machineLevel = MakeGpuReset( ev, ctx );

				if( machineLevel is null ) continue;
				res.Add( machineLevel );
				matched?.TryAdd( machineLevel.id, new() { ev } );
			}

			return res.OrderByDescending( e => e.time ).ThenBy( e => e.id, StringComparer.Ordinal ).ToList();
		}

		static CrashEntry? MakeAppEntry( Crash c, CrashContext ctx )
		{
			var e = new CrashEntry { machine = ctx.Machine };
			string? path, name;

			if( c.E1000 is not null )
			{
				var ev = c.E1000;
				e.kind = c.E1026 is not null ? CrashKind.Managed : CrashKind.Crash;
				e.time = ev.TimeUtc;
				name = ExeNameOf( ev.D( 0 ) );
				path = CrashAppTable.NormalizePath( ev.D( 10 ) );
				e.exeVersion = Clean( ev.D( 1 ) );
				e.faultingModule = Clean( ev.D( 3 ) );
				e.faultingModuleVersion = Clean( ev.D( 4 ) );
				e.faultingModulePath = CrashAppTable.NormalizePath( ev.D( 11 ) );
				e.exceptionCode = NormalizeCode( ev.D( 6 ) );
				var offset = NormalizeCode( ev.D( 7 ) );
				e.faultingOffset = offset;
				e.processId = ParsePid( ev.D( 8 ) );
				e.signature = $"{name}|{e.faultingModule}|{e.exceptionCode}|{TrimmedHex( ev.D( 7 ) )}";
			}
			else if( c.Hang is not null )
			{
				var ev = c.Hang;
				e.kind = CrashKind.Hang;
				e.time = ev.TimeUtc;
				name = ExeNameOf( ev.D( 0 ) );
				var p = CrashAppTable.NormalizePath( ev.D( 5 ) );
				path = p is not null && p.Contains( '\\' ) ? p : null;
				e.exeVersion = Clean( ev.D( 1 ) );
				e.processId = ParsePid( ev.D( 2 ) );
				e.signature = $"{name}|hang||";
			}
			else
			{
				var ev = c.E1026!;
				var (exe, _) = ParseDotNetText( ev.D( 0 ) );
				e.kind = CrashKind.Managed;
				e.time = ev.TimeUtc;
				name = ExeNameOf( exe );
				path = null;
			}

			if( c.E1026 is not null )
			{
				var (_, text) = ParseDotNetText( c.E1026.D( 0 ) );
				e.exceptionText = text;
				if( c.E1000 is null )
				{
					var type = text?.Split( '\n' )[0].Split( ':' )[0].Trim();
					e.signature = $"{name}||{type}|";
				}
			}

			var owner = ctx.Table.Find( path, name );
			if( !owner.IsOurs ) return null;

			e.app = owner.App;
			e.apps = owner.Apps;
			e.component = owner.Component;
			e.exe = path ?? owner.Candidates.Select( a => a.ExePath ).FirstOrDefault( p => p is not null ) ?? name;

			e.id = CrashEntry.MakeId( ctx.Machine, e.time, e.processId?.ToString( CultureInfo.InvariantCulture ) ?? "0" );

			// Windows writes each part of a version in 16 bits; the file says it right, if it is the file that crashed
			var crashedExe = path ?? owner.Candidates.Select( a => a.ExePath ).FirstOrDefault( p => p is not null );
			if( crashedExe is not null )
			{
				ChooseVersion( ctx, crashedExe, e.time, e.exeVersion, out var exeV, out var exeEv, out var exeNote );
				e.exeVersion = exeV; e.eventExeVersion = exeEv; e.exeVersionNote = exeNote;

				if( e.faultingModule is not null )
				{
					var moduleFile = e.faultingModulePath ?? System.IO.Path.Combine( System.IO.Path.GetDirectoryName( crashedExe ) ?? "", e.faultingModule );
					ChooseVersion( ctx, moduleFile, e.time, e.faultingModuleVersion, out var modV, out var modEv, out var modNote );
					e.faultingModuleVersion = modV; e.eventFaultingModuleVersion = modEv; e.faultingModuleVersionNote = modNote;
				}
			}

			// The report of the crash: found by what it says - its event time and its program - and not by the 1001, which Windows logs
			// again, days later, for the reports it kept queued.
			var foundReport = ctx.FindWerReport( name, path ?? owner.Candidates.Select( a => a.ExePath ).FirstOrDefault( p => p is not null ), e.time );
			if( foundReport is not null ) e.werReport = foundReport;

			// the dump: from the report when it names one, else the file LocalDumps would write
			string? dumpPath = null;
			if( c.Wer is not null )
			{
				var files = AttachedFiles( c.Wer ).ToList();
				dumpPath = files.FirstOrDefault( f => f.EndsWith( ".dmp", StringComparison.OrdinalIgnoreCase ) || f.EndsWith( ".mdmp", StringComparison.OrdinalIgnoreCase ) || f.EndsWith( ".hdmp", StringComparison.OrdinalIgnoreCase ) );

				var report = files.FirstOrDefault( f => f.EndsWith( "Report.wer", StringComparison.OrdinalIgnoreCase ) );
				if( report is null )
				{
					var folder = CrashAppTable.NormalizePath( c.Wer.D( 15 ) );
					if( folder is not null ) report = folder.EndsWith( ".wer", StringComparison.OrdinalIgnoreCase ) ? folder : folder + @"\Report.wer";
				}
				e.werReport ??= report;
			}

			if( dumpPath is not null )
			{
				var size = ctx.FileSize( dumpPath );
				e.dump = new CrashDump { path = dumpPath, exists = size is not null, size = size };
			}
			else if( name is not null && e.processId is int pid )
			{
				var folders = ctx.LocalDumpFolders( name );
				var file = $"{name}.{pid}.dmp";
				foreach( var folder in folders )
				{
					var candidate = Path.Combine( folder, file );
					if( ctx.FileSize( candidate ) is long size )
					{
						e.dump = new CrashDump { path = candidate, exists = true, size = size };
						break;
					}
				}

				if( e.dump is null && folders.Count > 0 )
					e.dump = new CrashDump { path = Path.Combine( folders[0], file ), exists = false };
			}

			// the dump folders of the application itself - what a report or LocalDumps did not find
			if( e.dump is not { exists: true } && owner.Candidates.Count > 0 )
			{
				var found = FindInAppDumpFolders( owner.Candidates.Select( a => a.App ).Distinct().SelectMany( a => ctx.AppDumpFolders( a ) ).Distinct( StringComparer.OrdinalIgnoreCase ),
									ctx, name, e.processId, e.time );
				if( found is not null ) e.dump = found;
			}

			return e;
		}

		/// <summary>
		/// The dump of a crash among the files of folders: the one named for the exe and process (<c>exe.1234.dmp</c>), else the
		/// newest written within a minute after the crash.
		/// </summary>
		static CrashDump? FindInAppDumpFolders( IEnumerable<string> folders, CrashContext ctx, string? exeName, int? pid, DateTime crashTime )
		{
			var files = new List<(string path, DateTime writtenUtc, long size)>();
			foreach( var f in folders )
			{
				try { files.AddRange( ctx.ListDumps( f ) ); }
				catch( Exception ) { } // a folder we cannot read has no dump for us
			}
			var picked = PickDump( files, exeName, pid, crashTime );
			return picked is null ? null : new CrashDump { path = picked.Value.path, exists = true, size = picked.Value.size };
		}

		/// <summary>
		/// The version of a crashed file: the file's own FileVersion when the file has not been written since the crash, with what the event said
		/// kept when it differs; the event's when the file has changed (and a note saying so), or when there is no file to ask.
		/// </summary>
		public static void ChooseVersion( CrashContext ctx, string file, DateTime crashUtc, string? eventVersion, out string? version, out string? eventValue, out string? note )
			=> ChooseVersion( eventVersion, ctx.FileVersion( file ), ctx.FileWritten( file ), crashUtc, out version, out eventValue, out note );

		/// <summary>Pure.</summary>
		public static void ChooseVersion( string? eventVersion, string? fileVersion, DateTime? fileWrittenUtc, DateTime crashUtc, out string? version, out string? eventValue, out string? note )
		{
			version = eventVersion;
			eventValue = null;
			note = null;

			if( fileWrittenUtc is not DateTime written ) return; // no file: the event is all there is

			if( written > crashUtc )
			{
				note = "as Windows recorded it (16 bits per part); the file has changed since the crash";
				return;
			}

			if( !string.IsNullOrWhiteSpace( fileVersion ) )
			{
				version = fileVersion!.Trim();
				if( !string.Equals( version, eventVersion, StringComparison.Ordinal ) ) eventValue = eventVersion;
			}
		}

		/// <summary>Pure: which of the files is the dump of the crash.</summary>
		public static (string path, DateTime writtenUtc, long size)? PickDump( IEnumerable<(string path, DateTime writtenUtc, long size)> files, string? exeName, int? pid, DateTime crashTime )
		{
			var list = files.ToList();

			if( exeName is not null && pid is not null )
			{
				var named = $"{exeName}.{pid}.dmp";
				var exact = list.Where( f => string.Equals( Path.GetFileName( f.path ), named, StringComparison.OrdinalIgnoreCase ) ).OrderByDescending( f => f.writtenUtc ).ToList();
				if( exact.Count > 0 ) return exact[0];
			}

			var near = list.Where( f => f.writtenUtc >= crashTime.AddSeconds( -2 ) && f.writtenUtc <= crashTime + DumpAfterCrash )
							.OrderByDescending( f => f.writtenUtc ).ToList();
			return near.Count > 0 ? near[0] : null;
		}

		static IEnumerable<CrashEntry> MakeReboots( List<RawEvent> all, CrashContext ctx )
		{
			CrashEntry? last = null;
			foreach( var ev in all.Where( e => Is( e, ProviderKernelPower, 41 ) || Is( e, ProviderEventLog, 6008 ) ) )
			{
				// 41 and 6008 of one reboot: the first one tells
				if( last is not null && ev.TimeUtc - last.time <= RebootWindow )
				{
					if( last.exceptionCode is null && Is( ev, ProviderKernelPower, 41 ) )
						last.exceptionCode = BugCheckOf( ev );
					continue;
				}

				if( last is not null ) yield return last;

				last = new CrashEntry
				{
					kind = CrashKind.Reboot,
					machine = ctx.Machine,
					time = ev.TimeUtc,
					exceptionText = "The system rebooted without cleanly shutting down first.",
					exceptionCode = Is( ev, ProviderKernelPower, 41 ) ? BugCheckOf( ev ) : null,
				};
				last.id = CrashEntry.MakeId( ctx.Machine, ev.TimeUtc, CrashKind.Reboot );
				last.signature = $"{CrashKind.Reboot}||";
			}

			if( last is not null ) yield return last;
		}

		/// <summary>The stop code of a Kernel-Power 41, when the reboot followed a bugcheck; null for 0.</summary>
		static string? BugCheckOf( RawEvent ev )
		{
			if( !ev.Named.TryGetValue( "BugcheckCode", out var code ) ) return null;
			if( !int.TryParse( code.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n ) || n == 0 ) return null;
			return "0x" + n.ToString( "x8", CultureInfo.InvariantCulture );
		}

		static CrashEntry MakeBugCheck( RawEvent ev, CrashContext ctx )
		{
			var e = new CrashEntry { kind = CrashKind.BugCheck, machine = ctx.Machine, time = ev.TimeUtc };
			var text = ev.D( 0 );
			var code = Regex.Match( text, "0x[0-9a-fA-F]+" );
			e.exceptionCode = code.Success ? NormalizeCode( code.Value ) : Clean( text );
			e.exceptionText = Clean( text );
			e.id = CrashEntry.MakeId( ctx.Machine, ev.TimeUtc, CrashKind.BugCheck );
			e.signature = $"{CrashKind.BugCheck}||{e.exceptionCode}|";

			// MEMORY.DMP is the dump of the LAST bugcheck. A file is this bugcheck's only when it was written within minutes of it: the
			// dump the event names, a minidump, or MEMORY.DMP - else a later crash has overwritten it.
			var named = CrashAppTable.NormalizePath( ev.D( 1 ) );
			var memory = Path.Combine( ctx.WindowsDir, "MEMORY.DMP" );
			var paths = new List<string>();
			if( named is not null && named.IndexOf( (char) 92 ) >= 0 ) paths.Add( named );
			if( !paths.Contains( memory, StringComparer.OrdinalIgnoreCase ) ) paths.Add( memory );

			bool IsOfThisBugCheck( string p )
				=> ctx.FileWritten( p ) is DateTime written && ( written - ev.TimeUtc ).Duration() <= BugCheckDumpWindow;

			var mini = ctx.FindMiniDump( ev.TimeUtc );
			var candidates = ( mini is null ? paths : new[] { mini }.Concat( paths ) ).Where( IsOfThisBugCheck ).ToList();

			if( candidates.Count > 0 && ctx.FileSize( candidates[0] ) is long size )
				e.dump = new CrashDump { path = candidates[0], exists = true, size = size };
			else
				e.dump = new CrashDump { path = paths[0], exists = false, note = ctx.FileSize( paths[0] ) is null ? "no dump file" : "overwritten by a later crash" };

			return e;
		}

		static CrashEntry MakeGpuReset( RawEvent ev, CrashContext ctx )
		{
			var driver = Clean( ev.D( 0 ) );
			var e = new CrashEntry
			{
				kind = CrashKind.GpuReset,
				machine = ctx.Machine,
				time = ev.TimeUtc,
				exceptionText = $"The display driver{( driver is null ? "" : " " + driver )} stopped responding and recovered.",
			};
			e.id = CrashEntry.MakeId( ctx.Machine, ev.TimeUtc, CrashKind.GpuReset );
			e.signature = $"{CrashKind.GpuReset}|{driver}||";
			return e;
		}
	}
}
