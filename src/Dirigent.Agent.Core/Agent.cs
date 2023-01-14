using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace Dirigent
{
	public class Agent : Disposable, IDirig
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public AppState? GetAppState( AppIdTuple Id ) { if( _localApps.Apps.TryGetValue(Id, out var x)) return x.AppState; else return null; }
		public IEnumerable<KeyValuePair<AppIdTuple, AppState>> GetAllAppStates() { return from x in _localApps.Apps select new KeyValuePair<AppIdTuple, AppState>(x.Key, x.Value.AppState); }
		public AppDef? GetAppDef( AppIdTuple Id ) { if( _localApps.Apps.TryGetValue(Id, out var x)) return x.RecentAppDef; else return null; }
		public IEnumerable<KeyValuePair<AppIdTuple, AppDef>> GetAllAppDefs() { return from x in _localApps.Apps select new KeyValuePair<AppIdTuple, AppDef>(x.Key, x.Value.RecentAppDef); }
		public IEnumerable<PlanDef> GetAllPlanDefs() { return new List<PlanDef>(); }
		public void Send( Net.Message msg ) { _client.Send( msg ); }
		public Task<TResult?> RunScriptAsync<TArgs, TResult>( string clientId, string scriptName, string? sourceCode, TArgs? args, string title, out Guid scriptInstance )
			=> _reflStates.ScriptReg.RunScriptAsync<TArgs, TResult>( clientId, scriptName, sourceCode, args, title, out scriptInstance );
		public Task<VfsNodeDef> ResolveAsync( VfsNodeDef nodeDef, bool forceUNC, bool includeContent )
			=> _reflStates.FileReg.ResolveAsync( _syncIDirig, nodeDef, forceUNC, includeContent, null );

		public bool WantsQuit { get; set; }
		public string Name => _clientIdent.Name;

		private ProcessInfoRegistry? _procInfoReg = null;
		private LocalAppsRegistry _localApps;
		private Net.ClientIdent _clientIdent; // name of the network client; messages are marked with that
		private Net.Client _client;
		private SharedContext _sharedContext;
		private string _rootForRelativePaths;
		private LocalConfig? _localConfig;
		private ToolsRegistry _toolsReg;
		public ToolsRegistry ToolsRegistry => _toolsReg;

		List<FolderWatcher> _folderWatchers = new List<FolderWatcher>();

		private TickableCollection _tickers;
		public TickableCollection Tickers => _tickers;
		public ScriptFactory ScriptFactory;
		private SynchronousOpProcessor _syncOps;
		private SynchronousIDirig _syncIDirig;
		private LocalScriptRegistry _localScripts;
		ReflectedStateRepo _reflStates;
		private bool _debug = false; // do not catch exceptions etc.
		AppConfig _ac;

		

        /// <summary>
		/// Dirigent internals vars that can be used for expansion inside process exe paths, command line...)
		/// </summary>
		Dictionary<string, string> _internalVars = new ();

		public Agent( AppConfig ac, string? machineId=null )
		{
			_ac = ac;

			if( machineId == null ) machineId = _ac.MachineId;

			log.Info( $"Running Agent machineId={machineId}, masterIp={_ac.MasterIP}, masterPort={_ac.MasterPort}" );

			_debug = Tools.BoolFromString( _ac.Debug );

			_clientIdent = new Net.ClientIdent() { Sender = machineId, SubscribedTo = Net.EMsgRecipCateg.Agent };
			_client = new Net.Client( _clientIdent, _ac.MasterIP, _ac.MasterPort, autoConn: true );
			_rootForRelativePaths = PathUtils.GetRootForRelativePaths( _ac.SharedCfgFileName, _ac.RootForRelativePaths );

			_reflStates = new ReflectedStateRepo( _client, machineId, _rootForRelativePaths );

			_syncOps = new SynchronousOpProcessor();
			_syncIDirig = new SynchronousIDirig( this, _syncOps );

			ScriptFactory = new ScriptFactory( _rootForRelativePaths );

			_sharedContext = new SharedContext(
				_rootForRelativePaths,
				_internalVars,
				new AppInitializedDetectorFactory(),
				_client,
				machineId
			);

			_tickers = new TickableCollection();

			_procInfoReg = new ProcessInfoRegistry();

			_localApps = new LocalAppsRegistry( _sharedContext, _procInfoReg );

			var toolDefs = new List<AppDef>();
			_localConfig = LoadLocalConfig( _ac.LocalCfgFileName, machineId );
			if( _localConfig is not null )
			{
				InitFromLocalConfig();
				toolDefs = _localConfig.Tools;
			}
			
			_toolsReg = new ToolsRegistry( _sharedContext, toolDefs, _reflStates );

			_localScripts = new LocalScriptRegistry( this, ScriptFactory, _syncOps, _rootForRelativePaths );
			
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if( !disposing ) return;
			
			_toolsReg?.Dispose();
			_procInfoReg?.Dispose();
			_reflStates.Dispose();
			_localScripts.Dispose();
			_tickers.Dispose();
			_client.Dispose();
		}

		public void Tick()
		{
			_client.Tick( OnMessage );

			_procInfoReg?.Tick();

			_localApps.Tick();

			_tickers.Tick();

			_syncOps.Tick();

			_localScripts.Tick();

			_toolsReg?.Tick();

			PublishAgentState();
		}

		void PublishAgentState()
		{
			var now = DateTime.UtcNow;

			// send client's state
			{
				var clientState = new ClientState();
				clientState.Ident = _clientIdent;
				clientState.LastChange = now;

				var msg = new Net.ClientStateMessage( now, clientState );
				_client.Send( msg );
			}

			// send the state of all local apps

			var states = new Dictionary<AppIdTuple, AppState>();
			foreach( var li in _localApps.Apps.Values )
			{
				states[li.Id] = li.AppState;
			}

			if( states.Count > 0 )
			{
				var msg = new Net.AppsStateMessage( states, DateTime.UtcNow );
				_client.Send( msg );
			}

			// state of the machine
			{
				var machineState = GetMachineState();

				var msg = new Net.MachineStateMessage( _clientIdent.Name, now, machineState );
				_client.Send( msg );
			}
		}


		void ProcessIncomingMessage( Net.Message msg )
		{
			switch( msg )
			{
				case Net.AppDefsMessage m:
				{
					//Debug.Assert( m.AppDefs is not null );
					if( m.AppDefs is not null )
					{
						foreach( var ad in m.AppDefs )
						{
							_localApps.AddOrUpdate( ad );
						}
					}
					break;
				}

				case Net.StartAppMessage m:
				{
					var la = _localApps.FindApp( m.Id );
					var vars = m.UseVars ? m.Vars ?? new() : null; // pass null if not vars change is required
					la.StartApp( flags: m.Flags, vars: vars );
					break;
				}

				case Net.KillAppMessage m:
				{
					var la = _localApps.FindApp( m.Id );
					la.KillApp( m.Flags );
					break;
				}

				case Net.RestartAppMessage m:
				{
					var la = _localApps.FindApp( m.Id );
					var vars = m.UseVars ? m.Vars ?? new() : null; // pass null if not vars change is required
					la.RestartApp( vars );
					break;
				}

				case Net.ResetMessage m:
				{
					_localApps.Clear();
					break;
				}

				case Net.SetVarsMessage m:
				{
					SetVars( m.Vars );
					break;
				}

				case Net.ShutdownMessage m:
				{
					Shutdown( m.Args );
					break;
				}

				case Net.TerminateMessage m:
				{
					Terminate( m.Args );
					break;
				}

				case Net.SetWindowStyleMessage m:
				{
					var la = _localApps.FindApp( m.AppIdTuple );
					la.SetWindowStyle( m.WindowStyle );
					break;
				}

				case Net.StartScriptMessage m:
				{
					if( m.HostClientId == _clientIdent.Name ) // is it for us? Note, this message is broadcasted to all nodes...
					{
						_localScripts.Start( m.Instance, m.ScriptName, m.SourceCode, m.Args, m.Title, m.Requestor );
					}
					break;
				}

				case Net.KillScriptMessage m:
				{
					_localScripts.Stop( m.Instance );
					break;
				}

			}
		}


		// incoming message from master
		void OnMessage( Net.Message msg )
		{
			if( _debug ) // no exception catching
			{
                ProcessIncomingMessage(msg);
				return;
			}
			
            try
            {
                ProcessIncomingMessage(msg);
            }
            catch (RemoteOperationErrorException) // an error from another agent received
            {
                throw; // just forward up the stack, DO NOT broadcast an error msg (would cause an endless loop & network flooding)
            }
            catch (Exception ex) // some local operation error as a result of remote request from another agent
            {
                log.ErrorFormat("Exception: "+ex.ToString());

                // send an error message to agents
                // the requestor is supposed to present an error message to the user
				var errmsg = new Net.RemoteOperationErrorMessage(
                            msg.Sender, // agent that requested the local operation here
                            ex.Message, // description of the problem
                            new Dictionary<string, string>() // additional info to the problem
                            {
                                { "Exception", ex.ToString() }
                            }
                    );
				_client.Send( errmsg );
            }
		}

		// format of string: VAR1=VALUE1::VAR2=VALUE2
		void SetVars( string vars )
		{
			try
			{
				// split & parse
				var varList = Tools.ParseEnvVarList( vars );
				if( varList is null ) return;

				// apply
				foreach( var kv in varList )
				{
					var name = kv.Key;
					var value = kv.Value;
					log.Debug(string.Format("Setting env var: {0}={1}", name, value));

					try{
						System.Environment.SetEnvironmentVariable( name, value );
					}
					catch( Exception ex )
					{
						log.ErrorFormat("Exception: SetVars {0}={1} failure: {2}", name, value, ex);
						throw new Exception(String.Format("SetVars {0}={1} failure: {2}", name, value, ex));
					}
				}
			}
			catch( Exception ex )
			{
				log.Error(ex.Message);
			}
		}

        public void Terminate( TerminateArgs args )
        {
			if( !String.IsNullOrEmpty( args.MachineId ) && _clientIdent.Sender != args.MachineId )
				return;

	        log.DebugFormat("Terminate killApps={0} machineId={1}", args.KillApps, args.MachineId);

			if( args.KillApps )
			{
				KillAllLocalAppsNoWait();
			}

			// terminate dirigent agent
			AppMessenger.Instance.Send( new Dirigent.AppMessages.ExitApp() );
        }

        public void Shutdown( ShutdownArgs args )
        {
	        log.DebugFormat("Shutdown mode={0}", args.Mode.ToString());

			string procName = "";
			string cmdl = "";

			#if Windows
				procName = "shutdown.exe";
				if( args.Mode == EShutdownMode.PowerOff ) cmdl="-s -t 0";
				if( args.Mode == EShutdownMode.Reboot ) cmdl="-r -t 0";
			#endif 
			
			#if Linux
				procName = "sudo";
				if( args.Mode == EShutdownMode.PowerOff )
				{
					cmdl="shutdown now";
				}
				if( args.Mode == EShutdownMode.Reboot )
				{
					cmdl="shutdown -r now";
				}
			#endif 

			var psi = new System.Diagnostics.ProcessStartInfo(procName, cmdl);
			psi.UseShellExecute = true;
            log.DebugFormat("StartProc exe \"{0}\", cmd \"{1}\", dir \"{2}\"", psi.FileName, psi.Arguments, psi.WorkingDirectory );
			System.Diagnostics.Process.Start(psi);
        }


		void KillAllLocalAppsNoWait()
		{
			foreach( var la in _localApps.Apps.Values )
			{
				la.KillApp();
			}
		}

		LocalConfig? LoadLocalConfig( string fileName, string machineId )
		{
			if( string.IsNullOrEmpty( fileName ) )
				return null;

			var fullPath = Path.GetFullPath( fileName );
			log.DebugFormat( "Loading local config file '{0}'", fullPath );
			return new LocalConfigReader( File.OpenText( fullPath ), machineId ).Config;
		}

		void InitFromLocalConfig()
		{
			InitializeFolderWatchers();
		}


		void InitializeFolderWatchers()
		{
			// no local config file loaded	
			if( _localConfig is null) return;

			foreach( var xmlCfg in _localConfig.folderWatcherXmls )
			{
				var fw = new FolderWatcher( xmlCfg, this, _rootForRelativePaths );
				if( fw.Initialized )
				{
					_folderWatchers.Add( fw );
				}
			}
		}

		MachineState GetMachineState()
		{
			#if Windows
			return new MachineState()
			{
				CPU = _procInfoReg?.GetTotalCpuUsage() ?? 0.0f,
				MemoryAvailMB = WinApi.PerformanceInfo.GetPhysicalAvailableMemoryInMiB(),
				MemoryTotalMB = WinApi.PerformanceInfo.GetTotalMemoryInMiB()
			};
			#else
			return new MachineState();
			#endif
		}

	}
}
