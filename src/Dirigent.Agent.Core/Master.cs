﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dirigent.Common;
using Dirigent.Net;

namespace Dirigent.Agent
{
	public class Master : Disposable, IDirig
	{

		#region IDirig interface

		public AppState? GetAppState( AppIdTuple Id ) { if( _allAppStates.AppStates.TryGetValue(Id, out var x)) return x; else return null; }
		public IEnumerable<KeyValuePair<AppIdTuple, AppState>> GetAllAppStates() { return _allAppStates.AppStates; }
		public PlanState? GetPlanState( string Id ) { if( _plans.Plans.TryGetValue(Id, out var x)) return x.State; else return null; }
		public IEnumerable<KeyValuePair<string, PlanState>> GetAllPlanStates() { return from x in _plans.Plans.Values select new KeyValuePair<string, PlanState>( x.Name, x.State ); }
		public AppDef? GetAppDef( AppIdTuple Id ) { if( _allAppDefs.AppDefs.TryGetValue(Id, out var x)) return x; else return null; }
		public IEnumerable<KeyValuePair<AppIdTuple, AppDef>> GetAllAppDefs() { return _allAppDefs.AppDefs; }
		public PlanDef? GetPlanDef( string Id ) { if( _plans.Plans.TryGetValue( Id, out var p ) ) return p.Def; else return null; }
		public IEnumerable<PlanDef> GetAllPlanDefs() { return from x in _plans.Plans.Values select x.Def; }
		public string Name => string.Empty;
		public void Send( Net.Message msg ) { ProcessIncomingMessage( msg ); }

		#endregion

		public bool WantsQuit { get; set; }
		public Dictionary<AppIdTuple, AppState> AppsState => _allAppStates.AppStates;

		#region Private fields

		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );
		private string _localIpAddr;
		private int _port;
		private Net.Server _server;
		private CLIProcessor _cliProc;
		private TelnetServer _telnetServer;
		private AllAppsStateRegistry _allAppStates;
		private AllAppsDefRegistry _allAppDefs;
		private PlanRegistry _plans;
		private Dictionary<AppIdTuple, AppDef> _defaultAppDefs;
		const float CLIENT_REFRESH_PERIOD = 1.0f;
		private Stopwatch _swClientRefresh;
		private TickableCollection _tickers;
		private string _sharedConfigFileName = string.Empty;
		
		#endregion

		public Master( string localIpAddr, int port, int cliPort, string sharedConfigFileName )
		{
			log.Info( $"Running Master at IP {localIpAddr}, port {port}, cliPort {cliPort}" );

			_localIpAddr = localIpAddr;
			_port = port;

			_allAppStates = new AllAppsStateRegistry();

			_allAppDefs = new AllAppsDefRegistry();
			_allAppDefs.Added += SendAppDefAddedOrUpdated;
			_allAppDefs.Updated += SendAppDefAddedOrUpdated;

			_defaultAppDefs = new Dictionary<AppIdTuple, AppDef>();

			_plans = new PlanRegistry( this );
			_plans.PlanDefUpdated += SendPlanDefUpdated;

			_server = new Server( port );
			_swClientRefresh = new Stopwatch();
			_swClientRefresh.Restart();

			// start a telnet client server
			_cliProc = new CLIProcessor( this );

            log.InfoFormat("Command Line Interface running on port {0}", cliPort);
			_telnetServer = new TelnetServer( "0.0.0.0", cliPort, _cliProc );

			var sharedConfig = LoadSharedConfig( sharedConfigFileName );
			InitFromConfig( sharedConfig );

			_tickers = new TickableCollection();
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			_telnetServer?.Dispose();
			_cliProc.Dispose();
			_server.Dispose();
		}

		public void Tick()
		{
			_tickers.Tick();

			_plans.Tick();

			_cliProc.Tick();

			_telnetServer?.Tick();

			// process all messages received since last tick from all clients
			_server.Tick( ( msg ) =>
			{
				ProcessIncomingMessage( msg );
			} );

			// periodically refresh intrested clients
			if( _swClientRefresh.Elapsed.TotalSeconds > CLIENT_REFRESH_PERIOD )
			{
				RefreshClients();
				_swClientRefresh.Restart();
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
					StartApp( m.Sender, m.Id, m.PlanName, m.Flags );
					break;
				}

				case KillAppMessage m:
				{
					KillApp( m.Sender, m.Id, m.Flags );
					break;
				}

				case RestartAppMessage m:
				{
					RestartApp( m.Sender, m.Id );
					break;
				}

				case CLIRequestMessage m:
				{
					var cliClient = new CLIClient( _server, m.Sender );
					_cliProc.AddRequest( cliClient, m.Text );
					break;
				}

				case StartPlanMessage m:
				{
					StartPlan( m.Sender, m.PlanName );
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
					RestartPlan( m.Sender, m.PlanName );
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
		}

		SharedConfig LoadSharedConfig( string fileName )
		{
			SharedConfig sharedConfig;
			log.DebugFormat( "Loading shared config file '{0}'", fileName );
			sharedConfig = new SharedXmlConfigReader( System.IO.File.OpenText( fileName ) ).cfg;
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
		/// Updates appDefs before acting on apps within the plan
		/// </summary>
		/// <param name="planDef"></param>
		void UsePlan( PlanDef planDef )
		{
			foreach( var ad in planDef.AppDefs )
			{
				_allAppDefs.AddOrUpdate( ad );
			}
		}

		/// <summary>
		/// Launches given app by sending LaunchApp command directly to owning agent.
		/// Throws on failure.
		/// </summary>
		/// <param name="id">App to run</param>
		/// <param name="planName">The plan the app belongs to. null=none (use default app settings), Empty=current plan, non-empty=specific plan name.</param>
		public void StartApp( string requestorId, AppIdTuple id, string? planName, Net.StartAppFlags flags=0 )
		{
			// load app def from given plan if a plan is specified
			if( planName != null && planName != string.Empty )
			{
				var app = _plans.FindAppInPlan( planName, id );

				// this sends app def to agent if different from the previous
				_allAppDefs.AddOrUpdate( app.Def );
			}
			else// load from defaults 
			if( planName is null )
			{
				if( _defaultAppDefs.TryGetValue( id, out var appDef ) )
				{
					_allAppDefs.AddOrUpdate( appDef );
				}
			}
			else // plan name empty, keep recent app def
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
			var msg = new Net.StartAppMessage( requestorId, id, planName, flags );
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
		public void RestartApp( string requestorId, AppIdTuple id )
		{
			var msg = new Net.RestartAppMessage( requestorId, id );
			_server.SendToSingle( msg, id.MachineId );
		}

		public void StartPlan( string requestorId, string planName )
		{
			var plan = _plans.FindPlan( planName ); // throws on error
			plan.Start( requestorId );
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

		public void RestartPlan( string requestorId, string planName )
		{
			var plan = _plans.FindPlan( planName ); // throws on error
			plan.Restart( requestorId );
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
					KillApp( requestorId, ad.Id );
				}
			}
		}

		public void ReloadSharedConfig( string requestorId, ReloadSharedConfigArgs args )
		{
			// load (may throw an exception on error)
			var sharedConfig = LoadSharedConfig( _sharedConfigFileName );

			// send kill to all
			KillAll( requestorId, new KillAllArgs() );

			// wait a while to give the apps the time to die
			Thread.Sleep(3000);

			// reinit from shared config
			InitFromConfig( sharedConfig );
		}

	}
}