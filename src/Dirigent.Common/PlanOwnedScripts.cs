using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent
{
	/// <summary>
	/// The scripts that belong to the marking a plan does when it starts - the master script and the slaves it started on
	/// the machines. A Kill All spares them (the marking finishes, bounded by the plan's MarkTimeout), unless it is the
	/// second one in a row, which is the emergency stop.
	/// </summary>
	/// <remarks>
	/// Kept per process, by script instance: the master script and the slaves it starts all run where the master is, and
	/// the master is what decides about KillAll.
	/// </remarks>
	public static class PlanOwnedScripts
	{
		// value: true = a slave on a machine, false = the master script that started the slaves
		static readonly ConcurrentDictionary<Guid, bool> _ids = new();

		public static void Add( Guid id, bool slave = false ) => _ids[id] = slave;
		public static void Remove( Guid id ) => _ids.TryRemove( id, out _ );
		public static bool Contains( Guid id ) => _ids.ContainsKey( id );

		/// <summary>The master scripts of the markings under way. Killing one is enough: it stops its slaves itself.</summary>
		public static List<Guid> Masters() => _ids.Where( kv => !kv.Value ).Select( kv => kv.Key ).ToList();
	}
}
