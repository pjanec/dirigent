using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent
{
	/// <summary>
	/// Maintains the current state of AppStates and PlanStates
	/// as received from the master
	/// </summary>
	public class ReflectedStateRepo	: IDirig
	{
		public ClientState? GetClientState( string Id ) { if( _clientStates.TryGetValue(Id, out var x)) return x; else return null; }
		public IEnumerable<KeyValuePair<string, ClientState>> GetAllClientStates() { return _clientStates; }
		public AppState? GetAppState( AppIdTuple Id ) { if( _appStates.TryGetValue(Id, out var st)) return st; else return null; }
		public IEnumerable<KeyValuePair<AppIdTuple, AppState>> GetAllAppStates() { return _appStates; }
		public PlanState? GetPlanState( string Id ) { if(string.IsNullOrEmpty(Id)) return null; if( _planStates.TryGetValue(Id, out var st)) return st; else return null; }
		public IEnumerable<KeyValuePair<string, PlanState>> GetAllPlanStates() { return _planStates; }
		public ScriptState? GetScriptState( string Id ) { if(string.IsNullOrEmpty(Id)) return null; if( _scriptStates.TryGetValue(Id, out var st)) return st; else return null; }
		public IEnumerable<KeyValuePair<string, ScriptState>> GetAllScriptStates() { return _scriptStates; }
		public AppDef? GetAppDef( AppIdTuple Id ) { if( _appDefs.TryGetValue(Id, out var st)) return st; else return null; }
		public IEnumerable<KeyValuePair<AppIdTuple, AppDef>> GetAllAppDefs() { return _appDefs;; }
		public PlanDef? GetPlanDef( string Id ) { return _planDefs.Find((x) => x.Name==Id); }
		public IEnumerable<PlanDef> GetAllPlanDefs() { return _planDefs; }
		public ScriptDef? GetScriptDef( string Id ) { return _scriptDefs.Find((x) => x.Id==Id); }
		public IEnumerable<ScriptDef> GetAllScriptDefs() { return _scriptDefs; }
		public void Send( Net.Message msg ) { _client.Send( msg ); }
		public string Name => _client.Ident.Name;

		// Fired awhen plan defs received/updated
		public Action? OnAppsReceived;
		public Action? OnPlansReceived;
		public Action? OnScriptsReceived;

		// fired when reset is received
		public Action? OnReset;

		private Net.Client _client;
		private Dictionary<AppIdTuple, AppState> _appStates = new Dictionary<AppIdTuple, AppState>();
		private Dictionary<string, ClientState> _clientStates = new Dictionary<string, ClientState>();
		
		/// <summary>
		/// The most recent app defs
		/// </summary>
		private Dictionary<AppIdTuple, AppDef> _appDefs = new();
		
		private Dictionary<string, PlanState> _planStates = new Dictionary<string, PlanState>();
		private List<PlanDef> _planDefs = new List<PlanDef>();

		private Dictionary<string, ScriptState> _scriptStates = new Dictionary<string, ScriptState>();
		private List<ScriptDef> _scriptDefs = new List<ScriptDef>();

		public ReflectedStateRepo( Net.Client client )
		{
			_client = client;
			_client.MessageReceived += OnMessage;
		}

		void OnMessage( Net.Message msg )
		{
			switch( msg )
			{
				case Net.ClientStateMessage m:
				{
					if( m.State != null && m.State.Ident != null ) // sanity check
					{
						var localTimeDelta = DateTime.UtcNow - m.TimeStamp;
						m.State.LastChange += localTimeDelta; // recalc to local time
						_clientStates[m.State.Ident.Name] = m.State;	
					}
					break;
				}

				case Net.AppsStateMessage m:
				{
					//Debug.Assert( m.AppsState != null );
					if( m.AppsState is not null )
					{
						var localTimeDelta = DateTime.UtcNow - m.TimeStamp;

						foreach( var (id, state) in m.AppsState )
						{
							state.LastChange += localTimeDelta; // recalc to local time

							_appStates[id] = state;	
						}
					}
					break;
				}

				case Net.PlansStateMessage m:
				{
					//Debug.Assert( m.PlansState != null );
					_planStates = m.PlansState ?? new Dictionary<string, PlanState>();
					break;
				}

				case Net.PlanDefsMessage m:
				{
					//Debug.Assert( m.PlanDefs != null );
					if( !m.Incremental ) // replace
					{
						if( m.PlanDefs is not null )
							_planDefs = new List<PlanDef>( m.PlanDefs );
						else
							_planDefs = new List<PlanDef>();
					}
					else // add/update
					{
						if( m.PlanDefs is not null )
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
					}
					OnPlansReceived?.Invoke();
					break;
				}

				case Net.ScriptStateMessage m:
				{
					//Debug.Assert( m.PlansState != null );
					_scriptStates = m.ScriptsState ?? new Dictionary<string, ScriptState>();
					break;
				}

				case Net.ScriptDefsMessage m:
				{
					if( !m.Incremental ) // replace
					{
						if( m.ScriptDefs is not null )
							_scriptDefs = new List<ScriptDef>( m.ScriptDefs );
						else
							_scriptDefs = new List<ScriptDef>();
					}
					else // add/update
					{
						if( m.ScriptDefs is not null )
						{
							foreach( var pd in m.ScriptDefs )
							{
								int idx = _scriptDefs.FindIndex( (x) => x.Id == pd.Id );
								if( idx < 0 )
								{
									_scriptDefs.Add( pd );
								}
								else
								{
									_scriptDefs[idx] = pd;
								}
							}
						}
					}
					OnScriptsReceived?.Invoke();
					break;
				}

				case Net.AppDefsMessage m:
				{
					//Debug.Assert( m.AppDefs != null );
					if( m.AppDefs is not null )
					{
						foreach( var ad in m.AppDefs )
						{
							_appDefs[ad.Id] = ad;
						}
					}

					OnAppsReceived?.Invoke();

					break;
				}

				case Net.ResetMessage m:
				{
					_appDefs.Clear(); 
					_planDefs.Clear();
					_appStates.Clear();
					_planStates.Clear();
					_scriptStates.Clear();
					OnReset?.Invoke();
					break;
				}
			}
		}
	}
}
