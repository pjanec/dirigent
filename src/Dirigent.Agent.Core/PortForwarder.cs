using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Dirigent
{
	public class PortForwarder : Disposable
	{
		SshClient _sshClient;
		GatewayDef _gwDef;
		List<GatewaySession.Machine> _machines;
		List<ForwardedPortLocal> _forwardedPorts = new List<ForwardedPortLocal>();


		public PortForwarder( GatewayDef gwDef, IEnumerable<GatewaySession.Machine> machines )
		{
			_gwDef = gwDef;
			_machines = new( machines );
			
			_sshClient = new SshClient( _gwDef.ExternalIP, _gwDef.Port, _gwDef.UserName, _gwDef.Password );
			_sshClient.ConnectionInfo.Timeout = TimeSpan.FromSeconds( 5 );
			_forwardedPorts = BuildPortForwardList();
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if( !disposing ) return;
			Stop();
			_sshClient.Dispose();
		}

		List<ForwardedPortLocal> BuildPortForwardList()
		{
			var list = new List<ForwardedPortLocal>();

			foreach( var comp in _machines )
			{
				if( !comp.AlwaysLocal )
				{
					foreach( var svc in comp.Services )
					{
						var pf = new ForwardedPortLocal( "127.0.0.1", (uint)svc.FwdPort, svc.LocalIP, (uint)svc.LocalPort );
						list.Add( pf );
					}
				}
			}

			return list;
		}

		
		public bool IsRunning => _sshClient.IsConnected;
		
		public void Start()
		{
			if( _sshClient.IsConnected ) return;
			
			bool hadFwdPorts = _sshClient.ForwardedPorts.Any();

			if( hadFwdPorts )
			{
				Stop();	// removes the ports
			}

			_sshClient.Connect();

			foreach (var pf in _forwardedPorts)
			{
				_sshClient.AddForwardedPort( pf );
				pf.Start();
			}
		}

		public void Stop()
		{
			var pfList = _sshClient.ForwardedPorts.ToList();
			foreach( var pf in pfList )
			{
				pf.Stop();
				_sshClient.RemoveForwardedPort( pf );
			}

			_sshClient.Disconnect();
		}

	}
}
