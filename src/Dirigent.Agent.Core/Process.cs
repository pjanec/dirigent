using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
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

		public static Process_? Start( ProcessStartInfo_ psi, bool asNonElevated=false )
		{
			var p = new Process_();
			#if Windows
				if( asNonElevated )
				{
					return p.StartInternalAsDesktopUser( psi );
				}
				else
				{
 					return p.StartInternal( psi );
				}
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

		// https://stackoverflow.com/questions/11169431/how-to-start-a-new-process-without-administrator-privileges-from-a-process-with?noredirect=1&lq=1
		// 

		public Process_? StartInternalAsDesktopUser( ProcessStartInfo_ psi)
		{
			_hasProcessInfo = false;

			if (string.IsNullOrWhiteSpace(psi.FileName))
				throw new ArgumentException("Value cannot be null or whitespace.", nameof(psi.FileName));

			// To start process as shell user you will need to carry out these steps:
			// 1. Enable the SeIncreaseQuotaPrivilege in your current token
			// 2. Get an HWND representing the desktop shell (GetShellWindow)
			// 3. Get the Process ID(PID) of the process associated with that window(GetWindowThreadProcessId)
			// 4. Open that process(OpenProcess)
			// 5. Get the access token from that process (OpenProcessToken)
			// 6. Make a primary token with that token(DuplicateTokenEx)
			// 7. Start the new process with that primary token(CreateProcessWithTokenW)

			var hProcessToken = IntPtr.Zero;
			// Enable SeIncreaseQuotaPrivilege in this process.  (This won't work if current process is not elevated.)
			try
			{
				var process = WinApi.GetCurrentProcess();
				if (!WinApi.OpenProcessToken(process, 0x0020, ref hProcessToken))
				{
					log.Error("OpenProcessToken failed. Dirigent not elevated?");
					return null;
				}

				var tkp = new WinApi.TOKEN_PRIVILEGES
				{
					PrivilegeCount = 1,
					Privileges = new WinApi.LUID_AND_ATTRIBUTES[1]
				};

				if (!WinApi.LookupPrivilegeValue(null, "SeIncreaseQuotaPrivilege", ref tkp.Privileges[0].Luid))
				{
					log.Error("LookupPrivilegeValue failed. Dirigent not elevated?");
					return null;
				}

				tkp.Privileges[0].Attributes = 0x00000002;

				if (!WinApi.AdjustTokenPrivileges(hProcessToken, false, ref tkp, 0, IntPtr.Zero, IntPtr.Zero))
				{
					log.Error("AdjustTokenPrivileges failed. Dirigent not elevated?");
					return null;
				}
			}
			finally
			{
				WinApi.CloseHandle(hProcessToken);
			}

			// Get an HWND representing the desktop shell.
			// CAVEATS:  This will fail if the shell is not running (crashed or terminated), or the default shell has been
			// replaced with a custom shell.  This also won't return what you probably want if Explorer has been terminated and
			// restarted elevated.
			var hwnd = WinApi.GetShellWindow();
			if (hwnd == IntPtr.Zero)
			{
				log.Error("GetShellWindow failed. Explorer.exe not running? Custom shell in use?");
				return null;
			}

			var hShellProcess = IntPtr.Zero;
			var hShellProcessToken = IntPtr.Zero;
			var hPrimaryToken = IntPtr.Zero;
			try
			{
				// Get the PID of the desktop shell process.
				uint dwPID;
				if (WinApi.GetWindowThreadProcessId(hwnd, out dwPID) == 0)
				{
					log.Error("GetWindowThreadProcessId failed on the desktop shell process.");
					return null;
				}

				// Open the desktop shell process in order to query it (get the token)
				hShellProcess = WinApi.OpenProcess(WinApi.ProcessAccessFlags.QueryInformation, false, dwPID);
				if (hShellProcess == IntPtr.Zero)
				{
					log.Error("OpenProcess failed on the desktop shell process.");
					return null;
				}

				// Get the process token of the desktop shell.
				if (!WinApi.OpenProcessToken(hShellProcess, 0x0002, ref hShellProcessToken))
				{
					log.Error("OpenProcessToken failed on the desktop shell process.");
					return null;
				}

				var dwTokenRights = 395U;

				// Duplicate the shell's process token to get a primary token.
				// Based on experimentation, this is the minimal set of rights required for CreateProcessWithTokenW (contrary to current documentation).
				if (!WinApi.DuplicateTokenEx(hShellProcessToken, dwTokenRights, IntPtr.Zero, WinApi.SECURITY_IMPERSONATION_LEVEL.SecurityImpersonation, WinApi.TOKEN_TYPE.TokenPrimary, out hPrimaryToken))
				{
					log.Error("DuplicateTokenEx failed on the desktop shell process.");
					return null;
				}

				// Start the target process with the new token.
				if( WinApi.StartProcess( psi.FileName, psi.Arguments, psi.EnvironmentVariables, psi.WorkingDirectory, psi.WindowStyle, out _processInfo, out int win32error, hPrimaryToken ) )
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
			}
			finally
			{
				WinApi.CloseHandle(hShellProcessToken);
				WinApi.CloseHandle(hPrimaryToken);
				WinApi.CloseHandle(hShellProcess);
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
					if( handle != IntPtr.Zero )
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
			#if Windows
				if( _hasProcessInfo ) // process started by us
				{
					// as the Process object is not available, we need to find the info ourselves
					if( WinApi.GetExitCodeProcess( _processInfo.hProcess, out var exitCode2 ) )
					{
						return (int)exitCode2;
					}
					throw new System.ComponentModel.Win32Exception( Marshal.GetLastWin32Error() );
				}
				else
				if( _process != null ) // process adopted?
				{
					bool exitCodeFound = false;
					uint exitCode = 0;

					var hProcess = WinApi.OpenProcess( (uint) WinApi.ProcessAccessFlags.QueryLimitedInformation, false, (uint)_process.Id );
					if( hProcess != IntPtr.Zero ) // fails if process not started by us (adopted) and we do not have admin rights
					{
						if( WinApi.GetExitCodeProcess( hProcess, out exitCode ) )
						{
							exitCodeFound = true;
						}
						WinApi.CloseHandle( hProcess );
					}

					if( exitCodeFound )
					{
						return (int) exitCode;
					}
					else
					{
						throw new System.ComponentModel.Win32Exception( Marshal.GetLastWin32Error() );
					}
				}
				else // process never started nor adopted
				{
					return -1;
				}
			#else				
				if( _process != null )
				{
					return _process.ExitCode;
				}
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
				if( handle != IntPtr.Zero )
				{
					var sb = new StringBuilder(1000);
					WinApi.GetProcessImageFileName( _processInfo.hProcess, sb, 1000 );

					name = System.IO.Path.GetFileName( sb.ToString() );
					WinApi.CloseHandle( handle );
				}
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

		public UInt64 WorkingSet64
		{
			get
			{
			#if Windows
				if( !_hasProcessInfo ) // process never started
					return 0;

				// as the Process object is not available, we need to find the info ourselves
				UInt64 memSize = 0;
				var handle = WinApi.OpenProcess( (uint) WinApi.ProcessAccessFlags.QueryLimitedInformation, false, (uint)_processInfo.dwProcessId );
				if( handle != IntPtr.Zero )
				{
					WinApi.PROCESS_MEMORY_COUNTERS memoryCounters;
					memoryCounters.cb = (uint)Marshal.SizeOf(typeof(WinApi.PROCESS_MEMORY_COUNTERS));
					if( WinApi.GetProcessMemoryInfo( handle, out memoryCounters, memoryCounters.cb))
					{
						memSize = memoryCounters.WorkingSetSize;
					}
					WinApi.CloseHandle( handle );
				}
				return memSize;
			#else
				return 0;
			#endif
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