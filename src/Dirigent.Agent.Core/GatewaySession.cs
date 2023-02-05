using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Dirigent
{

	public class IPPort
	{
		public string IP = "";
		public int Port;
	}
		
    
	public class GatewaySession : Disposable, ISshProvider
	{
		public static string DirigentServiceName = "DIRIGENT";

        GatewayDef _gwdef;
		string _masterIP; 
		int _masterPort; // local behind the gateway
		List<Machine> _machines = new();

		PortForwarder? _portFwd;

		public bool IsConnected => _portFwd is null ? false : _portFwd.IsRunning;
		public GatewayDef Gateway => _gwdef;

		/// <summary> behind the gateway </summary>
		public string MasterIP => _masterIP;
		/// <summary> behind the gateway </summary>
		public int MasterPort => _masterPort;

		public string Host => _gwdef.ExternalIP;
		public string User => _gwdef.UserName;
		public int Port => _gwdef.Port;
		
		public GatewaySession( GatewayDef def, string masterIP, int masterPort )
		{
            _gwdef = def;
			_masterIP = masterIP;
			_masterPort = masterPort;

			PrepareMachines();
			PrepareDirigentMasterPortMapping();
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if( !disposing ) return;

            Close();
        }

		public void Open()
		{
			// init port forwarding
			StartPortForwarding();

			// create UNC symlinks for accessing files behind the gateway
			CreateSymlinks();			
		}

		public void Close()
		{
			_portFwd?.Dispose();
		}

		public bool AreMachinesSame( IEnumerable<MachineDef> machines )
		{
			return _gwdef.Machines.SequenceEqual( machines );
		}

		public class Machine
        {
			public string Id = "";
            public string IP = "";
			public bool AlwaysLocal;
            public List<Service> Services = new List<Service>();
			public List<SshSymLink> SymLinks = new List<SshSymLink>();
		}


		void PrepareMachines()
		{
			_machines.Clear();
			
            foreach( var machDef in _gwdef.Machines )
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
				m.SymLinks = CalcSymLinks( machDef ); // note: this is pass 1 of 2 (the other is in CreateSymlinks)
				_machines.Add( m );
			}

		}

		void PrepareDirigentMasterPortMapping()
		{
			// add map for dirigent master
			// search for the machine with the same IP as the master
			var masterMachine = _machines.Find( m => m.IP == _masterIP );
			if( masterMachine is null )
			{
				masterMachine = new Machine();
				masterMachine.Id = "DIRIGENT_MASTER";
				masterMachine.IP = _masterIP;
				_machines.Add( masterMachine );
			}
			// search for service with the same port as the master
			var masterService = masterMachine.Services.Find( s => s.LocalPort == _masterPort );
			if (masterService is null)
			{
				masterService = new Service(
					DirigentServiceName,
					masterMachine.IP,
					_masterPort,
					"127.0.0.1",
					++GatewayManager.LocalPortBase
				);
				masterMachine.Services.Add( masterService );
			}
		}

		IPPort? GetPortMap( Machine mach, string serviceName )
		{
			var svc = mach.Services.Find( s => string.Equals(s.Name, serviceName, StringComparison.OrdinalIgnoreCase) );
			if( svc is null)
				return null;

			return new IPPort()
			{
				IP = svc.GetIP( IsConnected ),
				Port = svc.GetPort( IsConnected )
			};
		}

		public IPPort? GetPortMapByMachineName( string machineId, string serviceName )
		{
			var mach = _machines.Find( m => string.Equals(m.Id, machineId, StringComparison.OrdinalIgnoreCase) );
			if( mach is null )
				return null;

			return GetPortMap( mach, serviceName );
		}

		public IPPort? GetPortMapByMachineIP( string machineIP, string serviceName )
		{
			var mach = _machines.Find( m => string.Equals(m.IP, machineIP, StringComparison.OrdinalIgnoreCase) );
			if( mach is null )
				return null;

			return GetPortMap( mach, serviceName );
		}

		// returns null if variables can't be resolved (machine not found etc.)
		public IDictionary<string, string> GetVariables( string machineId, string serviceName )
		{
			var vars = new Dictionary<string, string>();

			var comp = _machines.Find( m => string.Equals(m.Id, machineId, StringComparison.OrdinalIgnoreCase) );
			if (comp is null)
				return vars;

			
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

            vars["GW_IP"] = IsConnected ? _gwdef.ExternalIP : _gwdef.InternalIP;
            vars["GW_PORT"] = _gwdef.Port.ToString();
            vars["GW_USERNAME"] = _gwdef.UserName;
            vars["GW_PASSWORD"] = _gwdef.Password;

            vars["APP_NEW_GUID"] = Guid.NewGuid().ToString();

			return vars;

		}



		// for every file share on each machine create a symlink leading to that machine
		//  file share "C" => "C:\" for machine "m1" with IP 10.0.0.1:  
		//  symlink: "m1_C" => "\\10.0.0.1\C
		List<SshSymLink> CalcSymLinks( MachineDef mach )
		{
			List<SshSymLink> links = new List<SshSymLink>();

			foreach (var share in mach.FileShares)
			{
				if( string.IsNullOrEmpty( mach.Id ) || string.IsNullOrEmpty( mach.IP ) )
					continue;
					
				var link = new SshSymLink();
				link.Name = $"{mach.Id}_{share.Name}";
				link.TargetPath = $"\\\\{mach.IP}\\{share.Name}";
				link.LocalPath = System.IO.Path.GetFullPath( share.Path ); // normalizes to backslashes
				link.GatewayDir = ""; // will be set when the links are created
				links.Add( link );
			}

			return links;
		}

		void StartPortForwarding()
		{
			SetStatus( "Enabling port forwarding...", timeoutMsec: 1000 );

			if ( _portFwd is null )
			{
				_portFwd = new PortForwarder( _gwdef, _machines );
				_portFwd.Start();
			}

			SetStatus("");
		}

		// creates symlinks on the gateway machine and update the links list
		void CreateSymlinks()
		{
			// Generate a script that
			//  1. creates the symlinks
			//  2. tells us what folder on the gateway to find them (first line of stdout)

			SetStatus( "Creating symlinks..." );
			
			var linksDir = "";	// empty = failure

			const string MARKER_LINKSDIR="***LINKSDIR***";
			const string MARKER_EOLN = "***EOLN***";
			const string MARKER_EOF="***END***";
			var script = new List<string>();
			script.Add( $"@echo off" );
			script.Add( $"set LINKSDIR=%USERPROFILE%\\.dirigent\\Links" );
			script.Add( $"echo {MARKER_LINKSDIR}%LINKSDIR%{MARKER_EOLN}" );
			script.Add( $"" );
			script.Add( $"mkdir %LINKSDIR%" );
			script.Add( $"pushd %LINKSDIR%" );
			script.Add( $"" );

			foreach (var m in _machines)
			{
				foreach( var link in m.SymLinks )
				{
					script.Add( $"rmdir {link.Name}" );
					script.Add( $"mklink /D {link.Name} {link.TargetPath}" );
					script.Add( $"" );
				}
			}

			script.Add( $"popd" );
			script.Add( $"echo {MARKER_EOF}" );


			// run the script on the gateway
			
			ConnectionInfo connection = new ConnectionInfo(
				_gwdef.ExternalIP, _gwdef.Port, _gwdef.UserName,
				new PasswordAuthenticationMethod( _gwdef.UserName, _gwdef.Password ) );

			var output = new List<string>();
			using( SshClient client = new SshClient(connection) )
			{
				client.Connect();
				
				var stream = client.CreateShellStream("cmd", 80, 50, 1024, 1024, 1024);
				foreach( var line in script )
				{
					stream.WriteLine(line);
				}
		        stream.Flush();
				
				while(true)
				{
					var line = stream.ReadLine( TimeSpan.FromSeconds( 5 ) ); // poll until there is a timeout 
					if (string.IsNullOrEmpty( line ))
						break; // no more lines (timeout)
						
					line = Tools.RemoveAnsiEscapeSequences( line );
					var lines = line.Split( MARKER_EOLN );
					foreach (var l in lines)
					{
						if (!string.IsNullOrEmpty( l ))
							output.Add( l );
					}

					if (line.StartsWith(MARKER_EOF))
						break;
						
					output.Add(line);
				}
				stream.Close();
				client.Disconnect();
			}

			// extract the links dir from the output
			foreach( var line in output )
			{
				if (line.StartsWith( MARKER_LINKSDIR ))
				{
					var path = line.Substring( line.IndexOf( MARKER_LINKSDIR ) + MARKER_LINKSDIR.Length );
					linksDir = PathTools.ConvertWindowsPathToSshPath(path);
					break;
				}
			}

			// update the gateway links dir in the symlinks
			bool linksCreated = !string.IsNullOrEmpty( linksDir );
			foreach (var m in _machines)
			{
				if( linksCreated )
				{
					foreach (var link in m.SymLinks)
					{
						link.GatewayDir = linksDir;
					}
				}
				else // links very likely don't exist, we can't construct file paths -> clear the list
				{
					m.SymLinks.Clear();
				}
			}

			if( linksCreated )
			{
				SetStatus( "" );
			}
			else
			{
				SetStatus( "Links not created!", "error", 5000 );
			}
		}

		public IEnumerable<SshSymLink> GetSymLinks( string machineId )
		{
			var mach = _machines.Find( ( x ) => x.Id == machineId );
			if (mach != null)
				return mach.SymLinks;
			else
				return new List<SshSymLink>();
		}

		void SetStatus( string text, string type = "", int timeoutMsec = -1 )
		{
			AppMessenger.Instance.Send( new AppMessages.StatusText( "SSH", text, type, timeoutMsec ) );
		}

		public async Task DownloadAsync( string localPath, string remotePath )
		{
			using var copier = new SshFileCopier( _gwdef.ExternalIP, _gwdef.Port, _gwdef.UserName, _gwdef.Password );
			await copier.DownloadAsync( localPath, remotePath );
		}
		
		public async Task UploadAsync( string localPath, string remotePath )
		{
			using var copier = new SshFileCopier( _gwdef.ExternalIP, _gwdef.Port, _gwdef.UserName, _gwdef.Password );
			await copier.UploadAsync( localPath, remotePath );
		}

		// can we handle given path, is it pointing to our gateway?
		public bool IsCompatiblePath( string sshPath )
		{
			if( PathTools.TryParseSshPath( sshPath, out var pp ) )
			{
				if (pp.Host == this.Host && pp.Port == this.Port && pp.User == this.User )
					return true;
			}
			return false;
		}
	}
}
