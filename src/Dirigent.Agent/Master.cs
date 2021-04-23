using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dirigent.Common;
using Dirigent.Net;

namespace Dirigent.Agent
{
	public class Master : Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public bool WantsQuit { get; private set; }

		private string _localIpAddr;
		private int _port;
		private Net.Server _server;
		private CLIProcessor _cliProc;
		static TelnetServer _telnetServer;
		private AllAppsStateRegistry _allAppStates;
		private AllAppsDefRegistry _allAppDefs;
		private PlanRegistry _plans;
		private Dictionary<AppIdTuple, AppDef> _defaultAppDefs;
		const float CLIENT_REFRESH_PERIOD = 1.0f;
		private Stopwatch _swClientRefresh;

		public Master( string localIpAddr, int port, int cliPort, SharedConfig sharedConfig )
		{
			_localIpAddr = localIpAddr;
			_port = port;
			_allAppStates = new AllAppsStateRegistry();

			_allAppDefs = new AllAppsDefRegistry();
			_allAppDefs.Added += SendAppDefAddedOrUpdated;
			_allAppDefs.Updated += SendAppDefAddedOrUpdated;

			_defaultAppDefs = new Dictionary<AppIdTuple, AppDef>();

			_plans = new PlanRegistry();
			_server = new Server( port );
			//_sharedConfig = sharedConfig;
			_swClientRefresh = new Stopwatch();
			_swClientRefresh.Restart();

			InitFromSharedConfig( sharedConfig );

			log.Info( $"Running Master at IP {localIpAddr}, port {port}, cliPort {cliPort}" );

			// start a telnet client server
			_cliProc = new CLIProcessor( this );

            log.InfoFormat("Command Line Interface running on port {0}", cliPort);
			_telnetServer = new TelnetServer( "0.0.0.0", cliPort, _cliProc );
			_telnetServer.Start();

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
					if( m.AppsState is null )
					{
						// WTF?? no one is sending empty message!!!
						int i = 1;
					}
					else
					{
						foreach( var( appId, appState ) in m.AppsState )
						{
							_allAppStates.AddOrUpdate( appId, appState );
						}
					}
					break;
				}

				case LaunchAppMessage m:
				{
					LaunchApp( m.Id, m.PlanName );
					break;
				}

				case KillAppMessage m:
				{
					KillApp( m.Id );
					break;
				}

				case RestartAppMessage m:
				{
					RestartApp( m.Id );
					break;
				}

				case CLIRequestMessage m:
				{
					var cliClient = new CLIClient( _server, m.Sender );
					_cliProc.AddRequest( cliClient, m.Text );
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


		// Called once when client connects and sends ClientIdent
		void OnClientIdentified( ClientIdent ident )
		{
			if( ( ident.SubscribedTo & EMsgRecipCateg.Gui ) != 0 )
			{
				FeedGui( ident );
			}

			if( ( ident.SubscribedTo & EMsgRecipCateg.Agent ) != 0 )
			{
				FeedAgent( ident );
			}
		}

		void FeedGui( ClientIdent ident )
		{
			// send the full list of plans
			{
				var m = new Net.PlanDefsMessage( from p in _plans.Plans.Values select p.Def );
				_server.SendToSingle( m, ident.Name );
			}

			// send the full list of plan states
			{
				var m = new Net.PlansStateMessage( _plans.PlanStates );
				_server.SendToSingle( m, ident.Name );
			}

			// send the full list of app states
			{
				var m = new Net.AppsStateMessage( _allAppStates.AppStates );
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
		/// Sends current full state to subcribed clients
		/// </summary>
		void RefreshClients()
		{
			// apps
			{
				var m = new Net.AppsStateMessage( _allAppStates.AppStates );
				_server.SendToAllSubscribed( m, EMsgRecipCateg.Gui );
			}

			// plans
			{
				var m = new Net.PlansStateMessage( _plans.PlanStates );
				_server.SendToAllSubscribed( m, EMsgRecipCateg.Gui );
			}
		}

		void InitFromSharedConfig( SharedConfig sharedConfig )
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
		}

		/// <summary>
		/// Sends updated app def to their respoctive agent
		/// This happens when the appdef changes because of plan switch etc.
		/// </summary>
		/// <param name="appDef"></param>
		void SendAppDefAddedOrUpdated( AppDef ad )
		{
			foreach( var cl in _server.Clients )
			{
				if( cl.Name == ad.Id.AppId )
				{
					var m = new Net.AppDefsMessage( new AppDef[1] { ad }, incremental: true );
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
		public void LaunchApp( AppIdTuple id, string? planName )
		{
			// load app def from given plan if a plan is specified
			if( planName != null && planName != string.Empty )
			{
				var appDef = _plans.FindAppInPlan( planName, id );

				// this sends app def to agent if different from the previous
				_allAppDefs.AddOrUpdate( appDef );
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


			// send app start command
			var msg = new Net.LaunchAppMessage( id, null );
			_server.SendToSingle( msg, id.MachineId );
		}

		/// <summary>
		/// Send app kill command directly to owning agent
		/// </summary>
		public void KillApp( AppIdTuple id )
		{
			var msg = new Net.KillAppMessage( id );
			_server.SendToSingle( msg, id.MachineId );
		}

		/// <summary>
		/// Sends app restart command directly to owning agent.
		/// </summary>
		public void RestartApp( AppIdTuple id )
		{
			var msg = new Net.RestartAppMessage( id );
			_server.SendToSingle( msg, id.MachineId );
		}

	}
}
