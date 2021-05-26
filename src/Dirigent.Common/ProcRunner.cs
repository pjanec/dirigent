using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Dirigent
{
    /// <summary>
    /// Launches and keeps running the Dirigent process
    /// Process is started using the current directory.
    /// If process path not given, searched next to current executable.
    /// The --mode and --parentPid are added to command line, remaining parameters are copied from then current process ones
    /// Hides the main window.
    /// </summary>
    public class ProcRunner : Disposable
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);
        Process? _proc = null;
        private string _procNameNoExt;
        private string _procPath;
        private bool _killOnDispose;

        #if Windows
        [DllImport("user32.dll",EntryPoint = "ShowWindow",SetLastError = true)]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;
        private const int SW_HIDE = 0;
        #endif

        private bool _mainWndShown = false; // last known state
        private Thread? _keepAliveThread = null;
        private bool _quitKeepAliveThread = false;
        private string[] _cmdLineArgs;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="processPath"></param>
        /// <param name="optionReplacementTable"> list of pairs like {"--mode" => "new value"}  </param>
        /// <param name="killOnDispose"></param>
        public ProcRunner( string processPath, Dictionary<string, string>? optionReplacementTable, bool killOnDispose )
        {
            _procNameNoExt = System.IO.Path.GetFileNameWithoutExtension( processPath );
            _procPath = processPath;
            if( !System.IO.Path.IsPathFullyQualified(processPath) )
            {
                // get path next to the current process
                _procPath = System.IO.Path.Combine( Tools.GetExeDir(), System.IO.Path.GetFileName(processPath) );
            }
            _killOnDispose = killOnDispose;

            _cmdLineArgs = Environment.GetCommandLineArgs()[1..];  // exclude the exe file name
            if( optionReplacementTable != null )
            {
                foreach( (var option, var value) in optionReplacementTable )
                {
                    _cmdLineArgs = Tools.AddOrReplaceCmdLineOptionWithValue(
                        _cmdLineArgs,
                        option,
                        value
                    );
                }
            }
            
        }

        /// <summary>
        /// Starts the agent process. Throw exception!
        /// </summary>
        public void Launch()
        {
            var psi = new ProcessStartInfo();
			psi.FileName = _procPath;
            psi.Arguments = string.Join(" ", _cmdLineArgs );
            
            // tell the process who started it
            psi.Arguments += string.Format(" --parentPid {0} ", Process.GetCurrentProcess().Id );

            psi.WorkingDirectory = System.IO.Directory.GetCurrentDirectory(); // Tools.GetExeDir();
            psi.WindowStyle = ProcessWindowStyle.Minimized;
			psi.UseShellExecute = false; // allows us using environment variables

			try
            {
                log.DebugFormat("StartProc exe \"{0}\", cmd \"{1}\", dir \"{2}\", windowstyle {3}", psi.FileName, psi.Arguments, psi.WorkingDirectory, psi.WindowStyle );
                _proc = Process.Start(psi);
                if( _proc != null )
                {
                    log.DebugFormat("StartProc SUCCESS pid {0}", _proc.Id );
                    
                    // let the console show up
                    Thread.Sleep(500); 

                    // hide console if it should not be shown
                    if( !_mainWndShown )
                    {
                        IsShown = false; // hide the window completely
                    }
                }
                else
                {
                    log.DebugFormat("StartProc FAILED (no details)" );
                    _proc = null;
                }
            }
            catch (Exception ex)
            {
                log.DebugFormat("StartProc FAILED except {0}", ex.Message );
                throw new Exception( $"Failed to run process {psi.FileName} from {psi.WorkingDirectory}");
            }
        }

        /// <summary>
        /// Use instead of launch when the process to be launched is already existing but we still
        /// want to use the KeepAlive mechanism.
        /// </summary>
        /// <param name="pid"></param>
        public void Adopt( int pid )
        {
            _proc = Process.GetProcessById( pid );
        }

        public void Kill()
        {
            if( _proc ==null ) return;
            if( _proc.HasExited ) return;

            _proc.Kill();
            log.Debug($"Process {_procNameNoExt} killed (pid {_proc.Id})"  );
        }

        /// <summary>
        /// is Master process currently running?
        /// </summary>
        public bool IsRunning
        {
            get
            {
                if( _proc != null)
                {
                    return !_proc.HasExited;
                }
                return false;
            }
        }


        /// <summary>
        /// Call this periodically to check whether the master process is still running and restart it if it isn't
        /// </summary>
        public void StartKeepAlive()
        {
            if( _keepAliveThread != null ) return;

            _quitKeepAliveThread = false;
            _keepAliveThread = new Thread( () => { KeepAliveThreadFunction(); } );
            _keepAliveThread.Start();
        }

        public void StopKeepAlive()
        {
            if( _keepAliveThread == null ) return;
            _quitKeepAliveThread = true;
            _keepAliveThread.Join(2000);
        }

        private void KeepAliveThreadFunction()
        {
            while(!_quitKeepAliveThread )
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
        /// Show/hide the main window of the process
        /// </summary>
        public bool IsShown
        {
            get
            {
                return _mainWndShown;
            }

            set
            {
                if( value ) // show
                {
                    if( _proc == null ) return;
                    #if Windows
                    ShowWindow( _proc.MainWindowHandle, SW_RESTORE );
                    #endif
                    _mainWndShown = true;
                }
                else
                {
                    if( _proc == null ) return;
                    #if Windows
                    ShowWindow( _proc.MainWindowHandle, SW_HIDE );
                    #endif
                    _mainWndShown = false;
                }

            }
        }

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
            if(!disposing) return;
            StopKeepAlive();
            if( _killOnDispose ) Kill();
        }

        public void KillAllExisting()
        {
            Process[] localByName = Process.GetProcessesByName( _procNameNoExt );
            foreach( var proc in localByName )
            {
                proc.Kill();
            }
        }


    }
}
