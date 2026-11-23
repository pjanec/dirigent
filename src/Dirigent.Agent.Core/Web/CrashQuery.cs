using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using Dirigent.Crashes;

namespace Dirigent.Web
{
	/// <summary>
	/// The crashes of the site: asks the agent of every machine (one call per machine, all at once - see
	/// <c>BuiltIns/QueryCrashes.cs</c>), then does what needs the whole picture or what only the master knows: the share of
	/// a dump, the uptime and plan of the process from the exit history and the current states, the exits no event
	/// explains, the newest of each app.
	/// </summary>
	public class CrashQuery
	{
		readonly Master _master;
		readonly CrashFilter _filter;
		readonly bool _details;
		readonly List<string> _machines;
		readonly HashSet<string> _connected;
		readonly CrashMerger _merger = new();

		CrashQuery( Master master, CrashFilter filter, bool details )
		{
			_master = master;
			_filter = filter;
			_details = details;

			var apps = master.GetAllAppsDef().ToList();
			foreach( var kv in apps )
				_merger.ComponentOf[kv.Key.ToString()] = string.IsNullOrEmpty( kv.Value.Component ) ? null : kv.Value.Component;

			_machines = apps.Select( kv => kv.Key.MachineId ).Concat( master.GetAllMachinesDef().Select( m => m.Id ) )
						.Where( m => !string.IsNullOrEmpty( m ) )
						.Distinct( StringComparer.OrdinalIgnoreCase )
						.Where( m => filter.machines.Count == 0 || filter.machines.Any( p => Scripts.BuiltIn.ListVfsNodes.IsMatch( p.Trim(), m ) ) )
						.OrderBy( m => m, StringComparer.OrdinalIgnoreCase ).ToList();

			_connected = ( from kv in master.GetAllClientsState() where kv.Value.Connected select kv.Key ).ToHashSet( StringComparer.OrdinalIgnoreCase );
			foreach( var kv in master.ExitHistory.Snapshot() )
				_merger.Exits[kv.Key.ToString()] = kv.Value;

			foreach( var kv in master.GetAllAppsState() )
				if( kv.Value.Running && kv.Value.PID > 0 )
					_merger.Running[kv.Key.ToString()] = new RunningProcess { Pid = kv.Value.PID, StartedAt = kv.Value.StartedAt, Plan = kv.Value.PlanName };
		}

		/// <summary>Master thread only.</summary>
		/// <exception cref="ArgumentException">until is earlier than since</exception>
		public static CrashQuery Prepare( Master master, CrashFilter filter, bool details )
			=> new( master, filter.Resolve( DateTime.UtcNow ), details );

		public async Task<CrashListing> RunAsync()
		{
			var perMachine = await Task.WhenAll( _machines.Select( AskMachine ) );

			var listing = new CrashListing();
			var entries = new List<CrashEntry>();
			foreach( var (status, list) in perMachine )
			{
				listing.machines.Add( status );
				entries.AddRange( list );
			}

			foreach( var e in entries ) _merger.Enrich( e );
			entries.AddRange( _merger.ExitEntries( entries, m => _machines.Contains( m, StringComparer.OrdinalIgnoreCase ) ).ToList() );

			// the full filter now that the apps are resolved as far as they can be
			var shown = CrashFilter.NewestPerApp( entries.Where( e => _filter.Matches( e ) ), _filter.newestPerApp );

			listing.crashes = shown;
			listing.totals.crashes = shown.Count;
			listing.totals.dumpBytes = shown.Where( e => e.dump is { exists: true } ).Sum( e => e.dump!.size ?? 0 );
			return listing;
		}

