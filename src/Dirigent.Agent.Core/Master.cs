using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dirigent.Net;
using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Concurrent;

namespace Dirigent
{
	public class Master : Disposable, IDirig
	{

		#region IDirig interface

		public ClientState? GetClientState( string Id ) { if( _allClientStates.ClientStates.TryGetValue(Id, out var x)) return x; else return null; }
		public IEnumerable<KeyValuePair<string, ClientState>> GetAllClientsState() { return _allClientStates.ClientStates; }
		public AppState? GetAppState( AppIdTuple Id ) { if( _allAppStates.AppStates.TryGetValue(Id, out var x)) return x; else return null; }
		public IEnumerable<KeyValuePair<AppIdTuple, AppState>> GetAllAppsState() { return _allAppStates.AppStates; }
		public PlanState? GetPlanState( string Id ) { if( _plans.Plans.TryGetValue(Id, out var x)) return x.State; else return null; }
		public IEnumerable<KeyValuePair<string, PlanState>> GetAllPlansState() { return from x in _plans.Plans.Values select new KeyValuePair<string, PlanState>( x.Name, x.State ); }
		public MachineDef? GetMachineDef( string Id ) { return _machineDefs.Find( x => x.Id == Id ); }
		public IEnumerable<MachineDef> GetAllMachinesDef() { return _machineDefs; }
		public ScriptState? GetScriptState( Guid Id ) { return _reflScripts.GetScriptState( Id ); }
		public IEnumerable<KeyValuePair<Guid, ScriptState>> GetAllScriptsState() { return _reflScripts.GetAllScriptsState(); }
		public AppDef? GetAppDef( AppIdTuple Id ) { if( _allAppDefs.AppDefs.TryGetValue(Id, out var x)) return x; else return null; }
		public IEnumerable<KeyValuePair<AppIdTuple, AppDef>> GetAllAppsDef() { return _allAppDefs.AppDefs; }
		public PlanDef? GetPlanDef( string Id ) { if( _plans.Plans.TryGetValue( Id, out var p ) ) return p.Def; else return null; }
		public IEnumerable<PlanDef> GetAllPlansDef() { return from x in _plans.Plans.Values select x.Def; }
		public ScriptDef? GetScriptDef( Guid Id ) { return _reflScripts.ScriptDefs.Find((x) => x.Guid == Id); }
		public IEnumerable<ScriptDef> GetAllScriptsDef() { return _singlScripts.Scripts.Values.Select(e => e.Def); } // this include both static + dynamic definitions
		public VfsNodeDef? GetVfsNodeDef( Guid guid ) { return _files.GetVfsNodeDef(guid); }
		public IEnumerable<VfsNodeDef> GetAllVfsNodesDef() { return _files.GetAllVfsNodesDef(); }
		public string Name => string.Empty;

		/// <summary>The machine the master runs on; always known, unlike the client name.</summary>
		public string MachineId => _machineId;

		// ---- what describes this master to a tool: GET /api/status ----

		/// <summary>When this master started (UTC).</summary>
		public DateTime StartedAt { get; } = DateTime.UtcNow;

		/// <summary>The port of the web server; 0 or less when there is none.</summary>
		public int HttpPort { get; private set; }

		/// <summary>The local config file, if one was named; empty otherwise.</summary>
		public string LocalConfigFile { get; private set; } = string.Empty;

		/// <summary>The shared config file in use.</summary>
		public string SharedConfigFile => _sharedConfigFileName;

		/// <summary>When the shared config was last read (UTC).</summary>
		public DateTime SharedConfigLoadedAt { get; private set; }

		/// <summary>The MinDirigentVersion the shared config states; empty if it states none.</summary>
		public string MinDirigentVersion { get; private set; } = string.Empty;

		/// <summary>The name of the site, <c>&lt;Shared Name="..."&gt;</c>; empty if the shared config gives none.</summary>
		public string SiteName { get; private set; } = string.Empty;

		/// <summary>The site state for a collected archive - see <see cref="IDirig.ComposeSiteState"/>. Master thread only.</summary>
		public Task<string?>? ComposeSiteState( SiteStateExtras extras ) => Web.SiteStateDocument.Start( this, extras );

		/// <summary>True while a Kill All is still killing.</summary>
		public bool KillAllInProgress => _killAllInProgress;

		/// <summary>The address the master is reached at, as configured.</summary>
		public string LocalIp => _localIpAddr;

