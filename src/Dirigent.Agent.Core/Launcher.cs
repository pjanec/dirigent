using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using Dirigent.Common;

namespace Dirigent.Agent.Core
{
    public class Launcher : ILauncher
    {
        Process proc;
        AppDef appDef;

        public Launcher( AppDef appDef )
        {
            this.appDef = appDef;
        }

        public void Launch()
        {
            // start the process
            var psi = new ProcessStartInfo();
            psi.FileName =  appDef.ExeFullPath;
            psi.Arguments = appDef.CmdLineArgs;
            psi.WorkingDirectory = appDef.StartupDir;

            proc = Process.Start( psi );
        }

        public void Kill()
        {
            // kill the process and wait until it dies
            if( proc != null  && !proc.HasExited )
            {
                proc.Kill();
                proc.WaitForExit();
                proc = null;
            }
        }

        public bool IsRunning()
        {
            if( proc != null  && !proc.HasExited )
            {
                return true;
            }
            return false;
        }
    }

    public class LauncherFactory : ILauncherFactory
    {
        public ILauncher createLauncher( AppDef appDef )
        {
            return new Launcher( appDef );
        }
    }
}
