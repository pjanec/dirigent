using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent
{


	/// <summary>
	/// Used on agent. Contains only the apps belonging to the agent (having same machineId as the agent)
	/// </summary>
	public class LocalAppsRegistry
	{
		private Dictionary<AppIdTuple, LocalApp> _apps = new Dictionary<AppIdTuple, LocalApp>();
		public Dictionary<AppIdTuple, LocalApp> Apps => _apps;

        private SharedContext _sharedContext;

		public LocalAppsRegistry( SharedContext shCtx )
		{
            _sharedContext = shCtx;
		}

		public void Tick()
		{
			foreach( var li in Apps.Values )
			{
				li.Tick();
			}
		}

		public void Clear()
		{
			_apps.Clear();
		}

		//public bool TryGet( AppIdTuple appId, out AppState appState )
		//{
		//	appState = default(AppState);
		//	return false;
		//}

		/// <summary>
		/// Adds a new local app record if not yet existing
		/// </summary>
		/// <returns>null if already exisitng, new rec if just added</returns>
		public LocalApp? AddIfMissing( AppDef appDef, string? planName )
		{
			LocalApp? la;
			if( !_apps.TryGetValue( appDef.Id, out la ) )
			{
				la = new LocalApp( appDef, _sharedContext );
				_apps[appDef.Id] = la;
				return la;
			}
			return null; // not added, already existing
		}

		/// <summary>
		/// Creates new local app record if not exist yet.
		/// Otherwise updates the UpcomingAppDef.
		/// </summary>
		/// <param name="ad"></param>
		/// <param name="planName"></param>
		/// <returns></returns>
		public LocalApp AddOrUpdate(AppDef ad)
		{
			LocalApp? la;
			if (_apps.TryGetValue(ad.Id, out la))
			{
				la.UpdateAppDef(ad);
			}
			else
			{
				la = new LocalApp( ad, _sharedContext );
				_apps[ad.Id] = la;
			}
			return la;
		}

		/// <summary>
		/// Finds a LocalApp record by appIdTuple.
		/// Throws on failure.
		/// </summary>
		public LocalApp FindApp( AppIdTuple id )
		{
			if( _apps.TryGetValue( id, out var la ) )
			{
				return la;
			}
			throw new UnknownAppIdException( id );
		}

	}
}
