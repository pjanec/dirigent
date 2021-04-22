using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent.Common
{
	/// <summary>
	/// Maintains the current state of AppStates and PlanStates
	/// as received from the master
	/// </summary>
	public class ReflectedStateRepo
	{
		public Dictionary<AppIdTuple, AppState> AppStates => _appStates;
		public Dictionary<string, PlanState> PlanStates => _planStates;
		public List<PlanDef> PlanDefs => _planDefs;

		private Net.Client _client;
		private Dictionary<AppIdTuple, AppState> _appStates = new Dictionary<AppIdTuple, AppState>();
		private Dictionary<string, PlanState> _planStates = new Dictionary<string, PlanState>();
		private List<PlanDef> _planDefs = new List<PlanDef>();

		public ReflectedStateRepo( Net.Client client )
		{
			_client = client;
			_client.MessageReceived += OnMessage;
		}

		void OnMessage( Net.Message msg )
		{
			switch( msg )
			{
				case Net.AppsStateMessage m:
				{
					_appStates = m.appsState;
					break;
				}

				case Net.PlansStateMessage m:
				{
					_planStates = m.plansState;
					break;
				}

				case Net.PlanDefsMessage m:
				{
					_planDefs = new List<PlanDef>( m.PlanDefs );
					break;
				}
			}
		}
	}
}
