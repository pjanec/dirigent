using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
#if Windows
using System.Management;
#endif

using System.Xml.Linq;
using X = Dirigent.XmlConfigReaderUtils;
using System.Runtime.InteropServices;

namespace Dirigent
{
	public class Launcher : Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		Net.Client ctrl;
		Process? _proc;
		AppDef _appDef;
		string _relativePathsRoot;
		string? _planName; // in what plan's context the app is going to be started (just informative)
		string _masterIP; // ip address of the Dirigent Master process

		bool _dying = false;	// already killed but still in the system
		int _exitCode = 0; // cached exit code from last run

		Dictionary<string, string> _internalVars;
		Dictionary<string, string> _extraVars;

		SoftKiller _softKiller;


		// Mechanism for hard kill if multiple kills are sent while the process is being killed
		// (likely using a kill sequnce) but still not dead (kill actions not effective and user is impatient,
		// clicking the kill button multiple times
		int numKillOrdersToForcedHardKill = 7; // how many extra kill commands needs to be isuued to hard kill the process if still being soft-killed.
		int remainingKillOrdersToForcedHardKill = -1; // how many kill commands currently left before forced hard kill

		SharedContext _sharedContext;

		public Launcher( AppDef appDef, SharedContext sharedContext, Dictionary<string,string>? extraVars=null )
		{
			this.ctrl = sharedContext.Client;
			if( ctrl == null ) throw new ArgumentNullException( "ctrl", "Valid network-bound Dirigent Control required" );
			this._appDef = appDef;
			_sharedContext = sharedContext;

			_relativePathsRoot = sharedContext.RootForRelativePaths;

			this._planName = appDef.PlanName;
			this._masterIP = _sharedContext.Client.MasterIP;

			_extraVars = extraVars ?? new();
			this._internalVars = BuildVars( appDef, _sharedContext.InternalVars, _extraVars );

			//_cmdRepo = new CommandRepository( ctrl );
			//DirigentCommandRegistrator.Register( _cmdRepo );

			_softKiller = new SoftKiller( appDef );

			// handle the KillSoftly flag for backward compatibility
			if( appDef.KillSoftly )
			{
				if( _softKiller.IsDefined )
				{
					log.ErrorFormat( "{0}: KillSoftly can't be used together with SoftKill! Using just the SoftKill...", appDef.Id );
				}
				else // add a single Close action to the soft kill seq, with default timeout
				{
					_softKiller.AddClose();
				}
			}

		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			_softKiller.Dispose();
		}

		public void Tick()
		{
			checkExited();

			if( _softKiller.IsRunning )
			{
				if( !_softKiller.Tick() ) // have we failed to kill the process using a kill sequence?
				{
					KillExeHard();
				}
			}
		}


		Dictionary<string, string> BuildVars( AppDef appDef, Dictionary<string, string> internalVars, Dictionary<string,string> extraVars )
		{
			// start with externally defined (global) internal vars
			var res = new Dictionary<string, string>( internalVars );

			// add the local variables from appdef
			foreach( var kv in appDef.LocalVarsToSet )
			{
				res[kv.Key] = kv.Value;
			}

			foreach( var kv in extraVars )
			{
				if( !String.IsNullOrEmpty(kv.Value) )
				{
					res[kv.Key] = kv.Value;
				}
				else
				{
					res.Remove(kv.Key);
				}
			}

			return res;
		}

		string ExpandVars( String str )
		{
			return Tools.ExpandEnvAndInternalVars( str, _internalVars );
		}

		string BuildAbsolutePath( string anyPath )
		{
			return PathUtils.BuildAbsolutePath( anyPath, _relativePathsRoot );
		}

		public bool AdoptAlreadyRunning()
		{
			var appPath = ExpandVars( _appDef.ExeFullPath );
			ProcInfo? found = FindProcessByExeName( appPath );
			if( found != null )
			{
				log.DebugFormat( "Adopted existing process pid={0}, cmd=\"{1}\"", found.Pid, found.CmdLine );
				_proc = Process.GetProcessById( found.Pid );

				return true;
			}
			return false;
		}

