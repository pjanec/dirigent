using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent
{

	public class IPPort
	{
		public string IP = "";
		public int Port;
	}
		
    
	public class GatewaySession : Disposable
	{
        GatewayDef _def;
		string _masterIP;
		int _masterPort;
		List<Machine> _machines = new();

		PortForwarder? _portFwd;
		ToolsRegistry _toolsReg;

		public bool IsConnected => _portFwd is null ? false : _portFwd.IsRunning;

		public GatewaySession( GatewayDef def, string masterIP, int masterPort, ToolsRegistry toolsReg )
		{
            _def = def;
			_toolsReg = toolsReg;
			_masterIP = masterIP;
			_masterPort = masterPort;

			RecalcMachines( _def.Machines );
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if( !disposing ) return;

            _portFwd?.Dispose();
        }

		public void Open()
		{
			if( _portFwd is null )
			{
				_portFwd = new PortForwarder( _def, _machines, _toolsReg );
			}
		}

		public void Close()
		{
			_portFwd?.Dispose();
		}

		public bool AreMachinesSame( IEnumerable<MachineDef> machines )
		{
			return _def.Machines.SequenceEqual( machines );
		}

		public class Machine
        {
			public string Id = "";
            public string IP = "";
			public bool AlwaysLocal;
            public List<Service> Services = new List<Service>();
        }


		void RecalcMachines( IEnumerable<MachineDef> machineDefs )
		{
			_machines.Clear();
			
            foreach( var machDef in machineDefs )
            {
				var m = new Machine();
				m.Id = machDef.Id;
				m.IP = machDef.IP;
				m.AlwaysLocal = machDef.AlwaysLocal;
				foreach ( var svcConf in machDef.Services )
                {
                    m.Services.Add(
                        new Service(
                            svcConf.Name,
                            
                            // local
                            machDef.IP,
                            svcConf.Port,
                            
                            // remote
                            "127.0.0.1",
                            ++GatewayManager.LocalPortBase
                        )
                        {
                        }
                    );
                }
				_machines.Add( m );
			}

			{
				// add map for dirigent master
				// search for the machine with the same IP as the master
				var masterMachine = _machines.Find( m => m.IP == _masterIP );
				if( masterMachine is null )
				{
					masterMachine = new Machine();
					masterMachine.IP = _masterIP;
					_machines.Add( masterMachine );
				}
				// search for service with the same port as the master
				var masterService = masterMachine.Services.Find( s => s.LocalPort == _masterPort );
				if (masterService is null)
				{
					masterService = new Service(
						"DIRIGENT",
						masterMachine.IP,
						_masterPort,
						"127.0.0.1",
						++GatewayManager.LocalPortBase
					);
					masterMachine.Services.Add( masterService );
				}
			}
		}

		public IPPort? GetPortMap( string machineId, string serviceName )
		{
			var mach = _machines.Find( m => string.Equals(m.Id, machineId, StringComparison.OrdinalIgnoreCase) );
			if( mach is null )
				return null;

			var svc = mach.Services.Find( s => string.Equals(s.Name, serviceName, StringComparison.OrdinalIgnoreCase) );
			if( svc is null)
				return null;

			return new IPPort()
			{
				IP = svc.GetIP( IsConnected ),
				Port = svc.GetPort( IsConnected )
			};
		}

		// returns null if variables can't be resolved (machine not found etc.)
		public Dictionary<string, string>? GetVariables( string machineId, string serviceName )
		{
			var vars = new Dictionary<string, string>();

			var comp = _machines.Find( m => string.Equals(m.Id, machineId, StringComparison.OrdinalIgnoreCase) );
			if (comp is null)
				return null;

			
			bool remote = IsConnected;
			
			// set service-related variables
			var svc = comp.Services.Find( (x) => x.Name == serviceName );
            if( svc != null )
            {
                vars["SVC_IP"] = svc.GetIP( remote );
                vars["SVC_PORT"] = svc.GetPort( remote ).ToString();
                //vars["SVC_USERNAME"] = svc.UserName;
                //vars["SVC_PASSWORD"] = svc.Password;
            }

            vars["GW_IP"] = IsConnected ? _def.ExternalIP : _def.InternalIP;
            vars["GW_PORT"] = _def.Port.ToString();
            vars["GW_USERNAME"] = _def.UserName;
            vars["GW_PASSWORD"] = _def.Password;

            vars["APP_NEW_GUID"] = Guid.NewGuid().ToString();

			return vars;

		}
	}
}
