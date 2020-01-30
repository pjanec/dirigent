using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Common;

namespace Dirigent.Agent.Core
{
	public class AppRestarterManager
	{
		/// <summary>
		/// Helpers for implementing a pending RestartApp operation. They get instantiated upon RestartApp
		/// request and removed when the restart has finished. At most one per AppIdTuple.
		/// </summary>
		Dictionary<AppIdTuple, AppRestarter> appRestarters = new Dictionary<AppIdTuple, AppRestarter>();

		LocalOperations localOps;

		public AppRestarterManager( LocalOperations localOps )
		{
			this.localOps = localOps;
		}
		
		/// <summary>
		/// Ticks app restarters and remove those that are finished
		/// </summary>
		public void Tick()
		{
			List<KeyValuePair<AppIdTuple, AppRestarter>> toRemove = new List<KeyValuePair<AppIdTuple, AppRestarter>>();
			foreach( var kv in appRestarters )
			{
				var r = kv.Value;
				r.Tick();
				if( r.ShallBeRemoved )
				{
					toRemove.Add( kv );
				}
			}

			foreach( var kv in toRemove )
			{
				appRestarters.Remove( kv.Key );
			}
		}

		public void AddOrReset(AppDef appDef, AppState appState, bool waitBeforeRestart )
		{
			// add a brand new appRestarter if not exist yet
			AppRestarter r;
			if( !appRestarters.TryGetValue( appDef.AppIdTuple, out r ) )
			{
				r = new AppRestarter( appDef, appState, localOps, waitBeforeRestart );
				appRestarters[appDef.AppIdTuple] = r;
			}
			else // there is one already, reset it to a state as if just created
			{
				r.Reset(waitBeforeRestart);
			}
		}

		public void Remove( AppDef appDef )
		{
			AppRestarter r;
			if( appRestarters.TryGetValue( appDef.AppIdTuple, out r ) )
			{
				r.Dispose();
				appRestarters.Remove( appDef.AppIdTuple );
			}

		}

	}
}
