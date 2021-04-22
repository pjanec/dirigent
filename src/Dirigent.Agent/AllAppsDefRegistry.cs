using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dirigent.Common;

namespace Dirigent.Agent
{

	/// <summary>
	/// Used by master and by GUI to reflect the current state of any known application.
	/// App states are received from owning agents on change.
	/// App Defs are sent to owning agents on change.
	/// </summary>
	public class AllAppsDefRegistry
	{
		private Dictionary<AppIdTuple, AppDef> _appDefs = new Dictionary<AppIdTuple, AppDef>();

		public Dictionary<AppIdTuple, AppDef> AppDefs => _appDefs;
		public Action<AppDef>? Added;
		public Action<AppDef>? Updated;

		// does not fire notifs
		public void SetAll( IEnumerable<AppDef> allAppDefs )
		{
			_appDefs.Clear();
			foreach( var ad in allAppDefs )
			{
				_appDefs[ad.AppIdTuple] = ad;
			}
		}

		public void AddOrUpdate( AppDef newDef )
		{
			// check for change, fire change cb if there is
			if( _appDefs.TryGetValue( newDef.AppIdTuple, out var existingDef ) )
			{
				if( existingDef != newDef )
				{
					_appDefs[newDef.AppIdTuple] = newDef;
					Updated?.Invoke( newDef );
				}
			}
			else
			{
				_appDefs[newDef.AppIdTuple] = newDef;
				Added?.Invoke( newDef );
			}

		}

		public AppDef? FindApp( AppIdTuple appIdTuple, string? requestor = null )
		{
			if( _appDefs.TryGetValue( appIdTuple, out var existingDef ) )
			{
				return existingDef;
			}
			else if( requestor is not null )
			{
				throw new RemoteOperationErrorException( requestor, $"App {appIdTuple} does not exist." );
			}
			else
			{
				return null;
			}
		}						  

	}
}
