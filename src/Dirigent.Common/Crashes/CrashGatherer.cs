using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dirigent.Crashes
{
	/// <summary>What <see cref="CrashGatherer.ReadAsync"/> found on one machine.</summary>
	public class CrashGathered
	{
		public CrashAppTable Table = new();
		public CrashContext Context = new();

		/// <summary>The entries of the window, newest first, no filter but the window (the machine-level kinds included when wanted).</summary>
		public List<CrashEntry> Entries = new();

		/// <summary>The events of each entry, by its id.</summary>
		public Dictionary<string, List<RawEvent>> Matched = new();

		public bool Cut;
		public DateTime? OldestEvent;

		/// <summary>The app definitions of this machine, by app id.</summary>
		public Dictionary<string, AppDef> Defs = new( StringComparer.OrdinalIgnoreCase );
	}

	/// <summary>The part of reading the crashes of a machine that listing and collecting share: the table of programs and the event log.</summary>
	public static class CrashGatherer
	{
		/// <param name="dirig">the agent's own view</param>
		/// <param name="sinceUtc">the start of the window</param>
		/// <param name="untilUtc">the end of the window; null = now</param>
		/// <param name="machineLevel">whether to read the System log for the machine-level kinds too</param>
		/// <param name="dumpFolders">the dump folders of the applications, by app id (resolved by the master); null for none</param>
		public static async Task<CrashGathered> ReadAsync( IDirigAsync dirig, CrashEnvironment env, DateTime sinceUtc, DateTime? untilUtc, bool machineLevel,
				Dictionary<string, List<string>>? dumpFolders )
		{
			var now = DateTime.UtcNow;
			var machine = dirig.MachineId;
			var res = new CrashGathered();

			var table = new CrashAppTable();
			var defs = ( await dirig.GetAllAppsDefAsync() ).Select( kv => kv.Value )
						.Where( d => string.Equals( d.Id.MachineId, machine, StringComparison.OrdinalIgnoreCase ) ).ToList();
			foreach( var d in defs ) res.Defs[d.Id.ToString()] = d;

			// all at once: the agent answers each spec on its next tick
			var apps = await Task.WhenAll( defs.Select( async d =>
			{
				try
				{
					var spec = await dirig.ResolveAppLaunchSpecAsync( d );
					if( spec is null ) return null;

					var pe = spec.ParseExe();
					if( pe.ExeType == EExeType.DirigentCmd || pe.ReservedName is not null || string.IsNullOrWhiteSpace( pe.Path ) )
						return null; // runs no program of its own

					var file = spec.GetFileName( pe );

					// a bare name (found on the PATH, not where the launcher looks) is told by its name alone
					var bare = pe.Path.IndexOfAny( new[] { (char) 92, '/' } ) < 0 && !System.IO.Path.IsPathRooted( pe.Path );
					var rooted = !bare && System.IO.Path.IsPathRooted( file );
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
				catch( Exception )
				{
					return null;
				}
			} ) );

			table.Apps.AddRange( apps.Where( a => a is not null ).Select( a => a! ) );
			table.OwnExes.AddRange( env.OwnExes() );
			res.Table = table;

			var ctx = env.MakeContext( machine, table, now );
			if( dumpFolders is { Count: > 0 } )
			{
				var folders = new Dictionary<string, List<string>>( dumpFolders, StringComparer.OrdinalIgnoreCase );
				ctx.AppDumpFolders = app => folders.TryGetValue( app, out var l ) ? l : Array.Empty<string>();
			}
			res.Context = ctx;

			var source = env.Source!;
			var to = untilUtc ?? now;
			var appEvents = source.Read( new CrashEventQuery
			{
				Log = "Application",
				FromUtc = sinceUtc.AddMinutes( -1 ),
				ToUtc = to + CrashBuilder.WerDelay,
				Wanted = CrashBuilder.ApplicationWanted,
			} );

			var sysEvents = machineLevel
				? source.Read( new CrashEventQuery { Log = "System", FromUtc = sinceUtc, ToUtc = to, Wanted = CrashBuilder.SystemWanted } )
				: new CrashEventResult();

			res.Entries = CrashBuilder.Build( appEvents.Events.Concat( sysEvents.Events ), ctx, res.Matched )
							.Where( e => e.time >= sinceUtc && ( untilUtc is null || e.time <= untilUtc ) ).ToList();
			res.Cut = appEvents.Cut || sysEvents.Cut;
			res.OldestEvent = source.OldestEvent( "Application" );
			return res;
		}
	}
}
