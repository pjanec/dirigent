using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dirigent.Common;

namespace Dirigent.Agent
{
	public class Agent : Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public bool WantsQuit { get; private set; }

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
			// send the state of all local apps

			var states = new Dictionary<AppIdTuple, AppState>();
			foreach( var li in _localApps.Apps.Values )
			{
				states[li.Id] = li.AppState;
			}

			if( states.Count > 0 )
			{
				var msg = new Net.AppsStateMessage( states );
				_client.Send( msg );
			}
		}

		// incoming message from master
		void OnMessage( Net.Message msg )
		{
			switch( msg )
			{
				case Net.AppDefsMessage m:
				{
					foreach( var ad in m.AppDefs )
					{
						_localApps.AddOrUpdate( ad );
					}
					break;
				}

				case Net.LaunchAppMessage m:
				{
					var la = _localApps.FindApp( m.Id );
					la.LaunchApp();
					break;
				}

				case Net.KillAppMessage m:
				{
					var la = _localApps.FindApp( m.Id );
					la.KillApp();
					break;
				}

				case Net.RestartAppMessage m:
				{
					var la = _localApps.FindApp( m.Id );
					la.RestartApp();
					break;
				}
			}
		}
	}
}
