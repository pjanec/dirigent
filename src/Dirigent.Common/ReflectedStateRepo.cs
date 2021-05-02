using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent.Common
{
	/// <summary>
	/// Maintains the current state of AppStates and PlanStates
	/// as received from the master
	/// </summary>
	public class ReflectedStateRepo	: IDirig
	{
		//public Dictionary<AppIdTuple, AppState> AppStates => _appStates;
		//public Dictionary<AppIdTuple, AppDef> AppDefs => _appDefs;
		//public Dictionary<string, PlanState> PlanStates => _planStates;
		//public List<PlanDef> PlanDefs => _planDefs;
		//public void Send( Net.Message msg ) { _client.Send( msg ); }


		public AppState? GetAppState( AppIdTuple Id ) { if( _appStates.TryGetValue(Id, out var st)) return st; else return null; }
		public IEnumerable<KeyValuePair<AppIdTuple, AppState>> GetAllAppStates() { return _appStates; }
		public PlanState? GetPlanState( string Id ) { if( _planStates.TryGetValue(Id, out var st)) return st; else return null; }
		public IEnumerable<KeyValuePair<string, PlanState>> GetAllPlanStates() { return _planStates; }
		public AppDef? GetAppDef( AppIdTuple Id ) { if( _appDefs.TryGetValue(Id, out var st)) return st; else return null; }
		public IEnumerable<KeyValuePair<AppIdTuple, AppDef>> GetAllAppDefs() { return _appDefs;; }
		public PlanDef? GetPlanDef( string Id ) { return _planDefs.Find((x) => x.Name==Id); }
		public IEnumerable<PlanDef> GetAllPlanDefs() { return _planDefs; }
		public void Send( Net.Message msg ) { _client.Send( msg ); }


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
					Debug.Assert( m.AppsState != null );
					foreach( var (id, state) in m.AppsState )
					{
						_appStates[id] = state;	
					}
					break;
				}

				case Net.PlansStateMessage m:
				{
					Debug.Assert( m.PlansState != null );
					_planStates = m.PlansState;
					break;
				}

				case Net.PlanDefsMessage m:
				{
					Debug.Assert( m.PlanDefs != null );
					if( !m.Incremental ) // replace
					{
						_planDefs = new List<PlanDef>( m.PlanDefs );
					}
					else // add/update
					{
						foreach( var pd in m.PlanDefs )
						{
							int idx = _planDefs.FindIndex( (x) => x.Name == pd.Name );
							if( idx < 0 )
							{
								_planDefs.Add( pd );
							}
							else
							{
								_planDefs[idx] = pd;
							}
						}
					}
					break;
				}

				case Net.AppDefsMessage m:
				{
					Debug.Assert( m.AppDefs != null );

					foreach( var ad in m.AppDefs )
					{
						_appDefs[ad.Id] = ad;
					}

					break;
				}
			}
		}
	}
}
