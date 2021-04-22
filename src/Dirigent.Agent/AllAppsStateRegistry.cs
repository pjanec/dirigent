using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dirigent.Common;

namespace Dirigent.Agent
{

	///// <summary>
	///// Description of applications's current state
	///// </summary>
	//public class RemoteApp
	//{
	//	private AppState _appState;

	//	private AppDef _appDef;
	//}


	/// <summary>
	/// Used by master and by GUI to reflect the current state of any known application.
	/// App states are received from owning agents on change.
	/// App Defs are sent to owning agents on change.
	/// </summary>
	public class AllAppsStateRegistry
	{
		private Dictionary<AppIdTuple, AppState> _appStates = new Dictionary<AppIdTuple, AppState>();

		public Dictionary<AppIdTuple, AppState> AppStates => _appStates;

		public void AddOrUpdate( AppIdTuple appId, AppState appState )
		{
			_appStates[appId] = appState;
		}
	}
}
