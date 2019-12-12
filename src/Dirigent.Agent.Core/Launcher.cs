using System;
using System.IO;
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
        string RelativePathsRoot;

        public Launcher( AppDef appDef, String rootForRelativePaths )
        {
            this.appDef = appDef;

            if (String.IsNullOrEmpty(rootForRelativePaths))
            {
                RelativePathsRoot = System.IO.Directory.GetCurrentDirectory();
            }
            else
            {
                RelativePathsRoot = rootForRelativePaths;
            }
        }

        string BuildAbsolutePath( string anyPath )
        {
            if( Path.IsPathRooted( anyPath ) )
                return anyPath;

            return Path.Combine( RelativePathsRoot, anyPath );
        }

        public void Launch()
        {
			var appPath = System.Environment.ExpandEnvironmentVariables(appDef.ExeFullPath);

            // try to adopt an already running process (matching by process image file name, regardless of path)
            if( appDef.AdoptIfAlreadyRunning )
            {
                ProcInfo found = FindProcessByExeName( appPath );
                if( found != null )
                {
                    log.DebugFormat("Adopted existing process pid={0}, cmd=\"{1}\", dir=\"{2}\"", found.Process.Id, found.CmdLine, found.Process.StartInfo.WorkingDirectory );
                    proc = found.Process;
                    return;
                }
            }
        

			// set environment variables here so we can use them when expanding process path/args/cwd
			Environment.SetEnvironmentVariable("DIRIGENT_MACHINEID", appDef.AppIdTuple.MachineId);
			Environment.SetEnvironmentVariable("DIRIGENT_APPID", appDef.AppIdTuple.AppId);

            // start the process
            var psi = new ProcessStartInfo();
			psi.FileName =  BuildAbsolutePath( appPath );
			if( appDef.CmdLineArgs != null )
			{
				psi.Arguments = System.Environment.ExpandEnvironmentVariables(appDef.CmdLineArgs);
			}
            if (appDef.StartupDir != null)
            {
				var dir = System.Environment.ExpandEnvironmentVariables(appDef.StartupDir);
                psi.WorkingDirectory = BuildAbsolutePath(dir);
            }
            psi.WindowStyle = appDef.WindowStyle;
			
			psi.UseShellExecute = false; // allows us using environment variables

			//
			// modify the environment
			//
			foreach (var x in appDef.EnvVarsToSet)
			{							
				var name = x.Key;
				var value = System.Environment.ExpandEnvironmentVariables(x.Value);
				psi.EnvironmentVariables[name] = value;
			}
			if (!String.IsNullOrEmpty(appDef.EnvVarPathToAppend))
			{
				var name = "PATH";
				var postfix = System.Environment.ExpandEnvironmentVariables(appDef.EnvVarPathToAppend);
				// if relative path is specified, consider it relative to SharedConfig and make it absolute (per each ';' separated segment)
				postfix = string.Join(";", postfix.Split(';').Select(p => BuildAbsolutePath(p)));
				psi.EnvironmentVariables[name] = psi.EnvironmentVariables[name] + ";" + postfix;
			}
			if (!String.IsNullOrEmpty(appDef.EnvVarPathToPrepend))
			{
				var name = "PATH";
				var prefix = System.Environment.ExpandEnvironmentVariables(appDef.EnvVarPathToPrepend);
				// if relative path is specified, consider it relative to SharedConfig and make it absolute (per each ';' separated segment)
				prefix = string.Join(";", prefix.Split(';').Select(p => BuildAbsolutePath(p)));
				psi.EnvironmentVariables[name] = prefix + ";" + psi.EnvironmentVariables[name];
			}

			try
            {
                log.DebugFormat("StartProc exe \"{0}\", cmd \"{1}\", dir \"{2}\", windowstyle {3}", psi.FileName, psi.Arguments, psi.WorkingDirectory, psi.WindowStyle );
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
        private static void KillProcessAndChildren(int pid, int indent, bool killSoftly)
        {
            Process proc;
            try
            {
                proc = Process.GetProcessById(pid);
                log.DebugFormat(new String(' ', indent)+"KillTree pid {0}, name \"{1}\"", pid, proc.ProcessName );
            }
            catch( ArgumentException )
            {
                log.DebugFormat(new String(' ', indent)+"KillTree pid {0} - NOT RUNNING", pid );
                return;
            }

            // kill children
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection moc = searcher.Get();
            foreach (ManagementObject mo in moc)
            {
                var childPid = Convert.ToInt32(mo["ProcessID"]);
                    
                Process childProc = null;
                try { childProc = Process.GetProcessById(childPid); }
                catch( ArgumentException ) {
                    log.DebugFormat(new String(' ', indent)+"KillTree ChildProc pid {0} - NOT RUNNING", childPid );
                }

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
                        KillProcessAndChildren( childPid, indent+2, killSoftly );
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
            if( !proc.HasExited )
            {
                try
                {
                    if (killSoftly)
                    {
                        proc.CloseMainWindow();
                    }
                    else
                    {
                        proc.Kill();
                    }
                    log.DebugFormat(new String(' ', indent)+"Kill pid {0} DONE", proc.Id );
                }
                catch(Exception ex )
                {
                    log.DebugFormat(new String(' ', indent)+"Kill pid {0} EXCEPTION {1}", proc.Id, ex );
                }
            }
            else
            {
                log.DebugFormat(new String(' ', indent)+"     pid {0} already exited.", proc.Id );
            }

        }
        
        public void Kill()
        {
            //log.DebugFormat("Kill pid {0}", proc.Id );
            // bool IsRunningOnMono = (Type.GetType("Mono.Runtime") != null);
            
            if( proc == null )
            {
                log.DebugFormat("  pid {0} proc=null!", ProcessId);
                return;
            }

            if( proc.HasExited )
            {
                log.DebugFormat("  pid {0} already exited.", proc.Id );
                proc = null;
                return;
            }

            // kill the process and wait until it dies
            if (appDef.KillTree)
            {
                KillProcessAndChildren(proc.Id, 0, appDef.KillSoftly);
            }
            else
            {
                log.DebugFormat("Kill pid {0}, name \"{1}\"", proc.Id, proc.ProcessName );
                try
                {
                    if (appDef.KillSoftly)
                    {
                        proc.CloseMainWindow();   
                    }
                    else
                    {
                        proc.Kill();
                    }
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

        public bool Running
        {
            get
            {
                try
                {
                    if (proc != null && !proc.HasExited)
                    {
                        return true;
                    }
                    return false;
                }
                catch // if we adopted a foreign process or otherwise we can't get the exit code
                {
                    return false;
                }
            }
        }

        public int ExitCode
        {
            get
            {
                try
                {
                    if (proc != null && proc.HasExited)
                    {
                        return proc.ExitCode;
                    }
                    return 0; // default
                }
                catch // if we adopted a foreign process or otherwise we can't get the exit code
                {
                    return -1;
                }
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

        public class ProcInfo
        {
            public Process Process;
            public String Path;
            public String CmdLine;
        }

        public static ProcInfo FindProcessByExeName( string exePath )
        {
            string searchedExeName = Path.GetFileName(exePath);
    
            // find all processes
            var wmiQueryString = "SELECT ProcessId, ExecutablePath, CommandLine FROM Win32_Process";
            using (var searcher = new ManagementObjectSearcher(wmiQueryString))
            using (var results = searcher.Get())
            {
                var query = from p in Process.GetProcesses()
                            join mo in results.Cast<ManagementObject>()
                            on p.Id equals (int)(uint)mo["ProcessId"]
                            select new
                            {
                                Process = p,
                                Path = (string)mo["ExecutablePath"],
                                CommandLine = (string)mo["CommandLine"],
                            };
                foreach (var i in query)
                {
                    if( i.Path != null )
                    {
                        string exeName = Path.GetFileName(i.Path);
                        if( String.Compare(exeName, searchedExeName, true ) == 0 ) // exe name matches?
                        {
                            return new ProcInfo {
                                Process = i.Process,
                                Path = i.Path,
                                CmdLine = i.CommandLine
                            };
                        }
                    }
                }
            }

            return null;
        }
    }

    public class LauncherFactory : ILauncherFactory
    {
        public ILauncher createLauncher( AppDef appDef, string rootForRelativePaths )
        {
            return new Launcher( appDef, rootForRelativePaths );
        }
    }

}