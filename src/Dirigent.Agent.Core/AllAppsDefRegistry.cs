using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent
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
				_appDefs[ad.Id] = ad;
			}
		}

		public void AddOrUpdate( AppDef newAppDef )
		{
			// check for change, fire change cb if there is
			if( _appDefs.TryGetValue( newAppDef.Id, out var existingRec ) )
			{
				if( existingRec != newAppDef )
				{
					_appDefs[newAppDef.Id] = newAppDef;
					Updated?.Invoke( newAppDef );
				}
			}
			else
			{
				_appDefs[newAppDef.Id] = newAppDef;
				Added?.Invoke( newAppDef );
			}

		}

		/// <summary>
		/// Finds app definition by id. Throws if failed.
		/// </summary>
		public AppDef FindApp( AppIdTuple id )
		{
			if( _appDefs.TryGetValue( id, out var existingAdr ) )
			{
				return existingAdr;
			}
			else
			{
				throw new UnknownAppIdException( id );
			}
		}						  

	}
}
