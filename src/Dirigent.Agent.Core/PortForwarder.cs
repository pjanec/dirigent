using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent
{
	public class PortForwarder
	{
		Launcher Launcher;

		GatewayDef _gwDef;
		List<GatewaySession.Machine> _machines;

		public PortForwarder( GatewayDef gwDef, IEnumerable<GatewaySession.Machine> machines, ToolsRegistry toolsReg )
		{
			_gwDef = gwDef;
			_machines = new( machines );
			var plinkAppDef = toolsReg.GetAppDef("plink");
			if( plinkAppDef is null ) throw new Exception( "plink tool not defined" );
			var appDefClone = plinkAppDef.Clone();
			appDefClone.CmdLineArgs = BuildPlinkArgs();
			appDefClone.WindowStyle = EWindowStyle.Minimized;
			Launcher = new Launcher( appDefClone, toolsReg.SharedContext, new Dictionary<string, string>() );
		}

		public void Dispose()
		{
			Launcher?.Kill();
			Launcher?.Dispose();
		}

		string BuildPlinkArgs()
		{
			var sb = new StringBuilder();
			// 
			//&plink.exe 10.0.103.7 -l student -pw Zaq1Xsw2 -P 22 -no-antispoof `
			sb.Append( $"{_gwDef.ExternalIP} -P {_gwDef.Port} -l {_gwDef.UserName} -pw {_gwDef.Password} -no-antispoof ");
			foreach( var comp in _machines )
			{
				if( !comp.AlwaysLocal )
				{
					foreach( var svc in comp.Services )
					{
						// -L 7101:192.168.0.101:5900 
						var fwdArg = $"-L {svc.FwdPort}:{svc.LocalIP}:{svc.LocalPort} ";
						sb.Append( fwdArg );
					}
				}
			}

			return sb.ToString();
		}

		
		public bool IsRunning => Launcher != null && Launcher.Running;
		
		public void Start()
		{
			if( Launcher == null ) return;

            if( !Launcher.Running )
            {
                Launcher.Launch();
            }

            if( Launcher.Running )
            {
                Launcher.MoveToForeground();
            }
		}

		public void Stop()
		{
			if( Launcher == null ) return;
			Launcher.Kill();
		}

	}
}
