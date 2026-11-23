using System;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent
{
	/// <summary>Things the shared config gets wrong without being invalid, worth a warning in the log.</summary>
	public static class ConfigChecks
	{
		/// <summary>
		/// Applications whose machine id differs from a defined machine's only by case. Dirigent compares machine ids exactly, so such an
		/// application is never given to the agent of the machine - one warning per pair, naming both.
		/// </summary>
		public static List<string> MachineIdCaseMismatches( IEnumerable<string> definedMachines, IEnumerable<AppIdTuple> apps )
		{
			var machines = definedMachines.Where( m => !string.IsNullOrEmpty( m ) ).Distinct().ToList();
			var warnings = new List<string>();

			foreach( var group in apps.GroupBy( a => a.MachineId ).OrderBy( g => g.Key, StringComparer.Ordinal ) )
			{
				if( machines.Contains( group.Key ) ) continue;

				var like = machines.FirstOrDefault( m => string.Equals( m, group.Key, StringComparison.OrdinalIgnoreCase ) );
				if( like is null ) continue;

				warnings.Add( $"Application(s) {string.Join( ", ", group.Select( a => a.ToString() ).OrderBy( x => x, StringComparer.Ordinal ) )}: machine id '{group.Key}' differs only by case from the defined machine '{like}'. "
							+ "Machine ids are compared exactly, so the agent of that machine is not given these applications." );
			}

			return warnings;
		}
	}
}
