using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dirigent.Common;

namespace Dirigent.Agent
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

		//public bool TryGet( AppIdTuple appId, out AppState appState )
		//{
		//	appState = default(AppState);
		//	return false;
		//}

		public void AddOrUpdate( AppDef appDef )
		{
			LocalApp? la;
			if( _apps.TryGetValue( appDef.AppIdTuple, out la ) )
			{
				la.UpdateAppDef( appDef );
			}
			else
			{
				la = new LocalApp( appDef, _sharedContext );
				_apps[appDef.AppIdTuple] = la;
			}


		}

	}
}
