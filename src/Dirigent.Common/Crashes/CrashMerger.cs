using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Dirigent.Crashes
{
	/// <summary>A process Dirigent launched and that still runs.</summary>
	public class RunningProcess
	{
		public int Pid;
		public DateTime? StartedAt;
		public string? Plan;
	}

	/// <summary>
	/// What the master knows and the event log does not: when and in which plan Dirigent started the crashed process, and the
	/// exits that left no event. Pure - the master hands it its exit history and states.
	/// </summary>
	public class CrashMerger
	{
		/// <summary>An exit and an event of the same app and process lie within this: they are one crash.</summary>
		public static readonly TimeSpan MergeWindow = TimeSpan.FromSeconds( 30 );

		/// <summary>An exit whose start time is not known matches the event of its pid only within this (a pid is reused).</summary>
		public static readonly TimeSpan PidWindow = TimeSpan.FromMinutes( 5 );

		/// <summary>
		/// Whether the event happened to the process of the exit: within its life, from its start to a moment after its end - a crashed
		/// process may live on while Windows reports on it, so its exit can come minutes after the event - and, when its start is not
		/// known, within a few minutes of its end.
		/// </summary>
		public static bool IsEventOfExit( DateTime eventTime, AppExit exit, TimeSpan slack )
		{
			var end = Utc( exit.time );
			if( exit.startedAt is DateTime s )
				return eventTime >= Utc( s ).AddSeconds( -5 ) && eventTime <= end + slack;
			return ( end - eventTime ).Duration() <= slack;
		}

		/// <summary>The unexpected exits by app ("m1.a"), each list newest first.</summary>
		public Dictionary<string, List<AppExit>> Exits = new( StringComparer.OrdinalIgnoreCase );

		/// <summary>The running processes by app.</summary>
		public Dictionary<string, RunningProcess> Running = new( StringComparer.OrdinalIgnoreCase );

		/// <summary>The component of each app.</summary>
		public Dictionary<string, string?> ComponentOf = new( StringComparer.OrdinalIgnoreCase );

		static DateTime Utc( DateTime x )
			=> x.Kind == DateTimeKind.Local ? x.ToUniversalTime() : DateTime.SpecifyKind( x, DateTimeKind.Utc );

		/// <summary>
		/// The process of the entry, if Dirigent launched it: its start time, plan and uptime - from the exit history, or from the
		/// state of the app while the same process still runs. The pid also settles which app it was when several share the exe.
		/// </summary>
		public void Enrich( CrashEntry e )
		{
			if( e.processId is not int pid || CrashKind.IsMachineLevel( e.kind ) ) return;

			var candidates = e.app is not null ? new List<string> { e.app } : e.apps;

			foreach( var app in candidates )
			{
				DateTime? startedAt = null;
				string? plan = null;
				var found = false;

				var exit = Exits.TryGetValue( app, out var list )
							? list.FirstOrDefault( x => x.pid == pid && IsEventOfExit( e.time, x, PidWindow ) )
							: null;

				if( exit is not null )
				{
					found = true;
					startedAt = exit.startedAt;
					plan = exit.plan;
				}
				else if( Running.TryGetValue( app, out var cur ) && cur.Pid == pid && ( cur.StartedAt is null || Utc( cur.StartedAt.Value ) <= e.time ) )
				{
					found = true;
					startedAt = cur.StartedAt;
					plan = cur.Plan;
				}

				if( !found ) continue;

				if( e.app is null )
				{
					e.app = app;
					e.component = ComponentOf.TryGetValue( app, out var c ) ? c : null;
				}

				e.plan = string.IsNullOrEmpty( plan ) ? null : plan;
				if( startedAt is DateTime s )
				{
					e.startedAt = Utc( s );
					e.uptimeSeconds = Math.Max( 0, Math.Round( ( e.time - e.startedAt.Value ).TotalSeconds, 1 ) );
				}
				return;
			}
		}

		/// <summary>The exits Dirigent saw that no event explains (the same app and process, within a few seconds).</summary>
		/// <param name="events">the entries the event logs made</param>
		/// <param name="machineWanted">which machines are asked for</param>
		public IEnumerable<CrashEntry> ExitEntries( IReadOnlyList<CrashEntry> events, Func<string, bool> machineWanted )
		{
			foreach( var (app, list) in Exits )
			{
				var machine = app.Substring( 0, Math.Max( 0, app.IndexOf( '.' ) ) );
				if( !machineWanted( machine ) ) continue;

				foreach( var x in list )
				{
					var explained = events.Any( e => e.processId is not null && e.processId == x.pid
									&& e.machine.Equals( machine, StringComparison.OrdinalIgnoreCase )
									&& ( string.Equals( e.app, app, StringComparison.OrdinalIgnoreCase ) || e.apps.Contains( app, StringComparer.OrdinalIgnoreCase ) )
									&& IsEventOfExit( e.time, x, MergeWindow ) );
					if( explained ) continue;

					ComponentOf.TryGetValue( app, out var component );
					var time = Utc( x.time );
					var entry = new CrashEntry
					{
						kind = CrashKind.Exit,
						machine = machine,
						app = app,
						apps = new() { app },
						component = component,
						time = time,
						processId = x.pid,
						exitCode = x.exitCode,
						plan = x.plan,
						startedAt = x.startedAt is DateTime s ? Utc( s ) : null,
						signature = $"{app}|exit|{x.exitCode}|",
					};
					entry.uptimeSeconds = entry.startedAt is DateTime st ? Math.Max( 0, Math.Round( ( time - st ).TotalSeconds, 1 ) ) : null;
					entry.id = CrashEntry.MakeId( machine, time, ( x.pid ?? 0 ).ToString( CultureInfo.InvariantCulture ) );
					yield return entry;
				}
			}
		}
	}
}
