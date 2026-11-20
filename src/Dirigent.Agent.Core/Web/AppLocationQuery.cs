using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dirigent.Web
{
	public class AppLocationPath
	{
		public string declared { get; set; } = string.Empty;
		public string? localPath { get; set; }
		public string? uncPath { get; set; }
		public string? uncError { get; set; }
		public bool exists { get; set; }
		public long? size { get; set; }
		public DateTime? modified { get; set; }
		public string? fileVersion { get; set; }

		/// <summary>"resource", or the path of the version file next to the exe that <see cref="fileVersion"/> was read from (its whole text, line breaks as "\n").</summary>
		public string? versionSource { get; set; }

		/// <summary>Set for the program when it is found on the agent's PATH rather than where the launcher looks.</summary>
		public bool? foundOnPath { get; set; }
	}

	public class AppLocationCmdLine
	{
		public string declared { get; set; } = string.Empty;
		public string expanded { get; set; } = string.Empty;
	}

	/// <summary>Where one application's binary and working folder are, as its agent resolves them.</summary>
	public class AppLocationEntry
	{
		public string app { get; set; } = string.Empty;
		public string? plan { get; set; }
		public string planSource { get; set; } = "none";
		public string machine { get; set; } = string.Empty;

		/// <summary>The Groups of the application (';'-separated) - the component it belongs to, so that versions can be shown per component.</summary>
		public string groups { get; set; } = string.Empty;

		/// <summary>The application's Component - what versions are listed by; null if it names none.</summary>
		public string? component { get; set; }

		/// <summary>"process", a reserved exe name ("cmd", "powershell.file", ...) or "dirigentCommand".</summary>
		public string? kind { get; set; }
		public AppLocationPath exe { get; set; } = new();
		public AppLocationPath startupDir { get; set; } = new();
		public AppLocationCmdLine cmdLineArgs { get; set; } = new();
		public string? error { get; set; }

		public string ToJson() => AppFilesResult.ToJson( this );
	}

	/// <summary>
	/// Where applications keep their binaries. The definitions are picked on the master thread exactly as
	/// <see cref="AppFilesQuery"/> picks them; the owning agents are then asked, one call per machine, all
	/// machines at once - see <c>BuiltIns/ResolveAppLocations.cs</c>.
	/// </summary>
	public class AppLocationQuery
	{
		readonly Master _master;
		readonly List<AppFilesQuery> _apps;
		readonly HashSet<string> _connected;

		AppLocationQuery( Master master, List<AppFilesQuery> apps, HashSet<string> connected )
		{
			_master = master;
			_apps = apps;
			_connected = connected;
		}

		/// <summary>One application. Master thread only.</summary>
		/// <exception cref="AppFilesNotFoundException">unknown app or plan</exception>
		public static AppLocationQuery Prepare( Master master, string id )
			=> new( master, new() { AppFilesQuery.Prepare( master, id ) }, ConnectedMachines( master ) );

		/// <summary>All applications, optionally those of one machine. Master thread only.</summary>
		public static AppLocationQuery PrepareAll( Master master, string? machine )
		{
			var apps = ( from kv in master.GetAllAppsDef()
						 where string.IsNullOrEmpty( machine ) || kv.Key.MachineId == machine
						 orderby kv.Key.MachineId, kv.Key.AppId
						 select AppFilesQuery.Prepare( master, kv.Key.ToString() ) ).ToList();

			return new( master, apps, ConnectedMachines( master ) );
		}

		static HashSet<string> ConnectedMachines( Master master )
			=> ( from kv in master.GetAllClientsState() where kv.Value.Connected select kv.Key ).ToHashSet();

		public async Task<List<AppLocationEntry>> ResolveAsync()
		{
			var entries = _apps.Select( a => new AppLocationEntry()
			{
				app = a.AppIdText,
				plan = a.Plan,
				planSource = a.PlanSource,
				machine = a.Def.Id.MachineId,
				groups = a.Def.Groups ?? string.Empty,
				component = string.IsNullOrEmpty( a.Def.Component ) ? null : a.Def.Component,
				exe = { declared = a.Def.ExeFullPath },
				startupDir = { declared = a.Def.StartupDir },
				cmdLineArgs = { declared = a.Def.CmdLineArgs },
			} ).ToList();

			// one call per machine, all the machines at once
			await Task.WhenAll( _apps.Select( ( a, i ) => ( a, i ) )
				.GroupBy( x => x.a.Def.Id.MachineId )
				.Select( g => AskMachine( g.Key, g.Select( x => x.a.Def ).ToList(), g.Select( x => entries[x.i] ).ToList() ) ) );

			return entries;
		}

		async Task AskMachine( string machine, List<Dirigent.AppDef> defs, List<AppLocationEntry> entries )
		{
			try
			{
				if( !_connected.Contains( machine ) )
					throw new Exception( $"Machine {machine} not connected." );

				var result = await _master.RunScriptAsync<Scripts.BuiltIn.ResolveAppLocations.TArgs, Scripts.BuiltIn.ResolveAppLocations.TResult>(
					machine, Scripts.BuiltIn.ResolveAppLocations._Name, null,
					new Scripts.BuiltIn.ResolveAppLocations.TArgs() { Apps = defs },
					$"Locating {defs.Count} app(s) of {machine}", out _ );

				if( result is null || result.Entries.Count != entries.Count )
					throw new Exception( $"{machine} answered nothing sensible." );

				for( int i = 0; i < entries.Count; i++ )
					Fill( entries[i], result.Entries[i] );
			}
			catch( Exception e )
			{
				foreach( var entry in entries )
					entry.error = Tools.JustFirstLine( e.Message );
			}
		}

		void Fill( AppLocationEntry entry, Scripts.BuiltIn.ResolveAppLocations.TEntry answer )
		{
			if( answer.Location is null )
			{
				entry.error = answer.Error ?? "No answer.";
				return;
			}

			var loc = answer.Location;
			entry.kind = loc.Kind;
			Fill( entry.exe, loc.Exe, entry.machine );
			if( loc.Exe.FoundOnPath ) entry.exe.foundOnPath = true;
			Fill( entry.startupDir, loc.StartupDir, entry.machine );
			entry.cmdLineArgs.expanded = loc.CmdLineExpanded;
		}

		void Fill( AppLocationPath to, Scripts.BuiltIn.ResolveAppLocations.TPath from, string machine )
		{
			to.localPath = from.LocalPath;
			to.exists = from.Exists;
			to.size = from.Size;
			to.modified = from.Modified;
			to.fileVersion = from.FileVersion;
			to.versionSource = from.VersionSource;

			if( string.IsNullOrEmpty( from.LocalPath ) ) return;

			if( !System.IO.Path.IsPathRooted( from.LocalPath ) )
			{
				to.uncError = "Not an absolute path, so no share can lead to it.";
				return;
			}

			try
			{
				to.uncPath = _master.MakeUNC( from.LocalPath, machine, to.declared );
			}
			catch( Exception e )
			{
				to.uncError = Tools.JustFirstLine( e.Message );
			}
		}
	}
}
