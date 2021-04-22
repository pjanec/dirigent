using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent.Agent
{
	public class Agent : IDisposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public bool WantsQuit { get; private set; }

		/// <summary>App defs as received from master. Applied when launching an app.</summary>
		private AllAppsDefRegistry _appDefsReg;

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

			_clientIdent = new Net.ClientIdent() { Sender = Guid.NewGuid().ToString(), SubscribedTo = Net.EMsgRecipCateg.Gui };
			_client = new Net.Client( _clientIdent, masterIP, masterPort, autoConn: true );

			_sharedContext = new SharedContext(
				rootForRelativePaths,
				_internalVars,
				new AppInitializedDetectorFactory(),
				_client
			);

			_appDefsReg = new AllAppsDefRegistry();

			_localApps = new LocalAppsRegistry( _sharedContext );


		}

		public void Dispose()
		{
			_client.Dispose();
		}

		public void Tick()
		{
			_client.Tick( OnMessage );
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
						_appDefsReg.AddOrUpdate( ad );

						_localApps.AddOrUpdate( ad );
					}
					break;
				}

				case Net.LaunchAppMessage m:
				{
					// get most recently sent app def (might be newer than the one currently used)	
					var ad = _appDefsReg.FindApp( m.Id );

					var la = _localApps.FindApp( m.Id );
					
					la.LaunchApp( ad );
					break;
				}
			}
		}
	}
}