		/// <summary>The MACs the machine's agent last reported, for Wake-on-LAN.</summary>
		public List<string> GetLearnedMacs( string machineId )
			=> _macCache.Get( machineId ).Select( n => n.Mac ).Where( m => !string.IsNullOrEmpty( m ) ).Distinct().ToList();
		
		/// <summary>
		/// Send to all subcribed. Does not change the sender.
		/// This might be called from asyn context!
		/// </summary>
		/// <param name="msg"></param>
		public void Send( Net.Message msg )
		{
			// A message for one client goes to that client only. It used to go to everyone, and every receiver threw
			// away what was not for it: a script started on one machine (with its arguments - a resolved file tree can
			// be tens of KB) crossed the network once per connected agent and GUI.
			var target = msg switch
			{
				StartScriptMessage m => m.HostClientId,
				RunActionMessage m => m.HostClientId,
				UserNotificationMessage m => m.HostClientId,
				_ => null,
			};

			if( !string.IsNullOrEmpty( target ) )
				_server.SendToSingle( msg, target );
			else if( msg is ScriptStateMessage ss )
				RouteScriptState( ss );
			else
				// send to everyone else
				_server.SendToAllSubscribed( msg, EMsgRecipCateg.All );
			// process also on master
			_server.BufferMessageReceived( msg );
		}
		public Task<TResult?> RunScriptAsync<TArgs, TResult>( string clientId, string scriptName, string? sourceCode, TArgs? args, string title, out Guid scriptInstance )
			=> _reflScripts.RunScriptAsync<TArgs, TResult>( clientId, scriptName, sourceCode, args, title, out scriptInstance );
		public Task<VfsNodeDef?> ResolveAsync( VfsNodeDef nodeDef, bool forceUNC, bool includeContent, FileWindow? window = null )
		{
			//// if node not associated with any machine, resolve on master's machine
			//if( string.IsNullOrEmpty(nodeDef.MachineId) )
			//{
			//	nodeDef = Tools.Clone( nodeDef )!;
			//	nodeDef.MachineId = _machineId;	
			//}
			return _files.ResolveAsync( _syncIDirig, nodeDef, forceUNC, includeContent, null, window );
		}
					
		#endregion

		/// <summary>
		/// A path local to the given machine as a UNC path through one of that machine's shares,
		/// using the master's share table. Throws if no share covers the path.
		/// </summary>
		public string MakeUNC( string path, string? machineId, string whatFor )
			=> _files.MakeUNC( path, machineId, whatFor );

		/// <summary>The download jobs of REST and the CLI.</summary>
		public DownloadJobs DownloadJobs => _downloadJobs;

		/// <summary>The last unexpected exits of each app, kept in memory (lost when the master restarts).</summary>
		public Crashes.ExitHistory ExitHistory { get; } = new();

		public bool WantsQuit { get; set; }

		/// <summary>How the master port and the web API are secured; <see cref="SecuritySettings.Off"/> for neither.</summary>
		public SecuritySettings Security { get; private set; } = SecuritySettings.Off;
		public Dictionary<AppIdTuple, AppState> AppsState => _allAppStates.AppStates;

		public TickableCollection Tickers => _tickers;
		public string RootForRelativePaths => _rootForRelativePaths;
		public Dictionary<string, string> InternalVars => _internalVars;

		#region Private fields

		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );
		private string _localIpAddr;
		private int _port;
        private Server _server;

		/// <summary>What the master's server sent and received, by message type (<c>GET /api/status</c>). Its own instance, so that a process holding several masters or clients counts them apart.</summary>
		public Net.TrafficStats Traffic { get; } = new();
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
		private MachineMacCache _macCache;
		private DownloadJobs _downloadJobs;
		private List<MachineDef> _machineDefs = new List<MachineDef>();
		private List<AssocMenuItemDef> _menuItemDefs = new List<AssocMenuItemDef>();
		private List<ActionDef> _killAllExtras = new List<ActionDef>();
		private Dictionary<AppIdTuple, AppDef> _defaultAppDefs;
		const float CLIENT_REFRESH_PERIOD = 1.0f;

		/// <summary>How often everything is sent again, whatever has or has not changed: a safety net under the changes-only feed.</summary>
		const double FULL_FEED_PERIOD = 30.0;

