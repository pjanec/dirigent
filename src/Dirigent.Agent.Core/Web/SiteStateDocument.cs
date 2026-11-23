using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Dirigent.Web
{
	/// <summary>
	/// <c>_sitestate.json</c> of a collected archive: what the site was when the files were collected - which versions
	/// ran, which machines were there, what the plans and the applications were doing. For a person reading an archive
	/// attached to a bug report, first; so it is indented, the versions come first and as lines, empty fields are left out,
	/// and nothing is interpreted - the states are facts, not a diagnosis.
	/// </summary>
	public static class SiteStateDocument
	{
		public class Doc
		{
			public string? site { get; set; }
			public string collected { get; set; } = string.Empty;
			public string? comment { get; set; }
			public List<string>? selection { get; set; }
			public string? logsSince { get; set; }
			public string? logsUntil { get; set; }
			public string dirigent { get; set; } = string.Empty;
			public List<AppVersionEntry> versions { get; set; } = new();
			public List<Machine> machines { get; set; } = new();
			public List<PlanState> plans { get; set; } = new();
			public List<App> apps { get; set; } = new();
		}

		public class Machine
		{
			public string id { get; set; } = string.Empty;
			public string? ip { get; set; }
			public bool connected { get; set; }
			public bool? master { get; set; }
		}

		public class App
		{
			public string id { get; set; } = string.Empty;
			public string? component { get; set; }
			public string state { get; set; } = string.Empty;
			public string? plan { get; set; }
			public int? pid { get; set; }
			public string? startedAt { get; set; }
			public string? endedAt { get; set; }
			public int? exitCode { get; set; }
			public string? exe { get; set; }

			/// <summary>How many crashes of the application are in the window of the collection; absent when the collecting machines did not look.</summary>
			public int? crashes { get; set; }

			public LastCrash? lastCrash { get; set; }
		}

		public class LastCrash
		{
			public string time { get; set; } = string.Empty;
			public string kind { get; set; } = string.Empty;
		}

		static readonly JsonSerializerOptions _json = new()
		{
			WriteIndented = true,
			DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // "Bug 4711 - <tool>" stays readable, not <
		};

		/// <summary>
		/// What the master knows is read here, on its thread; the locations (and with them the versions) are asked of
		/// the agents in the task returned.
		/// </summary>
		public static Task<string?> Start( Master master, SiteStateExtras extras )
		{
			var doc = new Doc()
			{
				site = string.IsNullOrEmpty( master.SiteName ) ? null : master.SiteName,
				collected = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} (UTC {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss})",
				comment = string.IsNullOrWhiteSpace( extras.Comment ) ? null : extras.Comment.Trim(),
				selection = extras.Selection.Count > 0 ? extras.Selection.ToList() : null,
				logsSince = string.IsNullOrWhiteSpace( extras.LogsSince ) ? null : extras.LogsSince.Trim(),
				logsUntil = string.IsNullOrWhiteSpace( extras.LogsUntil ) ? null : extras.LogsUntil.Trim(),
				dirigent = AppConfig.VersionText(),
			};

			var clients = master.GetAllClientsState().ToDictionary( x => x.Key, x => x.Value );
			doc.machines = ( from m in master.GetAllMachinesDef()
							 orderby m.Id
							 select new Machine()
							 {
								 id = m.Id,
								 ip = clients.TryGetValue( m.Id, out var c ) && !string.IsNullOrEmpty( c.IP ) ? c.IP : m.IP,
								 connected = clients.TryGetValue( m.Id, out var c2 ) && c2.Connected,
								 master = m.Id == master.MachineId ? true : null,
							 } ).ToList();

			doc.plans = ( from p in master.GetAllPlansState() select new PlanState( p.Key, p.Value ) ).ToList();

			var states = ( from kv in master.GetAllAppsState()
						   let def = master.GetAppDef( kv.Key )
						   where def is not null
						   orderby kv.Key.MachineId, kv.Key.AppId
						   select new AppState( def, null, kv.Value, master.ExitHistory.Of( kv.Key ) ) ).ToList();

			var query = AppLocationQuery.PrepareAll( master, null );

			return Finish( doc, states, query, extras.Crashes, extras.CrashesLooked );
		}

		static async Task<string?> Finish( Doc doc, List<AppState> states, AppLocationQuery query, List<Dirigent.Crashes.CrashSummaryItem> crashes, bool crashesLooked )
		{
			var crashesOf = crashes.GroupBy( c => c.App, StringComparer.OrdinalIgnoreCase ).ToDictionary( g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase );

			var locations = await query.ResolveAsync();
			var exeOf = locations.ToDictionary( l => l.app, l => l.exe.localPath ?? NullIfEmpty( l.exe.declared ) );

			doc.versions = AppVersions.Of( locations );
			doc.apps = ( from s in states
						 let id = s.id.Split( '@' )[0]
						 select new App()
						 {
							 id = id,
							 component = s.component,
							 state = s.state.code,
							 plan = s.state.plan,
							 pid = s.state.pid,
							 startedAt = Local( s.state.startedAt ),
							 endedAt = Local( s.state.endedAt ),
							 exitCode = s.state.exitCode,
							 exe = exeOf.TryGetValue( id, out var exe ) ? exe : null,
							 crashes = crashesLooked ? ( crashesOf.TryGetValue( id, out var c ) ? c.Count : 0 ) : null,
							 lastCrash = crashesOf.TryGetValue( id, out var lc ) ? new LastCrash() { time = Local( lc.Last )!, kind = lc.LastKind } : null,
						 } ).ToList();

			return JsonSerializer.Serialize( doc, _json );
		}

		/// <summary>A UTC time as a person reads it on the site: local time, with the UTC one beside it.</summary>
		static string? Local( DateTime? utc )
			=> utc is null ? null : $"{utc.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss} (UTC {utc.Value:HH:mm:ss})";

		static string? NullIfEmpty( string? s ) => string.IsNullOrEmpty( s ) ? null : s;
	}
}
