using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;
using Dirigent.Common;

namespace Dirigent.Agent.TrayApp
{
    /// <summary>
    /// Launches and keeps running the Dirigent's Master process
    /// Process must be locate next to the TrayApp executable!
    /// </summary>
    public class MasterRunner : IDisposable
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        Process proc = null;

        [DllImport("user32.dll",EntryPoint = "ShowWindow",SetLastError = true)]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;
        private const int SW_HIDE = 0;

        private bool consoleShown = false; // last known state
        private Thread keepAliveThread = null;
        private bool quitKeepAliveThread = false;

        public int MasterPort = -1; // master's TCP port; empty=default
        public string SharedConfigFile = ""; // empty = default
        public string LocalConfigFile = ""; // empty = default
        public string StartupPlan = "";
        public int CLIPort = -1;
        
        /// <summary>
        /// Starts the dirigent master process. Throw exception!
        /// </summary>
        /// <remarks>
        /// master.exe process must reside in same directory as agent's process
        /// </remarks>
        public void Launch()
        {
            var psi = new ProcessStartInfo();
			var appPath = Tools.GetExeDir()+"\\Dirigent.Master.exe";
			psi.FileName =  appPath;
            psi.Arguments = string.Format("--ParentAgentPid {0} ", Process.GetCurrentProcess().Id ); // indicate master have been run as part of an agent
            if( MasterPort > 0 )
            {
                psi.Arguments += string.Format("--masterPort {0} ", MasterPort);
            }
            if( CLIPort > 0 )
            {
                psi.Arguments += string.Format("--CLIPort {0} ", CLIPort);
            }
            if( !string.IsNullOrEmpty(SharedConfigFile) )
            {
                psi.Arguments += string.Format("--sharedConfigFile {0} ", SharedConfigFile);
            }
            if( !string.IsNullOrEmpty(StartupPlan) )
            {
                psi.Arguments += string.Format("--startupPlan {0} ", StartupPlan);
            }

            psi.WorkingDirectory = System.IO.Directory.GetCurrentDirectory(); // Tools.GetExeDir();
            psi.WindowStyle = ProcessWindowStyle.Minimized;
			psi.UseShellExecute = false; // allows us using environment variables

			try
            {
                log.DebugFormat("StartProc exe \"{0}\", cmd \"{1}\", dir \"{2}\", windowstyle {3}", psi.FileName, psi.Arguments, psi.WorkingDirectory, psi.WindowStyle );
                proc = Process.Start(psi);
                if( proc != null )
                {
                    log.DebugFormat("StartProc SUCCESS pid {0}", proc.Id );
                    
                    // let the console show up
                    Thread.Sleep(500); 

                    // hide console if it should not be shown
                    if( !consoleShown )
                    {
                        IsConsoleShown = false; // hide the console window completely
                    }
                }
                else
                {
                    log.DebugFormat("StartProc FAILED (no details)" );
                    proc = null;
                }
            }
            catch (Exception ex)
            {
                log.DebugFormat("StartProc FAILED except {0}", ex.Message );
                throw new Exception(String.Format("Failed to run Dirigent Master process {0} from {1}", psi.FileName, psi.WorkingDirectory));
            }
        }

        public void Kill()
        {
            if( proc ==null ) return;
            if( proc.HasExited ) return;

            proc.Kill();
            log.DebugFormat("Master killed (pid {0})", proc.Id );
        }

        /// <summary>
        /// is Master process currently running?
        /// </summary>
        public bool IsRunning
        {
            get
            {
                if( proc != null)
                {
                    return !proc.HasExited;
                }
                return false;
            }
        }


        /// <summary>
        /// Call this periodically to check whether the master process is still running and restart it if it isn't
        /// </summary>
        public void StartKeepAlive()
        {
            if( keepAliveThread != null ) return;

            quitKeepAliveThread = false;
            keepAliveThread = new Thread( () => { KeepAliveThreadFunction(); } );
            keepAliveThread.Start();
        }

        public void StopKeepAlive()
        {
            if( keepAliveThread == null ) return;
            quitKeepAliveThread = true;
            keepAliveThread.Join(2000);
        }

        private void KeepAliveThreadFunction()
        {
            while(!quitKeepAliveThread )
            {
                // check if still running
                if( !IsRunning )
                {
                    // restart
                    try
                    {
                        Launch();
                    }
                    catch (Exception)
                    {
                        // ignore exceptions
                    }
                }
                
                Thread.Sleep(5000);
            }
        }

        /// <summary>
        /// Show/hide master's console
        /// </summary>
        public bool IsConsoleShown
        {
            get
            {
                return consoleShown;
            }

            set
            {
                if( value ) // show
                {
                    if( proc == null ) return;
                    ShowWindow( proc.MainWindowHandle, SW_RESTORE );
                    consoleShown = true;
                }
                else
                {
                    if( proc == null ) return;
                    ShowWindow( proc.MainWindowHandle, SW_HIDE );
                    consoleShown = false;
                }

            }
        }

        public void Dispose()
        {
            StopKeepAlive();
            Kill();
        }

        public void KillAllExistingMasterProcess()
        {
            Process[] localByName = Process.GetProcessesByName("Dirigent.Master");
            foreach( var proc in localByName )
            {
                proc.Kill();
            }
        }


    }
}
