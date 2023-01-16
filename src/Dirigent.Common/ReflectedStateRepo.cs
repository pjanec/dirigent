using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dirigent
{
	/// <summary>
	/// Maintains the current state of AppStates and PlanStates
	/// as received from the master
	/// </summary>
	public class ReflectedStateRepo	: Disposable, IDirig
	{
		public ClientState? GetClientState( string Id ) { if( _clientStates.TryGetValue(Id, out var x)) return x; else return null; }
		public IEnumerable<KeyValuePair<string, ClientState>> GetAllClientStates() { return _clientStates; }
		public AppState? GetAppState( AppIdTuple Id ) { if( _appStates.TryGetValue(Id, out var st)) return st; else return null; }
		public IEnumerable<KeyValuePair<AppIdTuple, AppState>> GetAllAppStates() { return _appStates; }
		public PlanState? GetPlanState( string Id ) { if(string.IsNullOrEmpty(Id)) return null; if( _planStates.TryGetValue(Id, out var st)) return st; else return null; }
		public IEnumerable<KeyValuePair<string, PlanState>> GetAllPlanStates() { return _planStates; }
		public ScriptState? GetScriptState( Guid Id ) { return _scriptReg.GetScriptState(Id); }
		public IEnumerable<KeyValuePair<Guid, ScriptState>> GetAllScriptStates() { return _scriptReg.GetAllScriptStates(); }
		public AppDef? GetAppDef( AppIdTuple Id ) { if( _appDefs.TryGetValue(Id, out var st)) return st; else return null; }
		public IEnumerable<KeyValuePair<AppIdTuple, AppDef>> GetAllAppDefs() { return _appDefs;; }
		public PlanDef? GetPlanDef( string Id ) { return _planDefs.Find((x) => x.Name==Id); }
		public IEnumerable<PlanDef> GetAllPlanDefs() { return _planDefs; }
		public ScriptDef? GetScriptDef( Guid Id ) { return _scriptReg.ScriptDefs.Find((x) => x.Guid==Id); }
		public IEnumerable<ScriptDef> GetAllScriptDefs() { return _scriptReg.ScriptDefs; }
		public VfsNodeDef? GetVfsNodeDef( Guid guid ) { return _fileReg.GetVfsNodeDef(guid); }
		public IEnumerable<VfsNodeDef> GetAllVfsNodeDefs() { return _fileReg.GetAllVfsNodeDefs(); }
		public MachineDef? GetMachineDef( string Id ) { return _machineDefs.Find((x) => x.Id==Id); }
		public IEnumerable<MachineDef> GetAllMachineDefs() { return _machineDefs; }
		public MachineState? GetMachineState( string Id ) { if(string.IsNullOrEmpty(Id)) return null; if( _machineStates.TryGetValue(Id, out var st)) return st; else return null; }
		public Task<TResult?> RunScriptAsync<TArgs, TResult>( string clientId, string scriptName, string? sourceCode, TArgs? args, string title, out Guid scriptInstance )
		  => _scriptReg.RunScriptAsync<TArgs, TResult>( clientId, scriptName, sourceCode, args, title, out scriptInstance );
		public Task<VfsNodeDef?> ResolveAsync( VfsNodeDef nodeDef, bool forceUNC, bool includeContent ) => _fileReg.ResolveAsync( _syncIDirig, nodeDef, forceUNC, includeContent, null );


		public void Send( Net.Message msg ) { _client.Send( msg ); }
		public string Name => _client.Ident.Name;

		// Fired awhen plan defs received/updated
		public Action? OnAppsReceived;
		public Action? OnPlansReceived;
		public Action? OnScriptsReceived;
		public Action? OnFilesReceived;
		public Action? OnMachinesReceived;
		public Action? OnActionsReceived;

		// fired when reset is received
		public Action? OnReset;

		private Net.Client _client;
		public Net.Client Client => _client;
		private Dictionary<AppIdTuple, AppState> _appStates = new();
		private Dictionary<string, ClientState> _clientStates = new();
		public Dictionary<string, ClientState> ClientStates => _clientStates;
		
		/// <summary>
		/// The most recent app defs
		/// </summary>
		private Dictionary<AppIdTuple, AppDef> _appDefs = new();
		
		private Dictionary<string, PlanState> _planStates = new();
		private List<PlanDef> _planDefs = new List<PlanDef>();
		
		/// <summary>
		/// tool menu actions
		/// </summary>
		private List<AssocMenuItemDef> _menuItemDefs = new List<AssocMenuItemDef>();
		public List<AssocMenuItemDef> MenuItems => _menuItemDefs;

		//private Dictionary<Guid, ScriptState> _scriptStates = new Dictionary<Guid, ScriptState>();

		private ReflectedScriptRegistry _scriptReg;
		public ReflectedScriptRegistry ScriptReg => _scriptReg;

		private FileRegistry _fileReg;
		public FileRegistry FileReg => _fileReg;
		SynchronousOpProcessor _syncOps;
		SynchronousIDirig _syncIDirig;

		

		private List<MachineDef> _machineDefs = new List<MachineDef>();
		private Dictionary<string, MachineState> _machineStates = new(); // id => state

		public ReflectedStateRepo( Net.Client client, string localMachineId, string rootForRelativePaths )
		{
			_client = client;
			_client.MessageReceived += OnMessage;

			_syncOps = new SynchronousOpProcessor();
			_syncIDirig = new SynchronousIDirig( this, _syncOps );
			
			_scriptReg = new ReflectedScriptRegistry( this );

			_fileReg = new FileRegistry( this, localMachineId, rootForRelativePaths, (string machineId) =>
			{
				if( _clientStates.TryGetValue( machineId, out var state ) )
				{
					return state.IP;
				}
				return null;
			});
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if( !disposing ) return;
			_client.MessageReceived -= OnMessage;
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
							if( state.LastChange.Ticks != 0 ) // if this is unitialized (from whatever reason), do not try to adjust it
							{
								state.LastChange += localTimeDelta; // recalc to local time
							}

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
					_scriptReg.UpdateScriptState( m.Instance, m.State );
					break;
				}

				case Net.ScriptDefsMessage m:
				{
					_scriptReg.SetScriptDefs( m.ScriptDefs, m.Incremental );
					OnScriptsReceived?.Invoke();
					break;
				}

				case Net.MenuItemDefsMessage m:
				{
					_menuItemDefs = m.MenuItemDefs ?? new();
					OnActionsReceived?.Invoke();
					break;
				}

				case Net.VfsNodesMessage m:
				{
					_fileReg.SetVfsNodes( m.VfsNodes );
					OnFilesReceived?.Invoke();
					break;
				}

				case Net.MachineDefsMessage m:
				{
					_machineDefs = m.Machines.ToList();
					_fileReg.SetMachines( _machineDefs );
					OnMachinesReceived?.Invoke();
					break;
				}

				case Net.MachineStateMessage m:
				{
					_machineStates[m.Id] = m.State;
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
					_scriptReg.Clear();
					_machineDefs.Clear();
					_fileReg.Clear();
					OnReset?.Invoke();
					break;
				}
			}
		}
	}
}