		string GetCmdExePath()
		{
			return Environment.GetEnvironmentVariable( "ComSpec" ) ?? string.Empty;
		}

		string GetPowershellExePath()
		{
			return Environment.ExpandEnvironmentVariables( @"%windir%\system32\WindowsPowerShell\v1.0\powershell.exe" ) ?? string.Empty;
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
			switch( _appDef.ExeFullPath.ToLower() )
			{
				// just cmd shell
				case "[cmd]":
					pe.ExeType = EExeType.Executable;
					pe.Path = GetCmdExePath();
					pe.CmdLine = ExpandVars( _appDef.CmdLineArgs );
					break;

				case "[cmd.file]":
				case "[cmd.command]":
					pe.ExeType = EExeType.Executable;
					pe.Path = GetCmdExePath();
					pe.CmdLine = "/c " + ExpandVars( _appDef.CmdLineArgs );
					break;

				// just powershell with any command line
				case "[powershell]":
					pe.ExeType = EExeType.Executable;
					pe.Path = GetPowershellExePath();
					pe.CmdLine = ExpandVars( _appDef.CmdLineArgs );
					break;

				// powershell script file specified in appDef.CmdLineArgs;
				case "[powershell.file]":
					pe.ExeType = EExeType.Executable;
					pe.Path = GetPowershellExePath();
					pe.CmdLine = "-executionpolicy bypass -file " + ExpandVars( _appDef.CmdLineArgs );
					break;

				// powershell command specified in CmdLineArgs
				case "[powershell.command]":
					pe.ExeType = EExeType.Executable;
					pe.Path = GetPowershellExePath();
					pe.CmdLine = "-executionpolicy bypass -command \"" + ExpandVars( _appDef.CmdLineArgs ) + "\"";
					break;


				// dirigent.AgentCmd command line
				case "[dirigent.command]":
					pe.ExeType = EExeType.DirigentCmd;
					pe.Path = string.Empty;
					pe.CmdLine = ExpandVars( _appDef.CmdLineArgs );
					break;

				default:
					pe.ExeType = EExeType.Executable;
					pe.Path = ExpandVars( _appDef.ExeFullPath );
					pe.CmdLine = ExpandVars( _appDef.CmdLineArgs );
					break;
			}

			return pe;
		}



		/// <summary>
		/// Starts the process (may also adopt an existing one if set in AppDef)
		/// Throws on failure.
		/// </summary>
		/// <returns>true if the app was launched, false if still dying</returns>
		public bool Launch()
		{
			// don't run again if not yet killed
			if( _dying ) return false;

			// not exited yet
			_exitCode = 0;


			// set environment variables here so we can use them when expanding process path/args/cwd
			Environment.SetEnvironmentVariable( "DIRIGENT_SHAREDCONFDIR", _relativePathsRoot );
			Environment.SetEnvironmentVariable( "DIRIGENT_PLAN", _planName );
			Environment.SetEnvironmentVariable( "DIRIGENT_MACHINEID", _appDef.Id.MachineId );
			Environment.SetEnvironmentVariable( "DIRIGENT_APPID", _appDef.Id.AppId );
			Environment.SetEnvironmentVariable( "DIRIGENT_MASTER_IP", _masterIP );


			var pe = ParseExe();

			switch( pe.ExeType )
			{
				case EExeType.DirigentCmd:
					return LaunchDirigentCmd( pe );
				default:
					return LaunchExe( pe );
			}

		}


