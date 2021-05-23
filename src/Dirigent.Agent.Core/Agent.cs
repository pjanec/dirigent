using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

		public bool WantsQuit { get; set; }
		public string Name => _clientIdent.Name;

		private LocalAppsRegistry _localApps;
		private Net.ClientIdent _clientIdent; // name of the network client; messages are marked with that
		private Net.Client _client;
		private SharedContext _sharedContext;

        /// <summary>
		/// Dirigent internals vars that can be used for expansion inside process exe paths, command line...)
		/// </summary>
		Dictionary<string, string> _internalVars = new ();

		public Agent( string machineId, string masterIP, int masterPort, string rootForRelativePaths )


		{
			log.Info( $"Running Agent machineId={machineId}, masterIp={masterIP}, masterPort={masterPort}" );

			_clientIdent = new Net.ClientIdent() { Sender = machineId, SubscribedTo = Net.EMsgRecipCateg.Agent };
			_client = new Net.Client( _clientIdent, masterIP, masterPort, autoConn: true );

			_sharedContext = new SharedContext(
				rootForRelativePaths,
				_internalVars,
				new AppInitializedDetectorFactory(),
				_client
			);

			_localApps = new LocalAppsRegistry( _sharedContext );


		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			_client.Dispose();
		}

		public void Tick()
		{
			_client.Tick( OnMessage );

			_localApps.Tick();

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
					la.StartApp( flags: m.Flags );
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
					la.RestartApp();
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
			}
		}


		// incoming message from master
		void OnMessage( Net.Message msg )
		{
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
			// split & parse
			var varList = new List<Tuple<string, string>>();
			foreach( var kv in vars.Split(new string[] { "::" }, StringSplitOptions.None))
			{
				if( string.IsNullOrWhiteSpace(kv) ) // nothing present
				{
					log.ErrorFormat("Invalid SetVars format: {0}", kv);
					continue;
				}

				int equalSignIdx = kv.IndexOf("=");

				if( equalSignIdx < 0 ) // equal sign not present
				{
					log.ErrorFormat("Invalid SetVars format: {0}", kv);
					continue;
				}

				string name = kv.Substring(0, equalSignIdx).Trim();
				string value = kv.Substring(equalSignIdx+1).TrimStart();
				
				if( string.IsNullOrEmpty(name) )
				{
					log.ErrorFormat("Invalid SetVars format: {0}", kv);
					continue;
				}

				varList.Add( new Tuple<string, string>(name, value) );
			}

			// apply
			foreach( var kv in varList )
			{
				var name = kv.Item1;
				var value = kv.Item2;
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


	}
}
