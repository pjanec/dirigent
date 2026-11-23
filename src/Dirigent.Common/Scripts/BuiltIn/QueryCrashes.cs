using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Dirigent;
using Dirigent.Crashes;

namespace Dirigent.Scripts.BuiltIn
{
	/// <summary>
	/// Lists the crashes of the programs of the site on the machine this script runs on - read from the Windows event log
	/// of that machine.
	/// </summary>
	/// <remarks>
	/// One call per machine, all the machines at once (see <c>CrashQuery</c> at the master). The agent builds the table of
	/// the site's programs itself - the programs of the app definitions it holds, expanded as the launcher expands them -
	/// reads the events of the window, correlates them (<see cref="CrashBuilder"/>) and filters them. What needs the whole
	/// picture (the newest of each app) and what only the master knows (the share of a dump, uptime, plan) is done
	/// at the master. The result carries no event XML and exception texts are cut: it goes to every client until 3.1.20.
	/// </remarks>
	public class QueryCrashes : Script
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public static readonly string _Name = "BuiltIns/QueryCrashes.cs";

		/// <summary>No machine answers with more entries than this.</summary>
		public const int MaxEntries = 500;

		/// <summary>The environment of a machine; replaced by tests, so that they do not depend on the real event log.</summary>
		public static Func<string, CrashEnvironment> EnvironmentFactory = _ => CrashEnvironment.Real();

		public class TArgs
		{
			public CrashFilter Filter = new();

			/// <summary>Add the versions of the crashed programs and the facts of the machine.</summary>
			public bool Details;

			public override string ToString() => "crashes";
		}

		public class TResult
		{
			/// <summary>"ok" or "not supported".</summary>
			public string Status = "ok";
			public CrashCoverage Coverage = new();
			public bool Cut;
			public List<CrashEntry> Entries = new();

			/// <summary>With details: the versions of the exe of the entries, by exe path.</summary>
			public Dictionary<string, CrashVersions> Versions = new();

			/// <summary>With details: the facts of the machine.</summary>
			public CrashMachineInfo? Machine;
		}

		protected async override Task<string?> Run()
		{
			var args = Tools.Deserialize<TArgs>( Args ) ?? new TArgs();
			var now = DateTime.UtcNow;
			var filter = args.Filter.Resolve( now );
			var machine = Dirig.MachineId;
			var env = EnvironmentFactory( machine );

			var result = new TResult();
			if( !env.IsSupported )
			{
				result.Status = "not supported";
				return Tools.Serialize( result );
			}

			var table = await BuildTable( env, machine );
			var ctx = env.MakeContext( machine, table, now );
			var source = env.Source!;

			var to = filter.until ?? now;
			var appEvents = source.Read( new CrashEventQuery
			{
				Log = "Application",
				FromUtc = filter.since!.Value.AddMinutes( -1 ),
				ToUtc = to + CrashBuilder.WerDelay,
				Wanted = CrashBuilder.ApplicationWanted,
			} );

			var wantsMachineLevel = filter.kinds.Count == 0 || filter.kinds.Any( k => CrashKind.IsMachineLevel( k ) );
			var sysEvents = wantsMachineLevel
				? source.Read( new CrashEventQuery { Log = "System", FromUtc = filter.since.Value, ToUtc = to, Wanted = CrashBuilder.SystemWanted } )
				: new CrashEventResult();

			var entries = CrashBuilder.Build( appEvents.Events.Concat( sysEvents.Events ), ctx )
							.Where( e => filter.Matches( e, deferAmbiguous: true ) )
							.ToList();

			result.Cut = appEvents.Cut || sysEvents.Cut;
			if( entries.Count > MaxEntries )
			{
				entries = entries.Take( MaxEntries ).ToList();
				result.Cut = true;
			}

			result.Entries = entries;
			result.Coverage.oldestEvent = source.OldestEvent( "Application" );

			if( args.Details )
			{
				foreach( var path in entries.Where( e => !CrashKind.IsMachineLevel( e.kind ) && e.exe is not null && e.exe.Contains( '\\' ) )
										.Select( e => e.exe! ).Distinct( StringComparer.OrdinalIgnoreCase ) )
				{
					var v = env.ReadVersions( path );
					if( v is not null ) result.Versions[path] = v;
				}

				result.Machine = env.ReadMachineInfo();
			}

			return Tools.Serialize( result );
		}

		/// <summary>The programs of this machine's app definitions - expanded as the launcher expands them - and Dirigent's own.</summary>
		async Task<CrashAppTable> BuildTable( CrashEnvironment env, string machine )
		{
			var table = new CrashAppTable();

			var defs = ( await Dirig.GetAllAppsDefAsync() )
						.Select( kv => kv.Value )
						.Where( d => string.Equals( d.Id.MachineId, machine, StringComparison.OrdinalIgnoreCase ) )
						.ToList();

			// all at once: the agent answers each spec on its next tick
			var apps = await Task.WhenAll( defs.Select( async d =>
			{
				try
				{
					var spec = await Dirig.ResolveAppLaunchSpecAsync( d );
					if( spec is null ) return null;

					var pe = spec.ParseExe();
					if( pe.ExeType == EExeType.DirigentCmd || pe.ReservedName is not null || string.IsNullOrWhiteSpace( pe.Path ) )
						return null; // runs no program of its own

					var file = spec.GetFileName( pe );
					var rooted = System.IO.Path.IsPathRooted( file );
					return new CrashApp
					{
						App = d.Id.ToString(),
						Component = string.IsNullOrEmpty( d.Component ) ? null : d.Component,
						ExePath = rooted ? file : null,
						ExeName = System.IO.Path.GetFileName( rooted ? file : pe.Path ),
						StartDir = spec.GetWorkingDirectory(),
						CmdLine = pe.CmdLine,
					};
				}
				catch( Exception e )
				{
					log.Debug( $"Cannot tell the program of {d.Id}: {e.Message}" );
					return null;
				}
			} ) );

			table.Apps.AddRange( apps.Where( a => a is not null ).Select( a => a! ) );
			table.OwnExes.AddRange( env.OwnExes() );
			return table;
		}
	}
}