		bool LaunchExe( ParsedExe pe )
		{
			// try to adopt an already running process (matching by process image file name, regardless of path)
			if( _appDef.AdoptIfAlreadyRunning )
			{
				if( AdoptAlreadyRunning() )
				{
					return true;
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
			if( _appDef.StartupDir != null )
			{
				var dir = ExpandVars( _appDef.StartupDir );
				psi.WorkingDirectory = BuildAbsolutePath( dir );
			}

			psi.WindowStyle = _appDef.WindowStyle switch
			{
				EWindowStyle.Normal => ProcessWindowStyle.Normal,
				EWindowStyle.Minimized => ProcessWindowStyle.Minimized,
				EWindowStyle.Maximized => ProcessWindowStyle.Maximized,
				EWindowStyle.Hidden => ProcessWindowStyle.Hidden,
				_ => ProcessWindowStyle.Normal
			};



			psi.UseShellExecute = false; // allows us using environment variables

			//
			// modify the environment
			//
			foreach( var x in _appDef.EnvVarsToSet )
			{
				var name = x.Key;
				var value = ExpandVars( x.Value );
				psi.EnvironmentVariables[name] = value;
			}

			if( !String.IsNullOrEmpty( _appDef.EnvVarPathToAppend ) )
			{
				var name = "PATH";
				var postfix = ExpandVars( _appDef.EnvVarPathToAppend );
				// if relative path is specified, consider it relative to SharedConfig and make it absolute (per each ';' separated segment)
				postfix = string.Join( ";", postfix.Split( ';' ).Select( p => BuildAbsolutePath( p ) ) );
				psi.EnvironmentVariables[name] = psi.EnvironmentVariables[name] + ";" + postfix;
			}

			if( !String.IsNullOrEmpty( _appDef.EnvVarPathToPrepend ) )
			{
				var name = "PATH";
				var prefix = ExpandVars( _appDef.EnvVarPathToPrepend );
				// if relative path is specified, consider it relative to SharedConfig and make it absolute (per each ';' separated segment)
				prefix = string.Join( ";", prefix.Split( ';' ).Select( p => BuildAbsolutePath( p ) ) );
				psi.EnvironmentVariables[name] = prefix + ";" + psi.EnvironmentVariables[name];
			}

			foreach( var x in _extraVars )
			{
				var name = x.Key;
				var value = ExpandVars( x.Value );
				if( !String.IsNullOrEmpty( value ) )
				{
					psi.EnvironmentVariables[name] = value;
				}
				else
				{
					psi.EnvironmentVariables.Remove(name);
				}
			}

			// run the process
			_proc = null;
			try
			{
				log.DebugFormat( "StartProc exe \"{0}\", cmd \"{1}\", dir \"{2}\", windowstyle {3}", psi.FileName, psi.Arguments, psi.WorkingDirectory, psi.WindowStyle );
				_proc = Process.Start( psi );
				if( _proc != null )
				{
					log.DebugFormat( "StartProc SUCCESS pid {0}", _proc.Id );

					// Note: WindowStyle in CreateProcess somehow does not work on console windows,
					// probably when the window creation happens later in the process life
					// Please use MainWindowStyles app watcher to set the style for such cases..
				}
				else
				{
					log.DebugFormat( "StartProc FAILED (no details)" );
					throw new AppStartFailureException( _appDef.Id, "no details" );
				}
			}
			catch( Exception ex )
			{
				log.DebugFormat( "StartProc FAILED except {0}", ex.Message );
				throw new AppStartFailureException( _appDef.Id, ex.Message, ex );
			}


			try
			{
				SetPriorityClass( _appDef.PriorityClass );
			}
			catch( Exception ex )
			{
				log.DebugFormat( "SetPriority FAILED except {0}", ex.Message );
			}

			return true;
		}

		bool LaunchDirigentCmd( ParsedExe pe )
		{
			ctrl.Send( new Net.CLIRequestMessage( pe.CmdLine ) );
			return true;
		}

		/// <summary>
		/// Kill a process, and all of its children.
		/// </summary>
		/// <param name="pid">Process ID.</param>
		private static void KillTree( int pid, int indent, bool includingParent = true )
		{
			#if Windows
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

			                Process? childProc = null;
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
			#endif
		}

		private static void KillChildren( int pid )
		{
			KillTree( pid, 0, includingParent: false );
		}


		// kills the process immediately with no mercy (no "softer" stop actions applied)
		void KillExeHard()
		{
			if( _proc == null )
			{
				//log.DebugFormat( "  pid {0} proc=null!", ProcessId );
				return;
			}

			if( _proc.HasExited )
			{
				log.DebugFormat( "  pid {0} already exited.", _proc.Id );
				_proc = null;
				return;
			}

			// kill the process
			if( _appDef.KillTree )
			{
				KillTree( _proc.Id, 0 );
			}
			else
			{
				log.DebugFormat( "Kill pid {0}, name \"{1}\"", _proc.Id, _proc.ProcessName );
				try
				{
					_proc.Kill();
					log.DebugFormat( "Kill pid {0} DONE", _proc.Id );
				}
				catch( Exception ex )
				{
					log.DebugFormat( "Kill pid {0} EXCEPTION {1}", _proc.Id, ex );
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



		public void Kill( Net.KillAppFlags flags=0 )
		{
			//log.DebugFormat("Kill pid {0}", proc.Id );
			// bool IsRunningOnMono = (Type.GetType("Mono.Runtime") != null);

			var pe = ParseExe();

			if( pe.ExeType != EExeType.Executable )
			{
				_dying = false;
				return;
			}


			// try to adopt an already running process (matching by process image file name, regardless of path)
			if( _proc == null && _appDef.AdoptIfAlreadyRunning )
			{
				AdoptAlreadyRunning();
			}

			if( _proc != null )
			{
				_dying = true;

				KillExe();
			}
		}

		void KillExe()
		{
			if( _proc == null )
			{
				return;
			}

			if( _proc.HasExited )
			{
				log.DebugFormat( "  pid {0} already exited.", _proc.Id );
				_proc = null;
				return;
			}

			_dying = true;

			// if already executing a soft kill sequence?
			if( _softKiller.IsRunning )
			{
				// is impatient force killing enabled?
				if( numKillOrdersToForcedHardKill > 0 )
				{
					remainingKillOrdersToForcedHardKill--;
					if( remainingKillOrdersToForcedHardKill <= 0 )
					{
						log.DebugFormat( "Impatient kill" );
						KillExeHard();
					}
				}
			}
			else if( _softKiller.IsDefined ) // soft kill sequence defined, try it first
			{
				_softKiller.Start( _proc );
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
			if( _proc == null ) return true;

			if( !_proc.HasExited )
			{
				// still running
				return false;
			}

			// already exited - remember exit code and forget about the process
			_dying = false;

			try
			{
				_exitCode = _proc.ExitCode;
			}
			catch // exception is thrown for process that was adopted and not started by us
			{
				_exitCode = 0;
			}


			_softKiller.Stop();

			_proc = null;

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
		public bool Dying => _dying;


		public int ExitCode
		{
			get
			{
				try
				{
					// make sure we remember the exit code when the process exits
					checkExited();

					return _exitCode;
				}
				catch // if we adopted a foreign process or otherwise we can't get the exit code
				{
					return -1;
				}
			}
		}

		//public int ProcessId => _proc?.Id ?? -1;
		public Process? Process => _proc;


		public record ProcInfo(
		    int Pid,
		    String Path,
		    String CmdLine
		);

		public static ProcInfo? FindProcessByExeName( string exePath )
		{
			#if Windows
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
			                                Pid = p.Id,
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
			                            return new ProcInfo (
			                                i.Pid,
			                                i.Path,
			                                i.CommandLine
			                            );
			                        }
			                    }
			                }
			            }
			#endif
			return null;
		}

		void SetPriorityClass( string priorityClass )
		{
			if( _proc == null ) return;
			if( string.IsNullOrEmpty( priorityClass ) ) return;

			ProcessPriorityClass prioClassNum = ProcessPriorityClass.Normal;
			foreach( var e in Enum.GetValues( typeof( ProcessPriorityClass ) ) )
			{
				if( string.Equals( e.ToString(), priorityClass, StringComparison.OrdinalIgnoreCase ) )
				{
					prioClassNum = ( ProcessPriorityClass )e;
				}
			}

			log.DebugFormat( "{0}: Setting PriorityClass = {1}", _appDef.Id, prioClassNum.ToString() );
			_proc.PriorityClass = prioClassNum;
		}

	}

}