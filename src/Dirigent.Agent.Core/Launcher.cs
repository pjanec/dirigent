using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Management;

using Dirigent.Common;
using System.Xml.Linq;
using X = Dirigent.Common.XmlConfigReaderUtils;
using System.Runtime.InteropServices;

namespace Dirigent.Agent.Core
{
    public class Launcher
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        IDirigentControl ctrl;
        Process proc;
        AppDef appDef;
        string RelativePathsRoot;
        string planName; // in what plan's context the app is going to be started (just informative)
        string masterIP; // ip address of the Dirigent Master process

		bool dying = false;	// already killed but still in the system
		int exitCode = 0; // cached exit code from last run
        
        Dictionary<string, string> internalVars;

        SoftKiller softKiller;

        CommandRepository cmdRepo;

        // Mechanism for hard kill if multiple kills are sent while the process is being killed
        // (likely using a kill sequnce) but still not dead (kill actions not effective and user is impatient,
        // clicking the kill button multiple times
        int numKillOrdersToForcedHardKill = 7; // how many extra kill commands needs to be isuued to hard kill the process if still being soft-killed.
        int remainingKillOrdersToForcedHardKill = -1; // how many kill commands currently left before forced hard kill

        public Launcher( IDirigentControl ctrl, AppDef appDef, String rootForRelativePaths, string planName, string masterIP, Dictionary<string, string> internalVars )
        {
            this.ctrl = ctrl;
            if( ctrl == null ) throw new ArgumentNullException("ctrl", "Valid network-bound Dirigent Control required"); 
            this.appDef = appDef;

            if (String.IsNullOrEmpty(rootForRelativePaths))
            {
                RelativePathsRoot = System.IO.Directory.GetCurrentDirectory();
            }
            else
            {
                RelativePathsRoot = rootForRelativePaths;
            }

            this.planName = planName;
            this.masterIP = masterIP;

            this.internalVars = BuildVars(appDef, internalVars);

            cmdRepo = new CommandRepository( ctrl );
            DirigentCommandRegistrator.Register( cmdRepo );

            softKiller = new SoftKiller( appDef );

            // handle the KillSoftly flag for backward compatibility
            if( appDef.KillSoftly )
            {
                if( softKiller.IsDefined )
                {
                    log.ErrorFormat("{0}: KillSoftly can't be used together with SoftKill! Using just the SoftKill...", appDef.AppIdTuple);
                }
                else // add a single Close action to the soft kill seq, with default timeout
                {
                    softKiller.AddClose();
                }
            }

        }

		public void Dispose()
		{
            softKiller.Dispose();
		}

        public void Tick()
        {
            checkExited();

            if( softKiller.IsRunning )
            {
                if( !softKiller.Tick() ) // have we failed to kill the process using a kill sequence?
                {
                    KillExeHard();
                }
            }
        }

		
        Dictionary<string, string> BuildVars( AppDef appDef, Dictionary<string, string> internalVars )
        {
            // start wit externally defined (global) internal vars
            var res = new Dictionary<string, string>(internalVars);

            // add the local variables from appdef
            foreach( var kv in appDef.LocalVarsToSet )
            {
                res[kv.Key] = kv.Value;
            }

            return res;
        }
        
        string ExpandVars( String str )
		{
            if( String.IsNullOrEmpty( str ) ) return String.Empty;

			var s = Tools.ExpandEnvVars(str, true);
			//s = ExpandNumericVars( s, numericParams, true );
			s = Tools.ExpandInternalVars( s, internalVars, true ); 
            s = Tools.RemoveVars( s ); // replace the remaining vars with en empty string
			return s;
		}

       string BuildAbsolutePath( string anyPath )
        {
            if( Path.IsPathRooted( anyPath ) )
                return anyPath;

            return Path.Combine( RelativePathsRoot, anyPath );
        }

        public bool AdoptAlreadyRunning()
        {
            var appPath = ExpandVars(appDef.ExeFullPath);
            ProcInfo found = FindProcessByExeName( appPath );
            if( found != null )
            {
                log.DebugFormat("Adopted existing process pid={0}, cmd=\"{1}\", dir=\"{2}\"", found.Process.Id, found.CmdLine, found.Process.StartInfo.WorkingDirectory );
                proc = found.Process;
                return true;
            }
            return false;
        }