		async Task<(CrashMachineStatus status, List<CrashEntry> entries)> AskMachine( string machine )
		{
			var status = new CrashMachineStatus { machine = machine };

			if( !_connected.Contains( machine ) )
			{
				status.status = "not connected";
				return (status, new());
			}

			try
			{
				var result = await _master.RunScriptAsync<Scripts.BuiltIn.QueryCrashes.TArgs, Scripts.BuiltIn.QueryCrashes.TResult>(
					machine, Scripts.BuiltIn.QueryCrashes._Name, null,
					new Scripts.BuiltIn.QueryCrashes.TArgs() { Filter = _filter, Details = _details },
					$"Reading the crashes of {machine}", out _ );

				if( result is null ) throw new Exception( $"{machine} answered nothing sensible." );

				status.status = result.Status;
				status.coverage = result.Coverage;
				status.cut = result.Cut;

				var versions = new Dictionary<string, CrashVersions>( result.Versions, StringComparer.OrdinalIgnoreCase );

				foreach( var e in result.Entries )
				{
					e.machine = machine;
					e.time = DateTime.SpecifyKind( e.time, DateTimeKind.Utc );

					if( e.dump is { exists: true } )
					{
						try { e.dump.unc = _master.MakeUNC( e.dump.path, machine, "crash dump" ); }
						catch( Exception ex ) { e.dump.uncError = Tools.JustFirstLine( ex.Message ); }
					}

					if( _details )
					{
						e.machineInfo = result.Machine;
						if( e.exe is not null && versions.TryGetValue( e.exe, out var v ) )
							e.versions = FaultingFirst( v, e.faultingModule );
					}
				}

				return (status, result.Entries);
			}
			catch( Exception e )
			{
				status.status = "error: " + Tools.JustFirstLine( e.Message );
				return (status, new());
			}
		}

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

	/// <summary>The filter of the query string of the REST route and of the arguments of the CLI command.</summary>
	public static class CrashFilterParser
	{
		static List<string> Split( IEnumerable<string?> values )
			=> values.Where( v => v is not null ).SelectMany( v => v!.Split( ',' ) ).Select( v => v.Trim() ).Where( v => v.Length > 0 ).ToList();

		static DateTime ParseTime( string name, string text )
		{
			if( !DateTimeOffset.TryParse( text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var t ) )
				throw new ArgumentException( $"'{name}' is not a time (ISO 8601): '{text}'." );
			return t.UtcDateTime;
		}

		public static bool IsTrue( string? v ) => v is not null && ( v == "1" || v.Equals( "true", StringComparison.OrdinalIgnoreCase ) || v.Equals( "yes", StringComparison.OrdinalIgnoreCase ) );

		/// <summary>The filter from named values; each name may be given in the singular or the plural, repeated or comma-separated.</summary>
		/// <exception cref="ArgumentException">a value is not understood</exception>
		public static CrashFilter Parse( Func<string, string[]> values )
		{
			string[] Get( string singular ) => values( singular ).Concat( values( singular + "s" ) ).ToArray();

			var f = new CrashFilter
			{
				components = Split( Get( "component" ) ),
				apps = Split( Get( "app" ) ),
				machines = Split( Get( "machine" ) ),
				kinds = Split( Get( "kind" ) ),
				ids = Split( Get( "id" ) ),
			};

			var since = values( "since" ).FirstOrDefault( v => !string.IsNullOrWhiteSpace( v ) );
			var until = values( "until" ).FirstOrDefault( v => !string.IsNullOrWhiteSpace( v ) );
			if( since is not null ) f.since = ParseTime( "since", since );
			if( until is not null ) f.until = ParseTime( "until", until );

			var n = values( "newestPerApp" ).FirstOrDefault( v => !string.IsNullOrWhiteSpace( v ) );
			if( n is not null )
			{
				if( !int.TryParse( n, NumberStyles.None, CultureInfo.InvariantCulture, out var count ) )
					throw new ArgumentException( $"'newestPerApp' is not a number: '{n}'." );
				f.newestPerApp = count;
			}

			return f;
		}

		public static CrashFilter Parse( NameValueCollection q ) => Parse( name => q.GetValues( name ) ?? Array.Empty<string>() );

		public static CrashFilter Parse( IDictionary<string, string> options )
			=> Parse( name =>
			{
				foreach( var kv in options )
					if( string.Equals( kv.Key, name, StringComparison.OrdinalIgnoreCase ) ) return new[] { kv.Value };
				return Array.Empty<string>();
			} );
	}
}
