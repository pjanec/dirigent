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

			/// <summary>The dump folders of the applications of the machine (their "dump*" file definitions, resolved by the master), by app id.</summary>
			public Dictionary<string, List<string>>? DumpFolders;

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

			var wantsMachineLevel = filter.kinds.Count == 0 || filter.kinds.Any( k => CrashKind.IsMachineLevel( k ) );
			var gathered = await CrashGatherer.ReadAsync( Dirig, env, filter.since!.Value, filter.until, wantsMachineLevel, args.DumpFolders );

			var entries = gathered.Entries
							.Where( e => filter.Matches( e, deferAmbiguous: true ) )
							.ToList();

			result.Cut = gathered.Cut;
			if( entries.Count > MaxEntries )
			{
				entries = entries.Take( MaxEntries ).ToList();
				result.Cut = true;
			}

			result.Entries = entries;
			result.Coverage.oldestEvent = gathered.OldestEvent;

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
	}
}