        string GetCmdExePath()
        {
            return Environment.GetEnvironmentVariable("ComSpec");
        }

        string GetPowershellExePath()
        {
            return Environment.ExpandEnvironmentVariables(@"%windir%\system32\WindowsPowerShell\v1.0\powershell.exe");
        }

        enum EExeType
        {
            Executable,   // normal executable startable via Process.Start
            DirigentCmd   // dirigent command(s) in text form
        }

        struct ParsedExe
        {
            public EExeType ExeType;
            public string Path;
            public string CmdLine;
        }

        ParsedExe ParseExe()
        {
            ParsedExe pe = new ParsedExe();

            // resolve reserved exe names
            switch( appDef.ExeFullPath.ToLower() )
            {
                // just cmd shell
                case "[cmd]":
                    pe.ExeType = EExeType.Executable;
                    pe.Path = GetCmdExePath();
                    pe.CmdLine = ExpandVars(appDef.CmdLineArgs);
                    break;

                case "[cmd.file]":
                case "[cmd.command]":
                    pe.ExeType = EExeType.Executable;
                    pe.Path = GetCmdExePath();
                    pe.CmdLine = "/c "+ExpandVars(appDef.CmdLineArgs);
                    break;

                // just powershell with any command line
                case "[powershell]":
                    pe.ExeType = EExeType.Executable;
                    pe.Path = GetPowershellExePath();
                    pe.CmdLine = ExpandVars(appDef.CmdLineArgs);
                    break;

                // powershell script file specified in appDef.CmdLineArgs;
                case "[powershell.file]":
                    pe.ExeType = EExeType.Executable;
                    pe.Path = GetPowershellExePath();
                    pe.CmdLine = "-executionpolicy bypass -file "+ExpandVars(appDef.CmdLineArgs);
                    break;

                // powershell command specified in CmdLineArgs
                case "[powershell.command]":
                    pe.ExeType = EExeType.Executable;
                    pe.Path = GetPowershellExePath();
                    pe.CmdLine = "-executionpolicy bypass -command \""+ExpandVars(appDef.CmdLineArgs)+"\"";
                    break;


                // dirigent.AgentCmd command line
                case "[dirigent.command]":
                    pe.ExeType = EExeType.DirigentCmd;
                    pe.Path = null;
                    pe.CmdLine = ExpandVars( appDef.CmdLineArgs );
                    break;
                
                default:
                    pe.ExeType = EExeType.Executable;
                    pe.Path = ExpandVars( appDef.ExeFullPath );
                    pe.CmdLine = ExpandVars( appDef.CmdLineArgs );
                    break;
            }

            return pe;
        }


        
        public void Launch()
        {
			// don't run again if not yet killed
			if( dying ) return;
			
			// not exited yet
			exitCode = 0;


            // set environment variables here so we can use them when expanding process path/args/cwd
            Environment.SetEnvironmentVariable("DIRIGENT_SHAREDCONFDIR", RelativePathsRoot);
            Environment.SetEnvironmentVariable("DIRIGENT_PLAN", planName);
            Environment.SetEnvironmentVariable("DIRIGENT_MACHINEID", appDef.AppIdTuple.MachineId);
            Environment.SetEnvironmentVariable("DIRIGENT_APPID", appDef.AppIdTuple.AppId);
            Environment.SetEnvironmentVariable("DIRIGENT_MASTER_IP", masterIP);


            var pe = ParseExe();

            switch( pe.ExeType )
            {
                case EExeType.DirigentCmd:
                    LaunchDirigentCmd( pe );
                    break;
                default:
                    LaunchExe( pe );
                    break;
            }

        }