		/// <summary>An agent not heard from for this long is taken to be gone, and its apps offline.</summary>
		static readonly TimeSpan AGENT_SILENCE_LIMIT = TimeSpan.FromSeconds( 3 );

		// what was last sent to the feed, to send what has changed since
		private readonly Dictionary<AppIdTuple, AppState> _feedSentApps = new();
		private readonly Dictionary<string, PlanState> _feedSentPlans = new();
		private readonly Dictionary<string, ClientState> _feedSentClients = new();
		private DateTime _lastFullFeed = DateTime.UtcNow;

		/// <summary>When each client was last heard from (any message), by client name.</summary>
		private readonly Dictionary<string, DateTime> _lastHeard = new();

		/// <summary>Who asked for each script that is running: its full state, with the result data, goes to nobody else.</summary>
		private readonly Dictionary<Guid, string> _scriptRequestors = new();
		private Stopwatch _swClientRefresh;
		private TickableCollection _tickers;
		private string _sharedConfigFileName = string.Empty;
		private string _rootForRelativePaths;
		private Dictionary<string, string> _internalVars = new Dictionary<string, string>();
		CancellationTokenSource _webServerCTS;
		private Task? _webServerTask; // null if no web server started
		public ScriptFactory ScriptFactory;
		public SynchronousOpProcessor SyncOps => _syncOps;
		public SynchronousOpProcessor _syncOps;
		private SynchronousIDirig _syncIDirig;
		private string _machineId; // empty if we run master standalone on an unidentified machine (this never happens as we always run master as part of some agent on a machine with a known id)
		private bool _debug = false; // do not catch exceptions etc.

		// set to true to during the KillAll operation;
		//   back to false either when all apps are killed or when the KillAll operation times out
		bool _killAllInProgress;
		
		// when to timeout the KillAll operation; used only during the KillAll operation
		DateTime _killAllMaxTime;


		#endregion

        public Master( AppConfig ac, string rootForRelativePaths )
        {
            log.Info( $"Running Master at IP {ac.LocalIP}, port {ac.MasterPort}, cliPort {ac.CliPort}" );
            InitializeCommon( ac, rootForRelativePaths );

            // Production server: plain TCP, or TLS and the site secret when security is on (see docs/Security.md)
            var security = ac.LoadSecurity();
            Security = security;
            log.Info( security.Secured ? $"The master port is secured (Security={security.Mode})." : "The master port is not secured (Security=off)." );
            _server = new Server( ac.MasterPort, Traffic, security );

            // Telnet/CLI: any command, from anybody who reaches the port. Off with --CLIPort off, and when security is on unless a port is named.
            if( ac.CliEnabled )
            {
                log.InfoFormat("Command Line Interface running on port {0}", ac.CliPort);
                if( ac.CliUnauthenticatedWhileSecured )
                    log.Warn( $"The telnet CLI runs on port {ac.CliPort} although security is on: it runs every command for anybody who can reach that port, with no check. Use --CLIPort off to switch it off." );
                _telnetServer = new TelnetServer( "0.0.0.0", ac.CliPort, _cliProc );
            }
            else
            {
                log.Info( ac.CliOff ? "The Command Line Interface is off (CLIPort=off)." : "The Command Line Interface is off: security is on and no CLIPort is named." );
            }

            // Load shared config from file
            var sharedConfig = LoadSharedConfig( ac.SharedCfgFileName );
            InitFromConfig( sharedConfig );

            // Web server
            if( ac.HttpPort > 0 )
            {
                _webServerTask = Web.WebServerRunner.RunWebServerAsync( this, $"http://*:{ac.HttpPort}", Web.WebServerRunner.HtmlRootPath, _webServerCTS.Token );
            }
        }

