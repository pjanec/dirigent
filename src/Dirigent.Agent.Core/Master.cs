using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dirigent.Net;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace Dirigent
{
	public class Master : Disposable, IDirig
	{

		#region IDirig interface

		public ClientState? GetClientState( string Id ) { if( _allClientStates.ClientStates.TryGetValue(Id, out var x)) return x; else return null; }
		public IEnumerable<KeyValuePair<string, ClientState>> GetAllClientStates() { return _allClientStates.ClientStates; }
		public AppState? GetAppState( AppIdTuple Id ) { if( _allAppStates.AppStates.TryGetValue(Id, out var x)) return x; else return null; }
		public IEnumerable<KeyValuePair<AppIdTuple, AppState>> GetAllAppStates() { return _allAppStates.AppStates; }
		public PlanState? GetPlanState( string Id ) { if( _plans.Plans.TryGetValue(Id, out var x)) return x.State; else return null; }
		public IEnumerable<KeyValuePair<string, PlanState>> GetAllPlanStates() { return from x in _plans.Plans.Values select new KeyValuePair<string, PlanState>( x.Name, x.State ); }
		public ScriptState? GetScriptState( Guid Id ) { return _reflScripts.GetScriptState( Id ); }
		public IEnumerable<KeyValuePair<Guid, ScriptState>> GetAllScriptStates() { return _reflScripts.GetAllScriptStates(); }
		public AppDef? GetAppDef( AppIdTuple Id ) { if( _allAppDefs.AppDefs.TryGetValue(Id, out var x)) return x; else return null; }
		public IEnumerable<KeyValuePair<AppIdTuple, AppDef>> GetAllAppDefs() { return _allAppDefs.AppDefs; }
		public PlanDef? GetPlanDef( string Id ) { if( _plans.Plans.TryGetValue( Id, out var p ) ) return p.Def; else return null; }
		public IEnumerable<PlanDef> GetAllPlanDefs() { return from x in _plans.Plans.Values select x.Def; }
		public ScriptDef? GetScriptDef( Guid Id ) { return _reflScripts.ScriptDefs.Find((x) => x.Guid == Id); }
		public IEnumerable<ScriptDef> GetAllScriptDefs() { return _reflScripts.ScriptDefs; }
		public VfsNodeDef? GetVfsNodeDef( Guid guid ) { return _files.GetVfsNodeDef(guid); }
		public IEnumerable<VfsNodeDef> GetAllVfsNodeDefs() { return _files.GetAllVfsNodeDefs(); }
		public string Name => string.Empty;
		
		/// <summary>
		/// Send to all subcribed. Does not change the sender.
		/// This might be called from asyn context!
		/// </summary>
		/// <param name="msg"></param>
		public void Send( Net.Message msg )
		{
			// send to everyone else
			_server.SendToAllSubscribed( msg, EMsgRecipCateg.All );
			// process also on master
			_server.BufferMessageReceived( msg );
		}
		public Task<TResult?> RunScriptAsync<TArgs, TResult>( string clientId, string scriptName, string? sourceCode, TArgs? args, string title, out Guid scriptInstance )
			=> _reflScripts.RunScriptAsync<TArgs, TResult>( clientId, scriptName, sourceCode, args, title, out scriptInstance );
		public Task<VfsNodeDef?> ResolveAsync( VfsNodeDef nodeDef, bool forceUNC, bool includeContent )
		{
			//// if node not associated with any machine, resolve on master's machine
			//if( string.IsNullOrEmpty(nodeDef.MachineId) )
			//{
			//	nodeDef = Tools.Clone( nodeDef )!;
			//	nodeDef.MachineId = _machineId;	
			//}
			return _files.ResolveAsync( _syncIDirig, nodeDef, forceUNC, includeContent, null );
		}
					
		#endregion

		public bool WantsQuit { get; set; }
		public Dictionary<AppIdTuple, AppState> AppsState => _allAppStates.AppStates;

		public TickableCollection Tickers => _tickers;
		public string RootForRelativePaths => _rootForRelativePaths;
		public Dictionary<string, string> InternalVars => _internalVars;

		#region Private fields

		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );
		private string _localIpAddr;
		private int _port;
		private Net.Server _server;
		private CLIProcessor _cliProc;
		private TelnetServer _telnetServer;
		private AllClientStateRegistry _allClientStates;
		private AllAppsStateRegistry _allAppStates;
		private AllAppsDefRegistry _allAppDefs;
		private PlanRegistry _plans;
		private ReflectedScriptRegistry _reflScripts;
		private LocalScriptRegistry _localScripts;
		private SingletonScriptRegistry _singlScripts;
		private FileRegistry _files;
		private List<MachineDef> _machineDefs = new List<MachineDef>();
		private List<AssocMenuItemDef> _menuItemDefs = new List<AssocMenuItemDef>();
		private Dictionary<AppIdTuple, AppDef> _defaultAppDefs;
		const float CLIENT_REFRESH_PERIOD = 1.0f;
		private Stopwatch _swClientRefresh;
		private TickableCollection _tickers;
		private string _sharedConfigFileName = string.Empty;
		private string _rootForRelativePaths;
		private Dictionary<string, string> _internalVars = new Dictionary<string, string>();
		CancellationTokenSource _webServerCTS;
		private Task _webServerTask; // null if no web server started
		public ScriptFactory ScriptFactory;
		public SynchronousOpProcessor SyncOps => _syncOps;
		public SynchronousOpProcessor _syncOps;
		private SynchronousIDirig _syncIDirig;
		private string _machineId; // empty if we run master standalone on an unidentified machine (this never happens as we always run master as part of some agent on a machine with a known id)
		private bool _debug = false; // do not catch exceptions etc.

		#endregion

		public Master( AppConfig ac, string rootForRelativePaths )
		{
			log.Info( $"Running Master at IP {ac.LocalIP}, port {ac.MasterPort}, cliPort {ac.CliPort}" );

			_debug = Tools.BoolFromString( ac.Debug );

			_localIpAddr = ac.LocalIP;
			_port = ac.MasterPort;

			_rootForRelativePaths = rootForRelativePaths;

			ScriptFactory = new ScriptFactory( rootForRelativePaths );
			_syncOps = new SynchronousOpProcessor();
			_syncIDirig = new SynchronousIDirig( this, _syncOps );

			_allAppStates = new AllAppsStateRegistry();
			_allClientStates = new AllClientStateRegistry();

			_allAppDefs = new AllAppsDefRegistry();
			_allAppDefs.Added += SendAppDefAddedOrUpdated;
			_allAppDefs.Updated += SendAppDefAddedOrUpdated;

			_defaultAppDefs = new Dictionary<AppIdTuple, AppDef>();

			_plans = new PlanRegistry( this );
			_plans.PlanDefUpdated += SendPlanDefUpdated;

			_reflScripts = new ReflectedScriptRegistry( this );
			_localScripts = new LocalScriptRegistry( this, this.ScriptFactory, this.SyncOps, _rootForRelativePaths );
			_singlScripts = new SingletonScriptRegistry( this, _localScripts );

			_machineId = ac.MachineId; // because we run master together with an agent, we should always know the machine id
			if (string.IsNullOrEmpty( _machineId )) throw new Exception($"MachineId not specified for Master!");

			_files = new FileRegistry(
				this,
				_machineId, // empty if we run master standalone on an unidentified machine
				_rootForRelativePaths,
				(string machineId) =>
				{
					if( _allClientStates.ClientStates.TryGetValue( machineId, out var state ) )
					{
						return state.IP;
					}
					return null;
				}
			);
 
			_server = new Server( ac.MasterPort );
			_swClientRefresh = new Stopwatch();
			_swClientRefresh.Restart();

			// start a telnet client server
			_cliProc = new CLIProcessor( this );

            log.InfoFormat("Command Line Interface running on port {0}", ac.CliPort);
			_telnetServer = new TelnetServer( "0.0.0.0", ac.CliPort, _cliProc );

			var sharedConfig = LoadSharedConfig( ac.SharedCfgFileName );
			InitFromConfig( sharedConfig );

			_tickers = new TickableCollection();

			_webServerCTS = new CancellationTokenSource();
			if( ac.HttpPort > 0 )
			{
				_webServerTask = Web.WebServerRunner.RunWebServerAsync( this, $"http://*:{ac.HttpPort}", Web.WebServerRunner.HtmlRootPath, _webServerCTS.Token );
			}

		}

		protected override void Dispose(bool disposing)
		{
			_webServerCTS.Cancel();
			if( _webServerTask != null )
				Task.WaitAll( _webServerTask );

			_singlScripts.Dispose();
			_tickers.Dispose();
			_telnetServer?.Dispose();
			_cliProc.Dispose();
			_server.Dispose();

			base.Dispose(disposing);
		}

		public void Tick()
		{
			_tickers.Tick();

			_singlScripts.Tick();
			_localScripts.Tick();
			_reflScripts.Tick();

			_plans.Tick();

			_cliProc.Tick();

			_telnetServer?.Tick();

			// process all messages received since last tick from all clients
			_server.Tick( ( msg ) =>
			{
				ProcessIncomingMessageAndHandleExceptions( msg );
			} );

			HandleDisconnectedClients();

			// periodically refresh intrested clients
			if( _swClientRefresh.Elapsed.TotalSeconds > CLIENT_REFRESH_PERIOD )
			{
				RefreshClients();
				_swClientRefresh.Restart();
			}

			SyncOps.Tick();
		}

		// Adds CLI request to be processed by the master during its next tick(s).
		// Returns the request object.
		// The caller can asynchronously await the completion of the operation (using "await operation.WaitAsync();")
		// Thread safe, can be called from async context.
		public CLIRequest AddCliRequest( ICLIClient client, string cmdLine ) => _cliProc.AddRequest( client, cmdLine );

		public SynchronousOp AddSynchronousOp( Action act )
		{
			return SyncOps.AddSynchronousOp( act );
		}

		void HandleDisconnectedClients()
		{
			// build a dict of all connected clients
			var connected = new Dictionary<string, int>(200);
			foreach( var cl in _server.Clients )
			{
				connected[cl.Name] = 1;
			}

			// clear connected flag for all client's states not found among the connected ones
			foreach( (var id, var state) in _allClientStates.ClientStates )
			{
				if( !connected.ContainsKey( id ) )
				{
					state.Connected = false;
				}
			}
		}

		void ProcessIncomingMessage( Message msg )
		{
			Type t = msg.GetType();

			//if( t != typeof(AppsStateMessage)) // do not log frequent messages; UPDATE: this msg is optionally replaced by UDP so should be rare or non-existing
			//{
			//    log.Debug( $"Incoming: {msg}" );
			//}

			switch( msg )
			{

				case ClientIdent m:
				{
					// client connected!
					OnClientIdentified( m );

					// get its IP address  port
					string ipAddress = String.Empty;
					int port = 0;
					var socket = _server.GetClientSocket( m.Name );
					if( socket != null )
					{
						Tools.GetRemoteIpAndPort( socket, out ipAddress, out port );
					}

					// remember initial client state as connected from certain IP address
					var cs = new ClientState();
					cs.Ident = m;
					cs.Connected = true;
					cs.LastChange = DateTime.UtcNow;
					cs.IP = ipAddress;
					_allClientStates.AddOrUpdate( m.Name, cs, ipAddress );

					break;
				}

				case ClientStateMessage m:
				{
					if( m.State != null && m.State.Ident != null) // sanity check
					{
						m.State.Connected = true;  // we just received from the client, so it must be connected
						m.State.LastChange = DateTime.UtcNow; // we just received from the client, so it must be fresh
						_allClientStates.AddOrUpdate( m.State.Ident.Name, m.State );
					}
					break;
				}

				// agent is sending the state of its apps
				case AppsStateMessage m:
				{
					//Debug.Assert( m.AppsState is not null );
					if( m.AppsState is not null )
					{
						var localTimeDelta = DateTime.UtcNow - m.TimeStamp;

						foreach( var( appId, appState ) in m.AppsState )
						{
							appState.LastChange += localTimeDelta; // recalc to master's time
							
							_allAppStates.AddOrUpdate( appId, appState );
						}
					}
					break;
				}

				case StartAppMessage m:
				{
					var vars = m.UseVars ? m.Vars ?? new() : null; // pass null if not vars change is required
					StartApp( m.Sender, m.Id, m.PlanName, m.Flags, vars );
					break;
				}

				case KillAppMessage m:
				{
					KillApp( m.Sender, m.Id, m.Flags );
					break;
				}

				case RestartAppMessage m:
				{
					var vars = m.UseVars ? m.Vars ?? new() : null; // pass null if not vars change is required
					RestartApp( m.Sender, m.Id, vars );
					break;
				}

				case CLIRequestMessage m:
				{
					var cliClient = new CLIClient( _server, m.Sender );
					AddCliRequest( cliClient, m.Text );
					break;
				}

				case StartPlanMessage m:
				{
					var vars = m.UseVars ? m.Vars ?? new() : null; // pass null if not vars change is required
					StartPlan( m.Sender, m.PlanName, vars );
					break;
				}

				case StopPlanMessage m:
				{
					StopPlan( m.Sender, m.PlanName );
					break;
				}

				case KillPlanMessage m:
				{
					KillPlan( m.Sender, m.PlanName );
					break;
				}

				case RestartPlanMessage m:
				{
					var vars = m.UseVars ? m.Vars ?? new() : null; // pass null if not vars change is required
					RestartPlan( m.Sender, m.PlanName, vars );
					break;
				}

				case SetAppEnabledMessage m:
				{
					SetAppEnabled( m.Sender, m.PlanName, m.Id, m.Enabled );
					break;
				}

				case KillAllMessage m:
				{
					KillAll( m.Sender, m.Args );
					break;
				}

				case ReloadSharedConfigMessage m:
				{
					ReloadSharedConfig( m.Sender, m.Args );
					break;
				}

				case RemoteOperationErrorMessage m:
				{
					// agent is sending an error - forward to the requestor
					if( !string.IsNullOrEmpty( m.Requestor ) )
					{
						_server.SendToSingle( m, m.Requestor );
					}
					break;
				}

				case UserNotificationMessage m:
				{
					// forward if sent from non-master
					if( m.Sender != "" && !string.IsNullOrEmpty( m.HostClientId ) )
					{
						_server.SendToSingle( m, m.HostClientId );
					}
					break;
				}

				case StartScriptMessage m:
				{
					if( string.IsNullOrEmpty(m.HostClientId) ) // request for us (master)
					{
						if( string.IsNullOrEmpty(m.ScriptName) ) // is it a ScriptDef based single-instance script?
						{
							StartSingletonScript( m.Requestor, m.Instance, Tools.Deserialize<string?>(m.Args) );
						}
						else // it is a generic script
						{
							_localScripts.Start( m.Instance, m.ScriptName, m.SourceCode, m.Args, m.Title, m.Requestor );
						}
					}
					else if( m.Sender != "" ) // forward to the target client
					{
						_server.SendToSingle( m, m.HostClientId );
					}
					break;
				}

				case KillScriptMessage m:
				{
					KillScript( m.Sender, m.Instance );
					// msg not saying where the script runs, send to everyone
					_server.SendToAllSubscribed( m, EMsgRecipCateg.All );
					break;
				}

				case SetVarsMessage m:
				{
					SetVars( m.Sender, m.Vars );
					break;
				}

				case TerminateMessage m:
				{
					Terminate( m.Sender, m.Args, m.MachineId );
					break;
				}

				case ShutdownMessage m:
				{
					Shutdown( m.Sender, m.Args, m.MachineId );
					break;
				}

				case ApplyPlanMessage m:
				{
					ApplyPlan( m.Sender, m.PlanName, m.AppIdTuple );
					break;
				}

				case SelectPlanMessage m:
				{
					SelectPlan( m.Sender, m.PlanName );
					break;
				}

				case SetWindowStyleMessage m:
				{
					// forward to all
					_server.SendToAllSubscribed( m, EMsgRecipCateg.Agent );
					break;
				}

				case ScriptStateMessage m:
				{
					_reflScripts.UpdateScriptState( m.Instance, m.State );
						
					// forward to others (if it was sent from non-master)
					if( m.Sender != "" )
					{
						_server.SendToAllSubscribed( m, EMsgRecipCateg.All );
					}
					break;
				}

				case MachineStateMessage m:
				{
					// forward to others (if it was sent from non-master)
					if( m.Sender != "" )
					{
						_server.SendToAllSubscribed( m, EMsgRecipCateg.All );
					}
					break;
				}
				
				case RunActionMessage m:
				{
					// forward to others
					if( m.Sender != "" ) // avoid sending twice is sent from master
					{
						_server.SendToAllSubscribed( m, EMsgRecipCateg.All );
					}
					break;
				}
				
			}

		}

		void ProcessIncomingMessageAndHandleExceptions( Message msg )
		{
			if( _debug )
			{
				ProcessIncomingMessage( msg );
			}
			else
			{
				try
				{
					ProcessIncomingMessage( msg );
				}
				catch( Exception ex )
				{
					var errText = $"Exception\n\n'{ex.Message}'\n\nwhen processing message '{msg}'";
					log.Error(errText, ex);
					
					// send error back to the sender
					// note: if the sender is an agent and not a GUI, we won't see anything...
					_server.SendToSingle( new RemoteOperationErrorMessage( msg.Sender, errText ), msg.Sender );
				}
			}
		}

		/// <summary>
		/// Sends the response over Dirigent's network back to the request sender 
		/// Created specifically for each single request.
		/// </summary>
		private class CLIClient : ICLIClient
		{
			Server _server;
			string _requestor;
			public string Name => "<master>";

			public CLIClient( Server server, string requestor )
			{
				_server = server;
				_requestor = requestor;
			}
			public void WriteResponse(string text)
			{
				if( _server.IsDisposed ) return;
				_server.SendToSingle( new Net.CLIResponseMessage( text ), _requestor );
			}
		}

		// send all initial data do all connected clients
		void FeedAllClients()
		{
			foreach( var cl in _server.Clients )
			{
				FeedSingleClient( cl );
			}
		}

		void FeedSingleClient( ClientIdent ident )
		{
			if( ident.IsGui )
			{
				FeedGui( ident );
			}

			if( ident.IsAgent )
			{
				FeedAgent( ident );
			}
		}
		

		// Called once when client connects and sends ClientIdent
		void OnClientIdentified( ClientIdent ident )
		{
			FeedSingleClient( ident );
		}

		void FeedGui( ClientIdent ident )
		{
			// send the full list of clients
			{
				foreach( (var id, var state) in _allClientStates.ClientStates )
				{
					var m = new Net.ClientStateMessage(DateTime.UtcNow, state);
					_server.SendToSingle( m, ident.Name );
				}
			}

			// send the full list of plans
			{
				var m = new Net.PlanDefsMessage( from p in _plans.Plans.Values select p.Def, incremental: false );
				_server.SendToSingle( m, ident.Name );
			}

			// send the full list of plan states
			{
				var m = new Net.PlansStateMessage( _plans.PlanStates );
				_server.SendToSingle( m, ident.Name );
			}

			// send the full list of app states
			{
				var m = new Net.AppsStateMessage( _allAppStates.AppStates, DateTime.UtcNow );
				_server.SendToSingle( m, ident.Name );
			}

			// send the full list of app defs
			{
				var m = new Net.AppDefsMessage( _allAppDefs.AppDefs.Values, incremental: false );
				_server.SendToSingle( m, ident.Name );
			}

			// send the full list of scripts
			{
				var m = new Net.ScriptDefsMessage( from p in _singlScripts.Scripts.Values select p.Def, incremental: false );
				_server.SendToSingle( m, ident.Name );
			}

			// send the full list of script states
			{
				foreach (var (inst, state) in _localScripts.ScriptStates)
				{
					var m = new Net.ScriptStateMessage( inst, state );
					_server.SendToSingle( m, ident.Name );
				}
			}

			// send full list of VFS nodes
			{
				var m = new Net.VfsNodesMessage( _files.VfsNodes.Values );
				_server.SendToSingle( m, ident.Name );
			}

			// send full list of machines
			{
				var m = new Net.MachineDefsMessage( _machineDefs );
				_server.SendToSingle( m, ident.Name );
			}

			// send full list of actions
			{
				var m = new Net.MenuItemDefsMessage( _menuItemDefs, false );
				_server.SendToSingle( m, ident.Name );
			}

		}

		void FeedAgent( ClientIdent ident )
		{
			// send list of app defs belonging to the just connected agent
			{
				var appDefs = from ad in _allAppDefs.AppDefs.Values
								where ad.Id.MachineId == ident.Name
								select ad;
				var m = new Net.AppDefsMessage( appDefs, incremental: false );
				_server.SendToSingle( m, ident.Name );
			}
		}


		/// <summary>
		/// Sends current runtime status of apps and plans to subcribed clients
		/// </summary>
		void RefreshClients()
		{
			// clients
			{
				foreach( (var id, var state) in _allClientStates.ClientStates )
				{
					var m = new Net.ClientStateMessage(DateTime.UtcNow, state);
					_server.SendToAllSubscribed( m, EMsgRecipCateg.All );
				}
			}

			// apps
			{
				var m = new Net.AppsStateMessage( _allAppStates.AppStates, DateTime.UtcNow );
				_server.SendToAllSubscribed( m, EMsgRecipCateg.Gui );
			}

			// plans
			{
				var m = new Net.PlansStateMessage( _plans.PlanStates );
				_server.SendToAllSubscribed( m, EMsgRecipCateg.Gui );
			}

			// we are sending script state on change only, from the async script execution
			//// scripts
			//{
			//	foreach (var (inst, state) in _localScripts.ScriptStates)
			//	{
			//		var m = new Net.ScriptStateMessage( inst, state );
			//		_server.SendToAllSubscribed( m, EMsgRecipCateg.Gui );
			//	}
			//}
		}

		SharedConfig LoadSharedConfig( string fileName )
		{
			SharedConfig sharedConfig;
			log.DebugFormat( "Loading shared config file '{0}'", fileName );
			sharedConfig = new SharedConfigReader( System.IO.File.OpenText( fileName ) ).Config;
			_sharedConfigFileName = fileName;
			return sharedConfig;
		}

		void InitFromConfig( SharedConfig sharedConfig )
		{
			// import plans
			_plans.SetAll( sharedConfig.Plans );

			// gather apps from all plans; appdefs from the recent plan win
			var allAppDefs = new Dictionary<AppIdTuple, AppDef>();
			foreach( var p in _plans.Plans.Values )
			{
				foreach( var ad in p.Def.AppDefs )
				{
					allAppDefs[ad.Id] = ad;
				}
			}

			// last use free/defaults app defs to initially override those from plans
			foreach( var ad in sharedConfig.AppDefaults )
			{
				_defaultAppDefs[ad.Id] = ad;
				allAppDefs[ad.Id] = ad;
			}

			_allAppDefs.SetAll( allAppDefs.Values );
			_allAppStates.SetDefault( allAppDefs.Values );


			// import predefined scripts
			_reflScripts.SetScriptDefs( sharedConfig.SingleInstScripts );
			_singlScripts.SetAll( sharedConfig.SingleInstScripts );

			_files.SetVfsNodes( sharedConfig.VfsNodes );
			_files.SetMachines( sharedConfig.Machines );

			_machineDefs = sharedConfig.Machines;
			_menuItemDefs = sharedConfig.MainMenuItems;

			// reset
			var m = new Net.ResetMessage();
			_server.SendToAllSubscribed( m, EMsgRecipCateg.All );

			// send the new info to all connected clients
			FeedAllClients();
		}

		/// <summary>
		/// Sends updated app def to their respective agent
		/// This happens when the appdef changes because of plan switch etc.
		/// </summary>
		/// <param name="appDef"></param>
		void SendAppDefAddedOrUpdated( AppDef ad )
		{
			foreach( var cl in _server.Clients )
			{
				if( cl.Name == ad.Id.MachineId || cl.IsGui )
				{
					var m = new Net.AppDefsMessage( new AppDef[1] { ad }, incremental: true );
					_server.SendToSingle( m, cl.Name );
				}
			}
		}

		void SendPlanDefUpdated( PlanDef pd )
		{
			foreach( var cl in _server.Clients )
			{
				if( cl.IsGui )
				{
					var m = new Net.PlanDefsMessage( new PlanDef[1] { pd }, incremental: true );
					_server.SendToSingle( m, cl.Name );
				}
			}
		}


		/// <summary>
		/// Updates appDefs of all apps in the plan to those from the plan
		/// </summary>
		void ApplyPlanToAllApps( Plan plan )
		{
			log.Debug($"Applying plan {plan.Name} to all apps from the plan");
			foreach( var ad in plan.Def.AppDefs )
			{
				_allAppDefs.AddOrUpdate( ad );
			}
		}

		/// <summary>
		/// Updates appDefs of given app to the one from the plan
		/// </summary>
		void ApplyPlanToSingleApp( Plan plan, AppIdTuple appIdTuple )
		{
			log.Debug($"Applying plan {plan.Name} to single app {appIdTuple}");
			var planApp = plan.FindApp( appIdTuple );
			_allAppDefs.AddOrUpdate( planApp.Def );
		}

		/// <summary>
		/// Launches given app by sending LaunchApp command directly to owning agent.
		/// Throws on failure.
		/// </summary>
		/// <param name="id">App to run</param>
		/// <param name="planName">The plan the app belongs to. null=none (use default app settings), Empty=current plan, non-empty=specific plan name.</param>
		/// <param name="vars">Additional env vars to be set for a process; also set as local vars for use in macro expansion. Null=no change from previously used vars.</param>
		public void StartApp( string requestorId, AppIdTuple id, string? planName, Net.StartAppFlags flags=0, Dictionary<string,string>? vars=null )
		{
			// load app def from given plan if a plan is specified
			if( !String.IsNullOrEmpty(planName) )
			{
				var app = _plans.FindAppInPlan( planName, id );

				// this sends app def to agent if different from the previous
				_allAppDefs.AddOrUpdate( app.Def );
			}
			else// empty plan name = load from default app defs
			if( planName is not null && String.IsNullOrEmpty(planName) )
			{
				if( _defaultAppDefs.TryGetValue( id, out var appDef ) )
				{
					_allAppDefs.AddOrUpdate( appDef );
				}
			}
			else // plan name is null, keep recently used app def
			{
				// nothing to do, app defs are already loaded, no change
			}

			// if starting an app from plan, it is actually applying the plan to the app, so set the flag accordingly
			// (if the plan is already running and this app is set "disabled" and is later started manually, it might help to satisfy the plan)
			if( !string.IsNullOrEmpty(planName) )
			{
				var plan = _plans.FindPlan( planName );
				plan.SetPlanApplied( id );
			}

			// send app start command
			var msg = new Net.StartAppMessage( requestorId, id, planName, flags, vars );
			_server.SendToSingle( msg, id.MachineId );
		}

		/// <summary>
		/// Send app kill command directly to owning agent
		/// </summary>
		public void KillApp( string requestorId, AppIdTuple id, KillAppFlags flags=0 )
		{
			var msg = new Net.KillAppMessage( requestorId, id, flags );
			_server.SendToSingle( msg, id.MachineId );
		}

		/// <summary>
		/// Sends app restart command directly to owning agent.
		/// </summary>
		/// <param name="vars">Additional env vars to be set for a process; also set as local vars for use in macro expansion. Null=no change from previously used vars.</param>
		public void RestartApp( string requestorId, AppIdTuple id, Dictionary<string,string>? vars=null )
		{
			var msg = new Net.RestartAppMessage( requestorId, id, vars );
			_server.SendToSingle( msg, id.MachineId );
		}

		/// <param name="vars">Additional env vars to be set for a process; also set as local vars for use in macro expansion. Null=no change from previously used vars.</param>
		public void StartPlan( string requestorId, string planName, Dictionary<string,string>? vars=null )
		{
			var plan = _plans.FindPlan( planName ); // throws on error
			plan.Start( requestorId, vars );
		}

		public void StopPlan( string requestorId, string planName )
		{
			var plan = _plans.FindPlan( planName ); // throws on error
			plan.Stop( requestorId );
		}

		public void KillPlan( string requestorId, string planName )
		{
			var plan = _plans.FindPlan( planName ); // throws on error
			plan.Kill( requestorId );
		}

		/// <param name="vars">Additional env vars to be set for a process; also set as local vars for use in macro expansion. Null=no change from vars used last time when app was started.</param>
		public void RestartPlan( string requestorId, string planName, Dictionary<string,string>? vars=null )
		{
			var plan = _plans.FindPlan( planName ); // throws on error
			plan.Restart( requestorId, vars );
		}

		public void SetAppEnabled( string requestorId, string? planName, AppIdTuple id, bool enabled )
		{
			if( planName is null ) return;

			var app = _plans.FindAppInPlan( planName, id ); // throws on error

            // find the appdef within the plan
            var ad = app.Def;

            // change the enabled flag in plan's appDef
            ad.Disabled = !enabled;

			// we need to comunicate the appDef change to Guis so they show it
			// agents will get the appdef as soon as the app gets next time started
			_plans.AppDefUpdated( planName, id );

		}

		// format of string: VAR1=VALUE1::VAR2=VALUE2
		public void SetVars( string requestorId, string vars )
		{
			var msg = new Net.SetVarsMessage( requestorId, vars );
			_server.SendToAllSubscribed( msg, EMsgRecipCateg.Agent );
		}

		public void KillAll( string requestorId, KillAllArgs args )
		{
			// stop all plans
			foreach( var p in _plans.Plans.Values )
			{
				p.Stop( requestorId );
			}

			// kill all apps
			foreach( var ad in _allAppDefs.AppDefs.Values )
			{
				if( string.IsNullOrEmpty(args.MachineId) || ad.Id.MachineId == args.MachineId )
				{
					// we reset the app state so the Initialized goes to 0,
					// causing the next StartPlan to start from scratch
					// (instead of thinking that some apps have already been initialized)
					KillApp( requestorId, ad.Id, KillAppFlags.ResetAppState );
				}
			}

			// kill all running scripts
			foreach( var (id, state) in _reflScripts.GetAllScriptStates() )
			{
				KillScript( requestorId, id );
			}
		}

		public void ReloadSharedConfig( string requestorId, ReloadSharedConfigArgs args )
		{
			var fileName = !string.IsNullOrEmpty(args.FileName) ? args.FileName : _sharedConfigFileName;
			
			// load (may throw an exception on error)
			var sharedConfig = LoadSharedConfig( fileName );

			if( args.KillApps )
			{
				// send kill to all
				KillAll( requestorId, new KillAllArgs() );
			}

			// wait a while to give the apps the time to die
			Thread.Sleep(3000);

			// reinit from shared config
			InitFromConfig( sharedConfig );
		}

		public void Terminate( string requestorId, TerminateArgs args, string? machineId )
		{
			var msg = new Net.TerminateMessage( requestorId, args, machineId );
			_server.SendToAllSubscribed( msg, EMsgRecipCateg.All );
		}

		public void Shutdown( string requestorId, ShutdownArgs args, string? machineId )
		{
			var msg = new Net.ShutdownMessage( requestorId, args, machineId );
			_server.SendToAllSubscribed( msg, EMsgRecipCateg.All );
		}

		public void Reinstall( string requestorId, ReinstallArgs args )
		{
			var msg = new Net.ReinstallMessage( requestorId, args );
			_server.SendToAllSubscribed( msg, EMsgRecipCateg.All );
		}


		public void StartSingletonScript( string? requestorId, Guid id, string? args )
		{
			_singlScripts.StartScript( requestorId, id, args );
		}

		public void StartSingletonScript( string requestorId, string scriptIdWithArgs )
		{
			if ( string.IsNullOrEmpty( scriptIdWithArgs ) ) return;

			var (id, args) = Tools.ParseScriptIdArgs( scriptIdWithArgs );
			if ( string.IsNullOrEmpty( id ) ) return;

			StartSingletonScript( requestorId, Guid.Parse(id), args );
		}

		public void KillScript( string requestorId, Guid id )
		{
			if( _singlScripts.Contains( id ) )
			{
				_singlScripts.KillScript( requestorId, id );
			}
			else
			if( _localScripts.Scripts.ContainsKey( id ) )
			{
				_localScripts.Stop( id );
			}
			else  // not our script, forward kill request to all others
			{
				_server.SendToAllSubscribed( new KillScriptMessage( requestorId, id ), EMsgRecipCateg.All );
			}
		}

		public ScriptState? GetScriptState( string requestorId, Guid id )
		{
			return _reflScripts.GetScriptState( id );
		}

		public void ApplyPlan( string requestorId, string planName, AppIdTuple appIdTuple )
		{
			var plan = _plans.FindPlan( planName ); // throws on error
			if (appIdTuple.IsEmpty())
			{
				ApplyPlanToAllApps( plan );  // throws on error
			}
			else
			{
				ApplyPlanToSingleApp( plan, appIdTuple );  // throws on error
			}
		}

		public void SelectPlan( string requestorId, string planName )
		{
			// remember what plan is selected on what client (remember: we do not use the ClientState message as it causes StackOverflow)
			if( _allClientStates.ClientStates.TryGetValue( requestorId, out var clientState ) )
			{
				clientState.SelectedPlanName = planName;
			}

			// apply the plan if said so in the plan def

			if( string.IsNullOrEmpty(planName) )
				return;

			var plan = _plans.FindPlan( planName ); // throws on error
			if( plan.Def.ApplyOnSelect )
			{
				ApplyPlanToAllApps( plan );
			}
		}

		
	}
}