        void LaunchExe( ParsedExe pe )
        {
            // try to adopt an already running process (matching by process image file name, regardless of path)
            if( appDef.AdoptIfAlreadyRunning )
            {
                ProcInfo found = FindProcessByExeName( pe.Path );
                if( found != null )
                {
                    log.DebugFormat("Adopted existing process pid={0}, cmd=\"{1}\", dir=\"{2}\"", found.Process.Id, found.CmdLine, found.Process.StartInfo.WorkingDirectory );
                    proc = found.Process;
                    return;
                }
            }

            remainingKillOrdersToForcedHardKill = numKillOrdersToForcedHardKill;

            // start the process
            var psi = new ProcessStartInfo();
			psi.FileName = BuildAbsolutePath( pe.Path );
			if( !String.IsNullOrEmpty( pe.CmdLine ) )
			{
				psi.Arguments = pe.CmdLine;
			}
            if (appDef.StartupDir != null)
            {
				var dir = ExpandVars(appDef.StartupDir);
                psi.WorkingDirectory = BuildAbsolutePath(dir);
            }

			switch( appDef.WindowStyle )
			{
				case EWindowStyle.Normal: psi.WindowStyle = ProcessWindowStyle.Normal; break;
				case EWindowStyle.Minimized: psi.WindowStyle = ProcessWindowStyle.Minimized; break;
				case EWindowStyle.Maximized: psi.WindowStyle = ProcessWindowStyle.Maximized; break;
				case EWindowStyle.Hidden: psi.WindowStyle = ProcessWindowStyle.Hidden; break;
				default: psi.WindowStyle = ProcessWindowStyle.Normal; break;
			}
			
			psi.UseShellExecute = false; // allows us using environment variables

			//
			// modify the environment
			//
			foreach (var x in appDef.EnvVarsToSet)
			{							
				var name = x.Key;
				var value = ExpandVars(x.Value);
				psi.EnvironmentVariables[name] = value;
			}
			if (!String.IsNullOrEmpty(appDef.EnvVarPathToAppend))
			{
				var name = "PATH";
				var postfix = ExpandVars(appDef.EnvVarPathToAppend);
				// if relative path is specified, consider it relative to SharedConfig and make it absolute (per each ';' separated segment)
				postfix = string.Join(";", postfix.Split(';').Select(p => BuildAbsolutePath(p)));
				psi.EnvironmentVariables[name] = psi.EnvironmentVariables[name] + ";" + postfix;
			}
			if (!String.IsNullOrEmpty(appDef.EnvVarPathToPrepend))
			{
				var name = "PATH";
				var prefix = ExpandVars(appDef.EnvVarPathToPrepend);
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

					// Note: WindowStyle in CreateProcess somehow does not work on console windows,
					// probably when the window creation happens later in the process life
					// Please use MainWindowStyles app watcher to set the style for such cases..
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


			if (proc != null)
			{
                try
                {
                    SetPriorityClass( appDef.PriorityClass );
                }
                catch(Exception ex)
                {
                    log.DebugFormat("SetPriority FAILED except {0}", ex.Message );
                }
			}

        }

        void LaunchDirigentCmd( ParsedExe pe )
        {
            var commands = cmdRepo.ParseCmdLine( pe.CmdLine, null );
            foreach( var cmd in commands )
            {
                cmd.Execute();
            }
        }

        /// <summary>
        /// Kill a process, and all of its children.
        /// </summary>
        /// <param name="pid">Process ID.</param>
        private static void KillTree(int pid, int indent, bool includingParent=true)
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

					try
					{
						// this might fail!
						bool isOurChild = childProc.StartTime >= proc.StartTime;

						if( isOurChild )
						{
							KillTree( childPid, indent+2 );
						}
						else
						{
							// child older than its parent? then it is NOT the parent's child!
							log.DebugFormat(new String(' ', indent)+"Ignoring pid {0}, name \"{1}\" - NOT a real child of {2}, created before its parent!", childPid, proc.ProcessName, pid );
						}
					}
					catch( Exception ex )
					{
						log.DebugFormat(new String(' ', indent)+"Failure killing child pid {0}: {1}", childPid, ex.Message );
					}

                }
            }

            if( includingParent )
            {
                // kill process at current level
                log.DebugFormat(new String(' ', indent)+"Kill pid {0}, name \"{1}\"", proc.Id, proc.ProcessName );
                if( !proc.HasExited )
                {
                    try
                    {
                        proc.Kill();
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
        }
        
        private static void KillChildren( int pid )
        {
            KillTree( pid, 0, includingParent: false );
        }


        // kills the process immediately with no mercy (no "softer" stop actions applied)
        void KillExeHard()
        {
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

            // kill the process
            if (appDef.KillTree)
            {
                KillTree( proc.Id, 0 );
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

            // We no longer block until the app dies
			//
			//log.DebugFormat("WaitForExit pid {0}", proc.Id );
            //proc.WaitForExit();
            //log.DebugFormat("WaitForExit pid {0} DONE", proc.Id );
            //
            //proc = null;

			// instead we start monitoring it until it vanishes
			//dying = true;
        }



        public void Kill()
        {
            //log.DebugFormat("Kill pid {0}", proc.Id );
            // bool IsRunningOnMono = (Type.GetType("Mono.Runtime") != null);
            
            var pe = ParseExe();
            
            if( pe.ExeType != EExeType.Executable )
            {
    			dying = false;
                return;
            }


            // try to adopt an already running process (matching by process image file name, regardless of path)
            if( proc == null && appDef.AdoptIfAlreadyRunning )
            {
                ProcInfo found = FindProcessByExeName( pe.Path );
                if( found != null )
                {
                    log.DebugFormat("Adopted existing process pid={0}, cmd=\"{1}\", dir=\"{2}\"", found.Process.Id, found.CmdLine, found.Process.StartInfo.WorkingDirectory );
                    proc = found.Process;
                }
            }

            if( proc != null )
            {
    			dying = true;

                KillExe();
            }
        }

        void KillExe()
        {
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

			dying = true;

            // if already executing a soft kill sequence?
            if( softKiller.IsRunning )
            {
                // is impatient force killing enabled?
                if( numKillOrdersToForcedHardKill > 0 )
                {
                    remainingKillOrdersToForcedHardKill--;
                    if( remainingKillOrdersToForcedHardKill <= 0 )
                    {
                        log.DebugFormat("Impatient kill");
                        KillExeHard();
                    }
                }
            }
            else if( softKiller.IsDefined ) // soft kill sequence defined, try it first
            {
                softKiller.Start( proc );
            }
            else // no kill sequence, kill hard immediately
            {
                KillExeHard();
            }
        }

		// If the process has exited, grabs the exit code and forgets about the process.
		// Returns true if the process is existing (not exited yet)
		private bool checkExited()
		{
			// have we already forgotten the process?
			if( proc == null ) return true;

			if( !proc.HasExited )
			{
				// still running
				return false;
			}

			// already exited - remember exit code and forget about the process
			dying = false;

			exitCode = proc.ExitCode;

            softKiller.Stop();

			proc = null;

			return true;
		}


        /// <summary>
		/// Returns true if the process is in the system, no matter if running corectly or
		/// whether it is still terminating. I.e. Running==true && Dying==true is a valid state.
		/// </summary>
		public bool Running
        {
            get
            {
                try
                {
					return !checkExited();
                }
                catch // if we adopted a foreign process or otherwise we can't get the exit code
                {
                    return false;
                }
            }
        }

		/// <summary>
		/// If true, the process termination attempt has been made (and the process is hopefully
		/// terminating) but it is still present in the system.
		/// </summary>
		public bool Dying
		{
			get { return dying; }
		}


        public int ExitCode
        {
            get
            {
                try
                {
					// make sure we remember the exit code when the process exits
					checkExited();

					return exitCode;
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

		void SetPriorityClass( string priorityClass )
		{
            if( proc == null ) return;
            if( string.IsNullOrEmpty( priorityClass ) ) return;

            ProcessPriorityClass prioClassNum = ProcessPriorityClass.Normal;
            foreach( var e in Enum.GetValues(typeof(ProcessPriorityClass)))
            {
                if( string.Equals( e.ToString(), priorityClass, StringComparison.OrdinalIgnoreCase ) )
                {
                    prioClassNum = (ProcessPriorityClass)e;
                }
            }

            log.DebugFormat("{0}: Setting PriorityClass = {1}", appDef.AppIdTuple, prioClassNum.ToString());
            proc.PriorityClass = prioClassNum;
		}

    }

}