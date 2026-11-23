using System;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent.Crashes
{
	/// <summary>One exit of an application that Dirigent did not cause.</summary>
	public class AppExit
	{
		/// <summary>UTC, by the clock of the agent that saw the process end.</summary>
		public DateTime time { get; set; }
		public int? exitCode { get; set; }
		public int? pid { get; set; }
		public DateTime? startedAt { get; set; }
		public string? plan { get; set; }

		/// <summary>Restarts left at the moment it ended (-1 = without limit); null when not known then - it is not guessed.</summary>
		public int? restartsRemaining { get; set; }

		/// <summary>When the master saw the application start again after this exit (UTC, by the clock of its agent), and the new process; null while it has not.</summary>
		public DateTime? restartedAt { get; set; }
		public int? restartedPid { get; set; }
	}

	/// <summary>
	/// The last unexpected exits of each application, kept by the master from the states the agents report. In memory only:
	/// lost when the master restarts. Kept here and not in <see cref="AppState"/>, which goes to every GUI every second.
	/// </summary>
	public class ExitHistory
	{
		/// <summary>How many exits of an application are kept.</summary>
		public const int PerApp = 10;

		readonly object _lock = new();
		readonly Dictionary<AppIdTuple, List<AppExit>> _exits = new();

		static DateTime Utc( DateTime x )
			=> x.Kind == DateTimeKind.Local ? x.ToUniversalTime() : DateTime.SpecifyKind( x, DateTimeKind.Utc );

		/// <summary>
		/// Unexpected means the process ended without Dirigent killing it: it was running, and now is not, and neither state
		/// says it was killed or being killed.
		/// </summary>
		public static bool IsUnexpectedExit( AppState? before, AppState after )
			=> before is not null && before.Running && !after.Running
				&& !before.Killed && !before.Dying && !after.Killed && !after.Dying;

		/// <summary>Sees a new state of an application; records the exit when it is an unexpected one.</summary>
		/// <returns>the exit recorded, or null</returns>
		public AppExit? Observe( AppIdTuple id, AppState? before, AppState after, DateTime nowUtc )
		{
			// the application started again: the exit before it is followed by a restart - a fact seen, not a guess made at the exit
			if( after.Running && after.PID > 0 && ( before is null || !before.Running || before.PID != after.PID ) )
			{
				lock( _lock )
				{
					var open = _exits.TryGetValue( id, out var prior ) ? prior.LastOrDefault( x => x.restartedAt is null ) : null;
					if( open is not null )
					{
						open.restartedAt = after.StartedAt is DateTime st ? Utc( st ) : nowUtc;
						open.restartedPid = after.PID;
					}
				}
			}

			if( !IsUnexpectedExit( before, after ) ) return null;

			var exit = new AppExit
			{
				time = after.EndedAt is DateTime ended ? Utc( ended ) : nowUtc,
				exitCode = after.Started ? after.ExitCode : null,
				pid = before!.PID > 0 ? before.PID : null,
				startedAt = ( after.StartedAt ?? before.StartedAt ) is DateTime s ? Utc( s ) : null,
				plan = string.IsNullOrEmpty( after.PlanName ) ? ( string.IsNullOrEmpty( before.PlanName ) ? null : before.PlanName ) : after.PlanName,
				restartsRemaining = after.RestartsRemaining == AppState.RESTARTS_UNITIALIZED ? null : after.RestartsRemaining,
			};

			lock( _lock )
			{
				if( !_exits.TryGetValue( id, out var list ) ) _exits[id] = list = new();
				list.Add( exit );
				if( list.Count > PerApp ) list.RemoveRange( 0, list.Count - PerApp );
			}

			return exit;
		}

		/// <summary>The exits of an application, newest first.</summary>
		public List<AppExit> Of( AppIdTuple id )
		{
			lock( _lock )
				return _exits.TryGetValue( id, out var list ) ? Enumerable.Reverse( list ).ToList() : new();
		}

		/// <summary>All the exits, by application, each newest first.</summary>
		public Dictionary<AppIdTuple, List<AppExit>> Snapshot()
		{
			lock( _lock )
				return _exits.ToDictionary( kv => kv.Key, kv => Enumerable.Reverse( kv.Value ).ToList() );
		}
	}
}
