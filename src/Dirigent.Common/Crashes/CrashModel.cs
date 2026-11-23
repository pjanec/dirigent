using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dirigent.Crashes
{
	/// <summary>The kinds of entries <c>GET /api/crashes</c> lists.</summary>
	public static class CrashKind
	{
		/// <summary>A native crash: Application Error 1000, with its Windows Error Reporting 1001.</summary>
		public const string Crash = "crash";

		/// <summary>An unhandled .NET exception: .NET Runtime 1026, with the Application Error 1000 of the same process.</summary>
		public const string Managed = "managed";

		/// <summary>Application Hang 1002.</summary>
		public const string Hang = "hang";

		/// <summary>Unexpected reboot: Kernel-Power 41 / EventLog 6008. No app.</summary>
		public const string Reboot = "reboot";

		/// <summary>Blue screen: BugCheck 1001. No app.</summary>
		public const string BugCheck = "bugcheck";

		/// <summary>Display driver reset: Display 4101. No app.</summary>
		public const string GpuReset = "gpuReset";

		/// <summary>No event, but Dirigent saw the process end without having killed it. Made at the master.</summary>
		public const string Exit = "exit";

		public static bool IsMachineLevel( string kind ) => kind == Reboot || kind == BugCheck || kind == GpuReset;
	}

	/// <summary>The dump of a crash, as its machine sees it. <see cref="unc"/> is added by the master.</summary>
	public class CrashDump
	{
		public string path { get; set; } = string.Empty;
		public string? unc { get; set; }
		public string? uncError { get; set; }
		public bool exists { get; set; }
		public long? size { get; set; }

		/// <summary>Why there is no dump, when that is worth saying ("overwritten by a later crash").</summary>
		public string? note { get; set; }
	}

	/// <summary>A file of the crashed program's folder.</summary>
	public class CrashFileVersion
	{
		public string name { get; set; } = string.Empty;
		public string? version { get; set; }
		public long size { get; set; }
		public DateTime modified { get; set; }

		/// <summary>Only in what a download collects; the listing has no hashes.</summary>
		public string? sha256 { get; set; }

		/// <summary>Set (true) when the file was written after the crash: it is not what crashed. Only in what a download collects.</summary>
		public bool? modifiedAfterCrash { get; set; }
	}

	/// <summary>Which build crashed: the exe's version and what lies beside it. No hashes here.</summary>
	public class CrashVersions
	{
		public string? exeFileVersion { get; set; }
		public string? exeProductVersion { get; set; }

		/// <summary>The DLLs of the exe's folder; the faulting module first.</summary>
		public List<CrashFileVersion> files { get; set; } = new();

		/// <summary>When the files were read (UTC): they are as they are then, not as at the crash. Only in what a download collects.</summary>
		public DateTime? collectedAt { get; set; }

		public string? note { get; set; }
	}

	public class CrashDisk
	{
		public string drive { get; set; } = string.Empty;
		public long freeMB { get; set; }
		public long totalMB { get; set; }
	}

	public class CrashGpu
	{
		public string? name { get; set; }
		public string? driverVersion { get; set; }
		public string? driverDate { get; set; }
	}

	/// <summary>What the machine was like - read once per machine.</summary>
	public class CrashMachineInfo
	{
		public string? os { get; set; }
		public string? osBuild { get; set; }
		public double uptimeSeconds { get; set; }
		public string? cpu { get; set; }
		public long ramTotalMB { get; set; }
		public long ramFreeMB { get; set; }
		public List<CrashDisk> disks { get; set; } = new();
		public List<CrashGpu> gpus { get; set; } = new();
	}

	/// <summary>One crash (or exit, or machine-level event), as listed.</summary>
	public class CrashEntry
	{
		/// <summary>machine/UTC time yyyy-MM-ddTHH:mm:ssZ/pid - for the entries with no process, the kind stands in for the pid. Stable.</summary>
		public string id { get; set; } = string.Empty;
		public string kind { get; set; } = CrashKind.Crash;
		public string machine { get; set; } = string.Empty;

		/// <summary>The application ("m1.a"); null when there is none or when several share the exe and nothing tells them apart (see <see cref="apps"/>).</summary>
		public string? app { get; set; }

		/// <summary>All the applications the exe may be; empty for a machine-level entry.</summary>
		public List<string> apps { get; set; } = new();

		/// <summary>The component of the application; null when there is no app or the candidates differ.</summary>
		public string? component { get; set; }

		public string? exe { get; set; }
		public string? exeVersion { get; set; }

		/// <summary>UTC.</summary>
		public DateTime time { get; set; }

		public string? exceptionCode { get; set; }

		/// <summary>The type and the first lines of the text of an unhandled .NET exception, cut.</summary>
		public string? exceptionText { get; set; }
		public string? faultingModule { get; set; }
		public string? faultingModuleVersion { get; set; }
		public string? faultingOffset { get; set; }

		/// <summary>exe file name|module|code|offset (module-relative; zeros trimmed) - the same bug has the same signature.</summary>
		public string? signature { get; set; }
		public int? processId { get; set; }

		/// <summary>The exit code Dirigent saw; only for <see cref="CrashKind.Exit"/>.</summary>
		public int? exitCode { get; set; }

		public CrashDump? dump { get; set; }
		public string? werReport { get; set; }

		/// <summary>When Dirigent started the process, UTC - when it did (matched by the pid).</summary>
		public DateTime? startedAt { get; set; }
		public double? uptimeSeconds { get; set; }
		public string? plan { get; set; }

		/// <summary>Only with details.</summary>
		public CrashVersions? versions { get; set; }

		/// <summary>Only with details. (Not called "machine": that is the id.)</summary>
		public CrashMachineInfo? machineInfo { get; set; }

		public static string MakeId( string machine, DateTime utc, string suffix )
			=> $"{machine}/{utc.ToUniversalTime():yyyy-MM-ddTHH:mm:ss}Z/{suffix}";
	}

	/// <summary>
	/// What a download of crashes asks for: the filters of the listing and what to take of each crash. Without
	/// <see cref="dumps"/> only the records are collected; without <see cref="context"/> only the dumps.
	/// </summary>
	public class CrashSelection : CrashFilter
	{
		public bool dumps { get; set; } = true;
		public bool context { get; set; } = true;

		/// <summary>A dump bigger than this is left out (and the archive says so); dumps are never cut. Null: no limit.</summary>
		public long? maxDumpBytes { get; set; }

		/// <summary>
		/// The selection with its window resolved: its own since/until, else those of the request they are part of, else the last 7 days.
		/// </summary>
		/// <exception cref="ArgumentException">until is earlier than since</exception>
		public CrashSelection ResolveWith( DateTime? requestSince, DateTime? requestUntil, DateTime nowUtc )
		{
			var copy = (CrashSelection) MemberwiseClone();
			copy.since ??= requestSince;
			copy.until ??= requestUntil;
			return (CrashSelection) copy.Resolve( nowUtc );
		}

		/// <summary>The bytes of dumps the selection would collect from the entries: only the dumps that are there, and not bigger than the limit.</summary>
		public long DumpBytesOf( IEnumerable<CrashEntry> entries )
			=> !dumps ? 0 : DistinctDumps( entries.Where( e => e.dump is { exists: true } && ( maxDumpBytes is null || ( e.dump.size ?? 0 ) <= maxDumpBytes ) ) ).Sum( d => d.size ?? 0 );

		/// <summary>The dumps of the entries, a file once however many entries name it.</summary>
		public static IEnumerable<CrashDump> DistinctDumps( IEnumerable<CrashEntry> entries )
			=> entries.Where( e => e.dump is not null ).Select( e => e.dump! ).GroupBy( d => d.path, StringComparer.OrdinalIgnoreCase ).Select( g => g.First() );
	}

	/// <summary>How far back one machine's log reaches.</summary>
	public class CrashCoverage
	{
		public DateTime? oldestEvent { get; set; }
	}

	/// <summary>What one machine said.</summary>
	public class CrashMachineStatus
	{
		public string machine { get; set; } = string.Empty;

		/// <summary>"ok", "not connected", "not supported" or "error: ...".</summary>
		public string status { get; set; } = "ok";
		public CrashCoverage coverage { get; set; } = new();
		public bool cut { get; set; }
	}

	public class CrashTotals
	{
		public int crashes { get; set; }
		public long dumpBytes { get; set; }
	}

	public class CrashListing
	{
		public List<CrashEntry> crashes { get; set; } = new();
		public List<CrashMachineStatus> machines { get; set; } = new();
		public CrashTotals totals { get; set; } = new();
	}

	/// <summary>
	/// What to list. The same object for the listing and - later - for collecting: different filters AND, the values of
	/// one filter OR. An entry without a component is dropped only by <see cref="components"/>.
	/// </summary>
	public class CrashFilter
	{
		public const int DefaultDays = 7;

		/// <summary>ISO 8601; without an offset, UTC.</summary>
		public DateTime? since { get; set; }
		public DateTime? until { get; set; }

		[JsonConverter( typeof( StringOrListConverter ) )]
		public List<string> components { get; set; } = new();

		/// <summary>App ids, with the wildcards of the config ("*", "?").</summary>
		[JsonConverter( typeof( StringOrListConverter ) )]
		public List<string> apps { get; set; } = new();

		[JsonConverter( typeof( StringOrListConverter ) )]
		public List<string> machines { get; set; } = new();

		[JsonConverter( typeof( StringOrListConverter ) )]
		public List<string> kinds { get; set; } = new();

		[JsonConverter( typeof( StringOrListConverter ) )]
		public List<string> ids { get; set; } = new();

		// The singular names of the query parameters, accepted in a JSON body too - one value or a list. Read only: they add to the lists above.
		[JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull ), Newtonsoft.Json.JsonIgnore, JsonConverter( typeof( StringOrListConverter ) )]
		public List<string>? component { get => null; set => components.AddRange( value ?? new() ); }

		[JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull ), Newtonsoft.Json.JsonIgnore, JsonConverter( typeof( StringOrListConverter ) )]
		public List<string>? app { get => null; set => apps.AddRange( value ?? new() ); }

		[JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull ), Newtonsoft.Json.JsonIgnore, JsonConverter( typeof( StringOrListConverter ) )]
		public List<string>? machine { get => null; set => machines.AddRange( value ?? new() ); }

		[JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull ), Newtonsoft.Json.JsonIgnore, JsonConverter( typeof( StringOrListConverter ) )]
		public List<string>? kind { get => null; set => kinds.AddRange( value ?? new() ); }

		[JsonIgnore( Condition = JsonIgnoreCondition.WhenWritingNull ), Newtonsoft.Json.JsonIgnore, JsonConverter( typeof( StringOrListConverter ) )]
		public List<string>? id { get => null; set => ids.AddRange( value ?? new() ); }

		/// <summary>Only the newest N entries of each app; null = all.</summary>
		public int? newestPerApp { get; set; }

		static DateTime Utc( DateTime x )
			=> x.Kind == DateTimeKind.Local ? x.ToUniversalTime() : DateTime.SpecifyKind( x, DateTimeKind.Utc );

		/// <summary>
		/// The filter with its window made definite and UTC: without any end the last 7 days; with only 'until', the 7 days before it.
		/// </summary>
		/// <exception cref="ArgumentException">until is earlier than since</exception>
		public CrashFilter Resolve( DateTime nowUtc )
		{
			var res = (CrashFilter) MemberwiseClone();
			res.components = new( components );
			res.apps = new( apps );
			res.machines = new( machines );
			res.kinds = new( kinds );
			res.ids = new( ids );

			DateTime? s = since is DateTime a ? Utc( a ) : null;
			DateTime? u = until is DateTime b ? Utc( b ) : null;

			if( s is DateTime ss && u is DateTime uu && uu < ss )
				throw new ArgumentException( "'until' is earlier than 'since'." );

			if( s is null ) s = ( u ?? nowUtc ).AddDays( -DefaultDays );

			res.since = s;
			res.until = u;
			return res;
		}

		static bool Any( List<string> values, Func<string, bool> test ) => values.Any( v => !string.IsNullOrWhiteSpace( v ) && test( v.Trim() ) );
		static bool Active( List<string> values ) => values.Any( v => !string.IsNullOrWhiteSpace( v ) );

		/// <summary>
		/// Whether the entry passes - every filter except <see cref="newestPerApp"/>, which needs the whole picture.
		/// </summary>
		/// <param name="deferAmbiguous">an entry that has several candidate apps and no app is not judged by apps and components (the master may resolve it by pid)</param>
		public bool Matches( CrashEntry e, bool deferAmbiguous = false )
		{
			if( since is DateTime s && e.time < s ) return false;
			if( until is DateTime u && e.time > u ) return false;

			if( Active( kinds ) && !Any( kinds, k => string.Equals( k, e.kind, StringComparison.OrdinalIgnoreCase ) ) ) return false;
			if( Active( ids ) && !Any( ids, i => string.Equals( i, e.id, StringComparison.OrdinalIgnoreCase ) ) ) return false;
			if( Active( machines ) && !Any( machines, m => Scripts.BuiltIn.ListVfsNodes.IsMatch( m, e.machine ) ) ) return false;

			var ambiguous = e.app is null && e.apps.Count > 1;
			if( deferAmbiguous && ambiguous ) return true;

			if( Active( components ) && !( e.component is not null && Any( components, c => Scripts.BuiltIn.ListVfsNodes.IsMatch( c, e.component ) ) ) )
				return false;

			if( Active( apps ) )
			{
				var names = e.app is not null ? new List<string> { e.app } : e.apps;
				if( !names.Any( n => Any( apps, p => MatchesApp( p, n ) ) ) ) return false;
			}

			return true;
		}

		/// <summary>A pattern against "m1.a" - or against the app part alone when the pattern has no machine.</summary>
		static bool MatchesApp( string pattern, string appId )
		{
			if( Scripts.BuiltIn.ListVfsNodes.IsMatch( pattern, appId ) ) return true;
			if( pattern.Contains( '.' ) ) return false;
			var dot = appId.IndexOf( '.' );
			return dot >= 0 && Scripts.BuiltIn.ListVfsNodes.IsMatch( pattern, appId.Substring( dot + 1 ) );
		}

		/// <summary>The newest N of each app (entries with no app, or machine-level ones, are all kept), newest first.</summary>
		public static List<CrashEntry> NewestPerApp( IEnumerable<CrashEntry> entries, int? n )
		{
			var sorted = entries.OrderByDescending( e => e.time ).ThenBy( e => e.id, StringComparer.Ordinal ).ToList();
			if( n is null || n < 0 ) return sorted;

			var seen = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );
			var res = new List<CrashEntry>();
			foreach( var e in sorted )
			{
				var key = e.app ?? ( e.apps.Count > 0 ? string.Join( ",", e.apps.OrderBy( a => a, StringComparer.OrdinalIgnoreCase ) ) : null );
				if( key is not null )
				{
					seen.TryGetValue( key, out var count );
					if( count >= n ) continue;
					seen[key] = count + 1;
				}
				res.Add( e );
			}
			return res;
		}
	}
}

namespace Dirigent.Crashes
{
	/// <summary>A JSON string or an array of strings, read as a list: <c>"app": "m1.a"</c> and <c>"apps": ["m1.a", "m2.*"]</c> are both what a tool writes.</summary>
	public class StringOrListConverter : JsonConverter<List<string>>
	{
		public override List<string>? Read( ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options )
		{
			var res = new List<string>();
			switch( reader.TokenType )
			{
				case JsonTokenType.Null:
					return res;
				case JsonTokenType.String:
					res.Add( reader.GetString() ?? "" );
					return res;
				case JsonTokenType.StartArray:
					while( reader.Read() && reader.TokenType != JsonTokenType.EndArray )
						res.Add( reader.TokenType == JsonTokenType.String ? reader.GetString() ?? "" : throw new JsonException( "A list of strings is expected." ) );
					return res;
				default:
					throw new JsonException( "A string or a list of strings is expected." );
			}
		}

		public override void Write( Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options )
		{
			writer.WriteStartArray();
			foreach( var v in value ) writer.WriteStringValue( v );
			writer.WriteEndArray();
		}
	}
}
