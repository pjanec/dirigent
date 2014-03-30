using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Management;

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
            psi.WindowStyle = appDef.WindowStyle;

            try
            {
                proc = Process.Start(psi);
            }
            catch (Exception ex)
            {
                throw new AppStartFailureException(appDef.AppIdTuple, ex.Message, ex);
            }
        }

        /// <summary>
        /// Kill a process, and all of its children.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        private static void KillProcessAndChildren(int pid)
        {
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection moc = searcher.Get();
            foreach (ManagementObject mo in moc)
            {
                KillProcessAndChildren(Convert.ToInt32(mo["ProcessID"]));
            }
            try
            {
                Process proc = Process.GetProcessById(pid);
                proc.Kill();
            }
            catch (ArgumentException)
            {
                // Process already exited.
            }
        }
        
        public void Kill()
        {
            // bool IsRunningOnMono = (Type.GetType("Mono.Runtime") != null);
            
            // kill the process and wait until it dies
            if( proc != null  && !proc.HasExited )
            {
                if (appDef.KillTree)
                {
                    KillProcessAndChildren(proc.Id);
                }
                else
                {
                    proc.Kill();
                }

                proc.WaitForExit();
                proc = null;
            }
        }

        public bool Running
        {
            get
            {
                if (proc != null && !proc.HasExited)
                {
                    return true;
                }
                return false;
            }
        }

        public int ExitCode
        {
            get
            {
                if (proc != null && proc.HasExited)
                {
                    return proc.ExitCode;
                }
                return 0; // default
            }
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
