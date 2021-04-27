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
		public Dictionary<AppIdTuple, AppDef> AppDefs => _appDefs;
		public Dictionary<string, PlanState> PlanStates => _planStates;
		public List<PlanDef> PlanDefs => _planDefs;
		public Net.Client Client => _client;

		private Net.Client _client;
		private Dictionary<AppIdTuple, AppState> _appStates = new Dictionary<AppIdTuple, AppState>();
		
		/// <summary>
		/// The most recent app defs
		/// </summary>
		private Dictionary<AppIdTuple, AppDef> _appDefs = new();
		
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
					if( m.AppsState != null )
					{
						foreach( var (id, state) in m.AppsState )
						{
							_appStates[id] = state;	
						}
					}
					else
					{
						// WTF?? no one is sending empty message!!!
						int i =1;
					}
					break;
				}

				case Net.PlansStateMessage m:
				{
					if( m.PlansState != null )
					{
						_planStates = m.PlansState;
					}
					break;
				}

				case Net.PlanDefsMessage m:
				{
					if( m.PlanDefs != null )
					{
						_planDefs = new List<PlanDef>( m.PlanDefs );
					}
					break;
				}

				case Net.AppDefsMessage m:
				{
					if( m.AppDefs != null )
					{
						foreach( var ad in m.AppDefs )
						{
							_appDefs[ad.Id] = ad;
						}
					}
					break;
				}
			}
		}
	}
}
