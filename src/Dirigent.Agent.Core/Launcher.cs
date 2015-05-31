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
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

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
                log.DebugFormat("StartProc exe \"{0}\", cmd \"{1}\", dir \"{2}\", windowstyle {3}", appDef.ExeFullPath, appDef.CmdLineArgs, appDef.StartupDir, appDef.WindowStyle );
                proc = Process.Start(psi);
                if( proc != null )
                {
                    log.DebugFormat("StartProc SUCCESS pid {0}", proc.Id );
                }
                else
                {
                    log.DebugFormat("StartProc FAILED (no details)" );
                }
            }
            catch (Exception ex)
            {
                log.DebugFormat("StartProc FAILED except {0}", ex.Message );
                throw new AppStartFailureException(appDef.AppIdTuple, ex.Message, ex);
            }
        }

        /// <summary>
        /// Kill a process, and all of its children.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        private static void KillProcessAndChildren(int pid, int indent)
        {
            try
            {
                Process proc = Process.GetProcessById(pid);
                log.DebugFormat(new String(' ', indent)+"KillTree pid {0}, name \"{1}\"", pid, proc.ProcessName );

                // kill children
                ManagementObjectSearcher searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
                ManagementObjectCollection moc = searcher.Get();
                foreach (ManagementObject mo in moc)
                {
                    var childPid = Convert.ToInt32(mo["ProcessID"]);
                    
                    Process childProc = null;
                    try { childProc = Process.GetProcessById(childPid); }
                    catch( ArgumentException ) {}

                    if( childProc != null ) // child process still alive
                    {
                        // check if the child process found is not older than its presumable parent, i.e. that we are not a victiom if pid reuse
                        // we should not kill children of another (already dead) parent!
                        // note: the pid are reused by the system; if another parent process died its pid could have been
                        // already recycled and assigned to a process we are killing; then a completely unrelated children
                        // would be find using false parent's pid...
                        // see also http://blogs.msdn.com/b/oldnewthing/archive/2015/04/03/10605029.aspx
                        if( childProc.StartTime >= proc.StartTime )
                        {
                            KillProcessAndChildren( childPid, indent+2 );
                        }
                        else
                        {
                            // child older than its parent? then it is NOT the parent's child!
                            log.DebugFormat(new String(' ', indent)+"Ignoring pid {0}, name \"{1}\" - NOT a real child of {2}, created before its parent!", childPid, proc.ProcessName, pid );
                        }
                    }
                }
                
                // kill process at current level
                log.DebugFormat(new String(' ', indent)+"Kill pid {0}, name \"{1}\"", proc.Id, proc.ProcessName );
                try
                {
                    proc.Kill();
                    log.DebugFormat(new String(' ', indent)+"Kill pid {0} DONE", proc.Id );
                }
                catch(Exception ex )
                {
                    log.DebugFormat("Kill pid {0} EXCEPTION {1}", proc.Id, ex );
                }
            }
            catch( ArgumentException )
            {
                log.DebugFormat(new String(' ', indent)+"KillTree pid {0} - NOT RUNNING", pid );
            }

        }
        
        public void Kill()
        {
            //log.DebugFormat("Kill pid {0}", proc.Id );
            // bool IsRunningOnMono = (Type.GetType("Mono.Runtime") != null);
            
            // kill the process and wait until it dies
            if( proc != null  && !proc.HasExited )
            {
                if (appDef.KillTree)
                {
                    KillProcessAndChildren(proc.Id, 0);
                }
                else
                {
                    log.DebugFormat("Kill pid {0}, name \"{1}\"", proc.Id, proc.ProcessName );
                    try
                    {
                        proc.Kill();
                        log.DebugFormat("Kill pid {0} DONE", proc.Id );
                    }
                    catch(Exception ex )
                    {
                        log.DebugFormat("Kill pid {0} EXCEPTION {1}", proc.Id, ex );
                    }
                }

                log.DebugFormat("WaitForExit pid {0}", proc.Id );
                proc.WaitForExit();
                log.DebugFormat("WaitForExit pid {0} DONE", proc.Id );
                proc = null;
            }
            else
            {
                log.DebugFormat(" pid {0} not running", proc.Id );
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

        public int ProcessId
        {
            get
            {
                if (proc != null ) return proc.Id;
                return -1;
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