        // Shared initialization, kept from the refactor that introduced it
        [MemberNotNull(
            nameof(_localIpAddr), nameof(_allClientStates), nameof(_allAppStates), nameof(_allAppDefs), nameof(_plans),
            nameof(_reflScripts), nameof(_localScripts), nameof(_singlScripts), nameof(_files), nameof(_macCache), nameof(_downloadJobs), nameof(_defaultAppDefs),
            nameof(_swClientRefresh), nameof(_tickers), nameof(_rootForRelativePaths), nameof(_webServerCTS),
            nameof(ScriptFactory), nameof(_syncOps), nameof(_syncIDirig), nameof(_machineId), nameof(_cliProc)
        )]
        void InitializeCommon( AppConfig ac, string rootForRelativePaths )
        {
            _debug = Tools.BoolFromString( ac.Debug );

            _localIpAddr = ac.LocalIP;
			HttpPort = ac.HttpPort;
			LocalConfigFile = ac.LocalCfgFileName ?? string.Empty;
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

            _machineId = ac.MachineId;
            if( string.IsNullOrEmpty( _machineId ) ) throw new Exception( $"MachineId not specified for Master!" );

            // what agents report of their adapters, for Wake-on-LAN
            _macCache = new MachineMacCache( ac.AgentStatusFolder );

            // the collections requested over REST and the CLI, and the folder they are built in
            _downloadJobs = new DownloadJobs( DownloadJobs.FolderFrom( ac ), ac.ArchiveRetentionHours, ac.ArchiveMaxBytes,
                                              ac.ServeArchives != "0", this );
            try { _downloadJobs.Cleanup(); }
            catch( Exception e ) { log.Warn( $"Could not clean up the download archives: {e.Message}" ); }

            _files = new FileRegistry(
                this,
                _machineId,
                _rootForRelativePaths,
                (string machineId) =>
                {
                    if( _allClientStates.ClientStates.TryGetValue( machineId, out var state ) )
                    {
                        return state.IP;
                    }
                    return null;
                },
                ac.DownloadFolder
            );

            _files.MasterIP = ac.MasterIP;

            // the master resolves what is on its own machine (and, in a test bed, everything); the marks of those files
            // are kept by the agents, which write them out - so the master reads what they wrote
            var markReaders = new Dictionary<string, FileMarkStore>();
            var agentStatusFolder = ac.AgentStatusFolder;
            _files.MarkStores = machine =>
            {
                lock( markReaders )
                {
                    if( !markReaders.TryGetValue( machine, out var store ) )
                        markReaders[machine] = store = new FileMarkStore( machine, agentStatusFolder );
                    store.ReloadIfChanged();
                    return store;
                }
            };

            _swClientRefresh = new Stopwatch();
            _swClientRefresh.Restart();

            _cliProc = new CLIProcessor( this );
            _cliProc.MaxProcessingTimePerTickMs = (int)(ac.MasterTickPeriod * 0.30);

            _tickers = new TickableCollection();
            _webServerCTS = new CancellationTokenSource();
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
			_downloadJobs.Tick();
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

			UpdateAppsFreshness();

			// periodically refresh intrested clients
			if( _swClientRefresh.Elapsed.TotalSeconds > CLIENT_REFRESH_PERIOD )
			{
				RefreshClients();
				_swClientRefresh.Restart();
			}

			SyncOps.Tick();

			CheckKillAllFinished();
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

		/// <summary>
		/// Runs a script defined in shared config. Allows overriding the arguments.
		/// </summary>
		/// <param name="scriptGuid">script guid defined in sharedConfig</param>
		/// <param name="scriptArgsOverride">parameters to be used instead the default ones specified in shared config</param>
		public void RunStartupScript( string scriptGuid, string? scriptArgsOverride )
		{
			try
			{
				var guid = Guid.Parse(scriptGuid);
				string? args = null;
				if( !string.IsNullOrEmpty(scriptArgsOverride) ) args = scriptArgsOverride;
				StartSingletonScript( _machineId, guid, args );
			}
			catch( Exception ex )
			{
				log.Error( $"Failed to start startup script {scriptGuid}", ex );
			}
		}

		/// <summary>
		/// Keeps the states of the apps honest about their agent. Agents send an app's state when it changes, so the time of its last
		/// change says nothing about whether the agent is there; the master knows that, from the connection and the heartbeat
		/// (the client state every agent sends each second, and anything else it says). The apps of an agent that is connected and heard
		/// from are refreshed to the time it was last heard; those of one that is gone or silent are marked offline - which is a change,
		/// and goes to the GUIs like any other.
		/// </summary>
		void UpdateAppsFreshness()
		{
			var now = DateTime.UtcNow;
			HashSet<string>? alive = null;

			foreach( var (id, state) in _allAppStates.AppStates )
			{
				// declared but never reported: stays offline until its agent says something about it
				if( state.LastChange.Ticks == 0 ) continue;

				if( alive is null )
				{
					alive = new HashSet<string>();
					foreach( var cl in _server.Clients )
						if( _lastHeard.TryGetValue( cl.Name, out var heard ) && now - heard < AGENT_SILENCE_LIMIT ) alive.Add( cl.Name );
				}

				if( alive.Contains( id.MachineId ) )
				{
					state.LastChange = _lastHeard[id.MachineId];
					state.Offline = false;
				}
				else
				{
					state.Offline = true; // the time of the last change stays: it is that of the last news
				}
			}
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

			// whatever a client says is a sign of life, and what keeps the apps of an agent fresh
			if( !string.IsNullOrEmpty( msg.Sender ) ) _lastHeard[msg.Sender] = DateTime.UtcNow;

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

					// remember its adapters, to be able to wake it up while it is off
					if( m.IsAgent )
					{
						_macCache.Update( m.Name, m.Nics );
					}

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

							// an app that was running and is not, without our killing it, is written down
							_allAppStates.AppStates.TryGetValue( appId, out var before );
							ExitHistory.Observe( appId, before, appState, DateTime.UtcNow );

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
					NoteScriptRequestor( m.Instance, !string.IsNullOrEmpty( m.Requestor ) ? m.Requestor : m.Sender );
					if( string.IsNullOrEmpty(m.HostClientId) ) // request for us (master)
					{
						if( string.IsNullOrEmpty(m.ScriptName) ) // is it a ScriptDef based single-instance script?
						{
							StartSingletonScript( m.Requestor, m.Instance, m.Args );
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

				case WakeUpMessage m:
				{
					WakeUp( m.Sender, m.MachineId );
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

					// forward to those who care (if it was sent from non-master; a state of the master's own was routed by Send)
					if( m.Sender != "" )
					{
						RouteScriptState( m );
					}
					break;
				}

				case MachineStateMessage m:
				{
					// forward to the GUIs that look at it (if it was sent from non-master); nothing on an agent reads another machine's
					if( m.Sender != "" )
					{
						_server.SendToFeed( m, EMsgRecipCateg.Gui );
					}
					break;
				}

				case FeedRequestMessage m:
				{
					// only a GUI has a window to hide
					var ident = _server.Clients.FirstOrDefault( c => c.Name == m.Sender );
					if( ident is not null && ident.IsGui && _server.SetFeedWanted( m.Sender, m.Wanted ) )
					{
						// it was not looking, and now it is: the changes it missed are lost, so it gets everything first
						FeedGuiStates( ident );
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

		// send all initial data to all connected clients
		void FeedAllClients()
		{
			foreach( var cl in _server.Clients )
			{
				FeedSingleClient( cl );
			}
		}

		void FeedSingleClient( ClientIdent ident )
		{
			// The machines and their file shares, to everyone - an agent needs them as much as a GUI.
			// Only the master reads SharedConfig.xml, so this is the only way anybody else learns of a
			// <Share>; and an agent is asked to resolve the nodes belonging to its own machine, which
			// means turning a local path into a UNC one that the other machines can reach.
			//
			// Sent before the rest on purpose: the VFS nodes that follow are resolved against this
			// table, so an agent that receives them in this order is never briefly able to resolve
			// something without knowing where it lives.
			{
				var machines = new Net.MachineDefsMessage( _machineDefs );
				_server.SendToSingle( machines, ident.Name );
			}

			if( ident.IsGui )
			{
				FeedGui( ident );
			}

			if( ident.IsAgent )
			{
				FeedAgent( ident );
			}

			// feed regardless of client type
			var m = new Net.KillAllStatus( _killAllInProgress );
			_server.SendToSingle( m, ident.Name );
		}
		

		// Called once when client connects and sends ClientIdent
		void OnClientIdentified( ClientIdent ident )
		{
			FeedSingleClient( ident );
		}

		void FeedGui( ClientIdent ident )
		{
			// the states, to a GUI that wants them
			bool feed = _server.WantsFeed( ident.Name );

			if( feed ) FeedClientStates( ident );

			// send the full list of plans
			{
				var m = new Net.PlanDefsMessage( from p in _plans.Plans.Values select p.Def, incremental: false );
				_server.SendToSingle( m, ident.Name );
			}

			if( feed ) FeedPlanAndAppStates( ident );

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

			if( feed ) FeedScriptStates( ident );

			// send full list of VFS nodes
			{
				var m = new Net.VfsNodesMessage( _files.VfsNodes.Values );
				_server.SendToSingle( m, ident.Name );
			}

			// send full list of actions
			{
				var m = new Net.MenuItemDefsMessage( _menuItemDefs, false );
				_server.SendToSingle( m, ident.Name );
			}

		}

		/// <summary>The states of everything - clients, plans, apps, scripts - to one GUI: what it gets when it connects and when it starts to look again.</summary>
		void FeedGuiStates( ClientIdent ident )
		{
			FeedClientStates( ident );
			FeedPlanAndAppStates( ident );
			FeedScriptStates( ident );
		}

		void FeedClientStates( ClientIdent ident )
		{
			foreach( (var id, var state) in _allClientStates.ClientStates )
			{
				var m = new Net.ClientStateMessage(DateTime.UtcNow, state);
				_server.SendToSingle( m, ident.Name );
			}
		}

		void FeedPlanAndAppStates( ClientIdent ident )
		{
			_server.SendToSingle( new Net.PlansStateMessage( _plans.PlanStates, full: true ), ident.Name );
			_server.SendToSingle( new Net.AppsStateMessage( _allAppStates.AppStates, DateTime.UtcNow ), ident.Name );
		}

		/// <summary>The cached states of the scripts, without their data: that goes to whoever asked for the script.</summary>
		void FeedScriptStates( ClientIdent ident )
		{
			foreach (var (inst, state) in _localScripts.ScriptCachedStates)
			{
				_server.SendToSingle( new Net.ScriptStateMessage( inst, WithoutData( state ) ), ident.Name );
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

			// the file definitions, so that an agent can look up a <FileRef> itself - anything it is asked to
			// resolve (a script started on it, a node of its machine) may refer to other definitions by id
			{
				var m = new Net.VfsNodesMessage( _files.VfsNodes.Values );
				_server.SendToSingle( m, ident.Name );
			}
		}


		/// <summary>
		/// Sends what changed in the runtime status of clients, apps and plans since the last time, to those who want it
		/// (and all of it now and then, in case something went astray).
		/// </summary>
		void RefreshClients()
		{
			var now = DateTime.UtcNow;
			bool full = ( now - _lastFullFeed ).TotalSeconds >= FULL_FEED_PERIOD;
			if( full ) _lastFullFeed = now;

			// clients: a change goes to everyone who looks, an agent gets all of them now and then (scripts on an agent read them)
			foreach( (var id, var state) in _allClientStates.ClientStates )
			{
				_feedSentClients.TryGetValue( id, out var sent );
				if( state.DiffersForFeed( sent ) )
				{
					_server.SendToFeed( new Net.ClientStateMessage( now, state ), EMsgRecipCateg.All );
					_feedSentClients[id] = state.Clone();
				}
				else if( full )
				{
					_server.SendToAllSubscribed( new Net.ClientStateMessage( now, state ), EMsgRecipCateg.Agent );
				}
			}

			// apps
			{
				var delta = new Dictionary<AppIdTuple, AppState>();
				foreach( (var id, var state) in _allAppStates.AppStates )
				{
					_feedSentApps.TryGetValue( id, out var sent );
					if( full || state.DiffersForFeed( sent ) )
					{
						delta[id] = state;
						_feedSentApps[id] = state.Clone();
					}
				}

				if( delta.Count > 0 )
				{
					_server.SendToFeed( new Net.AppsStateMessage( delta, now ), EMsgRecipCateg.Gui );
				}
			}

			// plans
			{
				var delta = new Dictionary<string, PlanState>();
				foreach( (var name, var state) in _plans.PlanStates )
				{
					_feedSentPlans.TryGetValue( name, out var sent );
					if( full || state.DiffersForFeed( sent ) )
					{
						delta[name] = state;
						_feedSentPlans[name] = state.CloneDeep();
					}
				}

				if( full )
				{
					// all of them, and the receiver forgets any other
					_server.SendToFeed( new Net.PlansStateMessage( _plans.PlanStates, full: true ), EMsgRecipCateg.Gui );
				}
				else if( delta.Count > 0 )
				{
					_server.SendToFeed( new Net.PlansStateMessage( delta, full: false ), EMsgRecipCateg.Gui );
				}
			}

			// we are sending script state on change only, from the async script execution
		}

		/// <summary>Remembers who asked for a script, to send its result to nobody else.</summary>
		void NoteScriptRequestor( Guid instance, string? requestor )
		{
			if( instance == Guid.Empty || string.IsNullOrEmpty( requestor ) ) return;
			lock( _scriptRequestors ) _scriptRequestors[instance] = requestor;
		}

		static ScriptState WithoutData( ScriptState state )
		{
			var c = state.Clone();
			c.Data = null;
			return c;
		}

		/// <summary>
		/// A state of a script, as it goes to the others: in full to whoever asked for the script - its data is the result, or
		/// the progress, which can be big - and without the data to the GUIs that look at the feed. Nobody else needs it.
		/// </summary>
		void RouteScriptState( ScriptStateMessage m )
		{
			string? requestor;
			lock( _scriptRequestors )
			{
				_scriptRequestors.TryGetValue( m.Instance, out requestor );
				if( m.State.Status is EScriptStatus.Finished or EScriptStatus.Failed or EScriptStatus.Cancelled )
					_scriptRequestors.Remove( m.Instance ); // the last word on it
			}

			bool toRequestor = !string.IsNullOrEmpty( requestor );
			if( toRequestor ) _server.SendToSingle( m, requestor! );

			var lean = new ScriptStateMessage( m.Instance, WithoutData( m.State ) ) { Sender = m.Sender };
			_server.SendToFeed( lean, EMsgRecipCateg.Gui, except: toRequestor ? requestor : null );
		}

		SharedConfig LoadSharedConfig( string fileName )
		{
			SharedConfig sharedConfig;
			log.DebugFormat( "Loading shared config file '{0}'", fileName );
			// the reader parses the whole document up front, so releasing the file right away
			// keeps a reload from leaking a handle and from holding the config file open
			using( var textReader = System.IO.File.OpenText( fileName ) )
			{
				sharedConfig = new SharedConfigReader( textReader ).Config;
			}
			_sharedConfigFileName = fileName;
			SharedConfigLoadedAt = DateTime.UtcNow;
			MinDirigentVersion = sharedConfig.MinDirigentVersion;
			SiteName = sharedConfig.SiteName;
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

			foreach( var warning in ConfigChecks.MachineIdCaseMismatches( _machineDefs.Select( m => m.Id ), allAppDefs.Keys ) )
				log.Warn( warning );

			_menuItemDefs = sharedConfig.MainMenuItems;
			_killAllExtras = sharedConfig.KillAllExtras;

			// what the clients were sent is void after the reset; they are fed everything below
			_feedSentApps.Clear();
			_feedSentPlans.Clear();

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
			bool alreadyInProgress = _killAllInProgress;
			
			// remember we are in the middle of the KillAll operation
			_killAllInProgress = true;
			const double KILLALL_TIMEOUT = 10.0; // seconds
			_killAllMaxTime = DateTime.UtcNow + TimeSpan.FromSeconds( KILLALL_TIMEOUT );
			
			if( !alreadyInProgress )
            {
				var msg = new Net.KillAllStatus(true);
				_server.SendToAllSubscribed( msg, EMsgRecipCateg.All );
			}

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

			// kill all running scripts - except the marking a plan is doing as it starts: that finishes, bounded by the plan's
			// MarkTimeout. A second Kill All while the first is still going is the emergency stop and takes those too: the
			// master script of each marking is killed, and it stops its slaves itself.
			if( alreadyInProgress )
			{
				foreach( var id in PlanOwnedScripts.Masters() )
					KillScript( requestorId, id );
			}

			foreach( var (id, state) in _reflScripts.GetAllScriptsState() )
			{
				if( PlanOwnedScripts.Contains( id ) ) continue;
				KillScript( requestorId, id );
			}

			// run extra commands defined in the shared config
			foreach( var ad in _killAllExtras )
			{
				if( !string.IsNullOrEmpty(ad.HostId) )
				{
					var msg = new Net.RunActionMessage(
						requestorId,
						ad,
						ad.HostId  // run the action on specified host
					);
					_server.SendToAllSubscribed( msg, EMsgRecipCateg.Agent );
				}
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

		/// <summary>
		/// Wakes a machine up with Wake-on-LAN, sent from here - the master's network is the one the
		/// machines are on, whoever asked.
		/// </summary>
		/// <remarks>
		/// A MAC given in SharedConfig overrides what agents reported: somebody wrote it there on
		/// purpose, e.g. because the machine wakes by an adapter other than the one it connects with.
		/// Otherwise every MAC the machine last reported is woken. The subnets it was last seen in
		/// are aimed at in either case.
		/// </remarks>
		/// <exception cref="Exception">no MAC is known; the caller's error path reports it back to the requestor</exception>
		public void WakeUp( string requestorId, string machineId )
		{
			var nics = _macCache.Get( machineId );

			var macs = new List<string>();
			var configured = GetMachineDef( machineId )?.MAC;
			if( !string.IsNullOrWhiteSpace( configured ) )
			{
				// a typo in SharedConfig must reach whoever asked, not just a warning in the master's log
				try { WakeOnLan.ParseMac( configured ); }
				catch( ArgumentException ) { throw new Exception( $"SharedConfig gives machine '{machineId}' MAC '{configured}', which is not a MAC address" ); }

				macs.Add( configured );
			}
			else
			{
				macs.AddRange( nics.Select( n => n.Mac ).Distinct() );
			}

			if( macs.Count == 0 )
			{
				throw new Exception( $"No MAC address known for machine '{machineId}'; it has never connected to this master and SharedConfig gives none" );
			}

			var plan = WakeOnLan.Plan( macs, nics, Tools.LocalIPv4Addresses );
			var sent = WakeOnLan.Send( plan );

			log.Info( $"WakeUp of '{machineId}' requested by '{requestorId}': sent {sent} of {plan.Count} magic packets for MAC {string.Join( ", ", macs )} to {string.Join( ", ", plan.Select( p => p.DestinationIp ).Distinct() )} from {string.Join( ", ", plan.Select( p => p.LocalIp ?? "(any)" ).Distinct() )}" );

			if( sent == 0 )
			{
				throw new Exception( $"Wake-on-LAN of machine '{machineId}' failed: none of the {plan.Count} magic packets could be sent; see the master's log" );
			}
		}

		/// <summary>
		/// Starts a script by name on a client (an agent) rather than on the master, the way a GUI does; the master forwards the request.
		/// </summary>
		/// <exception cref="Exception">the client is not connected</exception>
		public void StartScriptOn( string requestorId, string machineId, Guid id, string scriptName, string? args, string title )
		{
			var state = GetClientState( machineId );
			if( state is null || !state.Connected )
				throw new Exception( $"Machine {machineId} is not connected." );

			NoteScriptRequestor( id, requestorId );
			var msg = new Net.StartScriptMessage( requestorId, id, scriptName, null, args, title, machineId );
			_server.SendToSingle( msg, machineId );
		}

		public void Reinstall( string requestorId, ReinstallArgs args )
		{
			var msg = new Net.ReinstallMessage( requestorId, args );
			_server.SendToAllSubscribed( msg, EMsgRecipCateg.All );
		}


		/// <summary>
		/// Starts an already defined script instance.
		/// </summary>
		/// <param name="requestorId"></param>
		/// <param name="id">guid of an already defined script instance</param>
		/// <param name="args">optional args</param>
		public void StartSingletonScript( string? requestorId, Guid id, string? args )
		{
			_singlScripts.StartScript( requestorId, id, args );
		}

		/// <summary>
		/// Starts a script defined by the path, creating a new 'singleton' script instance.
		/// </summary>
		/// <param name="id">id to be assigned to the new script instance; if existing, the script record is replaced</param>
		/// <param name="path">path of the script file relative to the script root folder</param>
		/// <param name="args">optional arguments passed to the script</param>
		public void StartSingletonScript( string? requestorId, Guid id, string? path, string? args )
		{
			_singlScripts.StartScript( requestorId, id, path, args );
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
			if( _singlScripts.Contains( id ) )
			{
				return _singlScripts.GetScriptState( requestorId, id );
			}
			else
			if ( _localScripts.Scripts.ContainsKey( id ) )
			{
				return _localScripts.GetCachedState( id );
			}
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

		void CheckKillAllFinished()
		{
			if( !_killAllInProgress ) return;

			// check the status of all apps, if all are dead, we are done
			bool allDead = true;
			foreach( AppState appState in _allAppStates.AppStates.Values )
			{
				if( !appState.IsOffline && appState.Running )
				{
					allDead = false;
					break;
				}
			}

			if( allDead || DateTime.UtcNow > _killAllMaxTime )
			{
				_killAllInProgress = false;
				var msg = new Net.KillAllStatus(false);
				_server.SendToAllSubscribed( msg, EMsgRecipCateg.All );
			}
		}

		
	}
}
