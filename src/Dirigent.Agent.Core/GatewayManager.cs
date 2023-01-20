using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent
{
	public class GatewayManager
	{
        /// <summary>
        /// Local ports used with IP 127.0.0.1 when connecting to forwarded ports
        /// Incremented for each port-forwaded service
        /// </summary>
        public static int LocalPortBase = 41000;

		GatewayConfig _config;
		AppConfig _ac;
		ToolsRegistry _toolsReg;
		
		GatewaySession? _currentSession; // currently loaded gateway instance

		public IEnumerable<GatewayDef> Gateways => _config.Gateways;
		
		public GatewaySession? CurrentSession => _currentSession;
		
		// def used for creating current session
		// must be reference to the one from the _config (we want to modify it when machines are reported from master)
		GatewayDef? _currentDef;

		public GatewayManager( AppConfig ac, ToolsRegistry toolsReg )
        {
			_ac = ac;
			_toolsReg = toolsReg;
			var reader = new GatewayConfigReader( ac.GatewayCfgFileName );
			_config = reader.Config;
            
		}

		// What we need:
		//   enumerate available gateways defs so we can present them in a menu
		//   create a gateway session object for selected def - this calculates the port maps
		//   open the connection to the gateway - this estabilish the port forwarding which allows the GUI to connect to the master
		//   receive machine defs from the master
		//   compare with existing gatewaydefs; if different, update gateway config and restart the port forwarder
		//   offer the port mapping lookups (machineId, service name) -> (ip, port)


		void OpenSession( GatewayDef gatewayDef )
		{
			_currentDef = gatewayDef;
			_currentSession = new GatewaySession( gatewayDef, _ac.MasterIP, _ac.MasterPort, _toolsReg );
			_currentSession.Open();
		}

		void CloseSession()
		{
			_currentSession?.Dispose();
			_currentSession = null;
			_currentDef = null;
		}


		// to be called when a new list of machines is received from the master
		public void MachinesReceived( IEnumerable<MachineDef> machines )
		{
			if (_currentSession is null) return;
			
			// compare with existing gatewaydefs; if different, update gateway config and restart the port forwarder
			if( !_currentSession.AreMachinesSame( machines ) )
			{
				var def = _currentDef;

				// close the current session
				CloseSession();

				// update the gateway config
				def!.Machines = new( machines );

				// write the gateway config (now with updated machines)
				new GatewayConfigWriter().Write( _ac.GatewayCfgFileName, _config );

				// create a new session
				OpenSession( def );
			}

		}

		public Dictionary<string, string>? GetVariables( string machineId, string serviceName )
		{
			if (_currentSession is null) return null;
			return _currentSession.GetVariables( machineId, serviceName );
		}

	}
}
