using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Dirigent
{
	/// <summary>
	/// Manages gateway configurations. Opens a single gateway session,
	/// reads machine list from the dirigent behind the gateway and updates the
	/// gateway config with the actual machines from dirigent's config.
	/// The list is used next when opening a new session to the same gateway.
	/// Calls Connectd/Disconnected from Tick
	/// </summary>
	public class GatewayManager : Disposable
	{
        /// <summary>
        /// Local ports used with IP 127.0.0.1 when connecting to forwarded ports
        /// Incremented for each port-forwaded service
        /// </summary>
        public static int LocalPortBase = 41000;

		GatewayConfig _config;
		AppConfig _ac;
		
		GatewaySession? _currentSession; // currently loaded gateway instance

		public IEnumerable<GatewayDef> Gateways => _config.Gateways;
		
		public GatewaySession? CurrentSession => _currentSession;
		public bool IsConnected => _currentSession != null && _currentSession.IsConnected;


		// def used for creating current session
		// must be reference to the one from the _config (we want to modify it when machines are reported from master)
		GatewayDef? _currentDef;

		bool _wasConnected;
		public Action Connected = () => {};
		public Action Disconnected = () => {};

		public GatewayManager( AppConfig ac )
        {
			_ac = ac;
			
			var reader = new GatewayConfigReader( ac.GatewayCfgFileName );
			_config = reader.Config;
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if (!disposing) return;

			Disconnect();
		}

		public void Tick()
		{
			EvaluateConnectDisconnect();
		}

		void EvaluateConnectDisconnect()
		{
			// evaluate connection status change & call Connected/Disconnected
			if( _wasConnected && (CurrentSession is null || !CurrentSession.IsConnected) )
			{
				Disconnected?.Invoke();
				_wasConnected = false;
			}
			else if( !_wasConnected && CurrentSession is not null && CurrentSession.IsConnected )
			{
				Connected?.Invoke();
				_wasConnected = true;
			}
		}

		// What we need:
		//   enumerate available gateways defs so we can present them in a menu
		//   create a gateway session object for selected def - this calculates the port maps
		//   open the connection to the gateway - this estabilish the port forwarding which allows the GUI to connect to the master
		//   receive machine defs from the master
		//   compare with existing gatewaydefs; if different, update gateway config and restart the port forwarder
		//   offer the port mapping lookups (machineId, service name) -> (ip, port)

		void LoadSession( GatewayDef gatewayDef )
		{
			_currentDef = gatewayDef;


			// use master IP and port if defined for the gateway
			string masterIP = gatewayDef.MasterIP;
			int masterPort = gatewayDef.MasterPort;
						
			// otherwise use the command-line configured ones
			if( string.IsNullOrEmpty(masterIP) ) masterIP = _ac.MasterIP;
			if( masterPort <= 0 ) masterPort = _ac.MasterPort;

			_currentSession = new GatewaySession( gatewayDef, masterIP, masterPort );
		}

		public void Connect( GatewayDef gatewayDef )
		{
			LoadSession( gatewayDef );
			Connect();
		}

		public void Connect()
		{
			if (_currentSession is null)
				throw new Exception($"No current session, load the session first");
			_currentSession.Open();
			EvaluateConnectDisconnect(); // makes sure we correctly call Connected event
		}

		public void Disconnect()
		{
			_currentSession?.Dispose();
			_currentSession = null;
			_currentDef = null;
			EvaluateConnectDisconnect(); // makes sure we correctly call Disconnected event
		}


		// to be called when a new list of machines is received from the master
		public void UpdateMachines( IEnumerable<MachineDef> machinesEnun )
		{
			var machines = machinesEnun.ToList(); // make sure out machines will not disappear (happens on Disconnect)
			
			if (_currentSession is null) return;
			
			// compare with existing gatewaydefs; if different, update gateway config and restart the port forwarder
			if( !_currentSession.AreMachinesSame( machines ) )
			{
				// save when it still exists (cleared on Disconnect)
				var def = _currentDef;

				// close the current session
				Disconnect();

				// update the gateway config (it is the one linked to the _config)
				def!.Machines = new( machines );

				// write the gateway config (now with updated machines)
				new GatewayConfigWriter().Write( _ac.GatewayCfgFileName, _config );

				// create a new session
				Connect( def );
			}

		}

		public IDictionary<string, string>? GetVariables( string machineId, string serviceName )
		{
			if (_currentSession is null) return null;
			return _currentSession.GetVariables( machineId, serviceName );
		}

		void SetStatus( string text, string type = "", int timeoutMsec = -1 )
		{
			AppMessenger.Instance.Send( new AppMessages.StatusText( "SSH", "" ) );
		}
	}
}
