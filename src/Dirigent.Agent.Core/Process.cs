using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
#if Windows
using System.Management;
#endif


namespace Dirigent
{
	public class ProcessStartInfo_
	{
		public string FileName = String.Empty;
		public string Arguments = String.Empty;
		public string WorkingDirectory = String.Empty;
		public Dictionary<string, string> EnvironmentVariables = new Dictionary<string, string>( StringComparer.OrdinalIgnoreCase );
		public ProcessWindowStyle WindowStyle = ProcessWindowStyle.Normal;

		public ProcessStartInfo_()
		{
			// inherit env vars from this process
			foreach( System.Collections.DictionaryEntry kv in Environment.GetEnvironmentVariables(EnvironmentVariableTarget.Process) )
			{
				var name = (string?)kv.Key;
				var value = (string?) kv.Value;
				if(!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(value))
				{
					EnvironmentVariables[name] = value;
				}
			}
		}			

	}

	/// <summary>
	/// Facade over System.Diagnostics.Process starting the process using WinApi to make the apps
	/// respect the initial window style (especially minimized, hidden...)
	/// Other methods necessary for dirigent (Kill etc.) are delegated to the original Process instance the it obtained
	/// by PID right after starting the process. In rare cases the process might exit immediately, before the Process instance
	/// is obtained - this is why we reimplement the Process class methods also using WinApi as a fallback solution.
	/// </summary>
	public class Process_ : Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		System.Diagnostics.Process? _process;

		#if Windows
		WinApi.PROCESS_INFORMATION _processInfo;
		bool _hasProcessInfo = false;
		#endif

		Process_()
		{
		}

		Process_( Process proc )
		{
			_process = proc;
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if( !disposing ) return;
			#if Windows
			if( _hasProcessInfo )
			{
				WinApi.CloseHandle( _processInfo.hProcess );
			}
			#endif
		}

		public static Process_ Start( ProcessStartInfo_ psi )
		{
			var p = new Process_();
			#if Windows
 				return p.StartInternal( psi );
			#else
				// use native Process only
				var nativePsi = new ProcessStartInfo();
				nativePsi.FileName = psi.FileName;
				nativePsi.Arguments = psi.Arguments;
				nativePsi.WorkingDirectory = psi.WorkingDirectory;
				nativePsi.WindowStyle = psi.WindowStyle;
				return p.StartInternal( nativePsi );
			#endif
		}

		public static Process_ GetProcessById( int pid )
		{
			return new Process_( Process.GetProcessById( pid ) );
		}


		Process_ StartInternal( ProcessStartInfo nativePsi )
		{
			_process = Process.Start( nativePsi );
			return this;
		}

		#if Windows
		Process_ StartInternal( ProcessStartInfo_ psi )
		{
			_hasProcessInfo = false;

			if( WinApi.StartProcess( psi.FileName, psi.Arguments, psi.EnvironmentVariables, psi.WorkingDirectory, psi.WindowStyle, out _processInfo, out int win32error ) )
			{
				_hasProcessInfo = true;
				try
				{
					_process = System.Diagnostics.Process.GetProcessById( _processInfo.dwProcessId );
				}
				catch
				{
					// the process was created but process object can not be obtained
					// process might have terminated immediately (or another error, like access right...)
				}
			}
			else
			{
				throw new System.ComponentModel.Win32Exception( win32error );
			}
			return this;
		}
		#endif

		public void Kill()
		{
			if( _process != null )
			{
				_process.Kill();
				return;
			}

			#if Windows
			if( _hasProcessInfo )
			{
				if( !HasExited )
				{
					WinApi.TerminateProcess( _processInfo.hProcess, uint.MaxValue );
				}
			}
			#else
			#endif
		}
		
		public int Id
		{
			get
			{
				if( _process!=null )
					return _process.Id;

			#if Windows
				if( !_hasProcessInfo ) // process never started
					return -1;

				return (int)_processInfo.dwProcessId;
			#else
			return -1;
			#endif
			}
		}

		public bool HasExited
		{
			get
			{
				if( _process!=null )
					return _process.HasExited;
				
			#if Windows
				if( !_hasProcessInfo ) // process never started
					return true;

				// as the Process object is not available, we need to find the info ourselves
				// https://stackoverflow.com/questions/1591342/c-how-to-determine-if-a-windows-process-is-running
				WinApi.GetExitCodeProcess( _processInfo.hProcess, out var exitCode );
				if( exitCode == WinApi.STILL_ACTIVE )
				{
					// make sure we get access to the handle
					var handle = WinApi.OpenProcess( (uint) WinApi.ProcessAccessFlags.Synchronize, false, (uint)_processInfo.dwProcessId );
					if( handle != WinApi.INVALID_HANDLE_VALUE )
					{
						bool isStillRunning = false;
						if( WinApi.WaitForSingleObject( _processInfo.hProcess, 0 ) == WinApi.WAIT_TIMEOUT ) // still active?
						{
							isStillRunning = true;
						}
						WinApi.CloseHandle( handle );
						return !isStillRunning;
					}
				}
				return true;
			#else
				return true;
			#endif
			}
		}

		public IntPtr MainWindowHandle => _process!=null ? _process.MainWindowHandle : IntPtr.Zero;

		public int ExitCode
		{
			get
			{
				if( _process!=null )
					return _process.ExitCode;

			#if Windows
				if( !_hasProcessInfo ) // process never started
					return -1;

				// as the Process object is not available, we need to find the info ourselves
				WinApi.GetExitCodeProcess( _processInfo.hProcess, out var exitCode );
				return (int)exitCode;
			#else
				return 0;
			#endif
			}
		}

		public string ProcessName
		{
			get
			{
				if( _process!=null )
					return _process.ProcessName;

			#if Windows
				if( !_hasProcessInfo ) // process never started
					return string.Empty;

				// as the Process object is not available, we need to find the info ourselves
				var name = string.Empty;
				var handle = WinApi.OpenProcess( (uint) WinApi.ProcessAccessFlags.QueryLimitedInformation, false, (uint)_processInfo.dwProcessId );
				if( handle != WinApi.INVALID_HANDLE_VALUE )
				{
					var sb = new StringBuilder(1000);
					WinApi.GetProcessImageFileName( _processInfo.hProcess, sb, 1000 );

					name = System.IO.Path.GetFileName( sb.ToString() );
				}
				WinApi.CloseHandle( handle );
				return name;
			#else
				return string.Empty;
			#endif
			}
		}

		public ProcessPriorityClass PriorityClass
		{
			get
			{
				if( _process != null )
					return _process.PriorityClass;

				return ProcessPriorityClass.Normal;
			}

			set
			{
				if( _process != null )
					_process.PriorityClass = value;
			}
		}

		public void CloseMainWindow()
		{
			if( _process == null )
				return;
			_process.CloseMainWindow();
		}


	}
}