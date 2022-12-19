#if Windows

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Management;

namespace Dirigent
{
	static public class WinApi
	{
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

        // from http://stackoverflow.com/questions/2531828/how-to-enumerate-all-windows-belonging-to-a-particular-process-using-net/2584672#2584672

        public delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

        static public IEnumerable<IntPtr> EnumerateProcessWindowHandles(int processId)
        {
            var handles = new List<IntPtr>();

            Process? pr = null;
            try
            {
                pr = Process.GetProcessById(processId);
            }
            catch( ArgumentException )
            {
            }

            if( pr != null )
            {
				//var str = pr.MainWindowTitle;
				var mainWndH = pr.MainWindowHandle;
				if( mainWndH != IntPtr.Zero ) handles.Add( mainWndH );

                try
                {
                    var proc = Process.GetProcessById(processId);
                    foreach (ProcessThread thread in proc.Threads)
                    {
                        EnumThreadWindows(
						    thread.Id, 
                            (hWnd, lParam) =>
						    {
							    if( handles.IndexOf(hWnd) < 0 )	// add just unique handles
							    {
								    handles.Add(hWnd);
							    }
							    return true;
						    },
						    IntPtr.Zero);
                    }
                }
                catch( System.Exception ex )
                {
                    // throws exception for not-accessible processes
                    log.Debug($"Failed to enumerate process threads/windows handles. pid={processId} ex={ex.Message}");
                }
            }
            return handles;
        }

        public struct WinInfo
        {
            public System.IntPtr Handle;
            public string Title;
        }

        static public List<int> GetChildProcesses(int pid)
        {
            List<int> childrenPID = new List<int>();
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection moc = searcher.Get();
            foreach (ManagementObject mo in moc)
            {
                childrenPID.Add( Convert.ToInt32(mo["ProcessID"]) );
            }

            return childrenPID;
        }
        
        static public List<WinInfo> GetProcessWindows( int processId )
        {
            var list = new List<WinInfo>();

            foreach (var handle in EnumerateProcessWindowHandles( processId ) )
            {
                StringBuilder message = new StringBuilder(1000);
                
				UIntPtr result;
				if( SendMessageTimeout(
					handle,
					WM_GETTEXT,
					message.Capacity,
					message,
					SendMessageTimeoutFlags.SMTO_ABORTIFHUNG,
					10,	// timeout
					out result
				).ToInt32() > 0	)
				{
					list.Add( new WinInfo() { Handle = handle, Title = message.ToString() } );
				}
            }

            return list;
        }


        [DllImport("user32.dll")]
        static public extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn,
            IntPtr lParam);

        public const uint WM_GETTEXT = 0x000D;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, StringBuilder lParam);

		[Flags]
		public enum SendMessageTimeoutFlags : uint
		{
			SMTO_NORMAL = 0x0,
			SMTO_BLOCK = 0x1,
			SMTO_ABORTIFHUNG = 0x2,
			SMTO_NOTIMEOUTIFNOTHUNG = 0x8
		}

		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern IntPtr SendMessageTimeout(
		IntPtr hWnd,
		uint Msg,
		int wParam,
		StringBuilder lParam,
		SendMessageTimeoutFlags fuFlags,
		uint uTimeout,
		out UIntPtr lpdwResult);

        [Flags()]
        public enum SetWindowPosFlags : uint
        {
            /// <summary>If the calling thread and the thread that owns the window are attached to different input queues, 
            /// the system posts the request to the thread that owns the window. This prevents the calling thread from 
            /// blocking its execution while other threads process the request.</summary>
            /// <remarks>SWP_ASYNCWINDOWPOS</remarks>
            AsynchronousWindowPosition = 0x4000,
            /// <summary>Prevents generation of the WM_SYNCPAINT message.</summary>
            /// <remarks>SWP_DEFERERASE</remarks>
            DeferErase = 0x2000,
            /// <summary>Draws a frame (defined in the window's class description) around the window.</summary>
            /// <remarks>SWP_DRAWFRAME</remarks>
            DrawFrame = 0x0020,
            /// <summary>Applies new frame styles set using the SetWindowLong function. Sends a WM_NCCALCSIZE message to 
            /// the window, even if the window's size is not being changed. If this flag is not specified, WM_NCCALCSIZE 
            /// is sent only when the window's size is being changed.</summary>
            /// <remarks>SWP_FRAMECHANGED</remarks>
            FrameChanged = 0x0020,
            /// <summary>Hides the window.</summary>
            /// <remarks>SWP_HIDEWINDOW</remarks>
            HideWindow = 0x0080,
            /// <summary>Does not activate the window. If this flag is not set, the window is activated and moved to the 
            /// top of either the topmost or non-topmost group (depending on the setting of the hWndInsertAfter 
            /// parameter).</summary>
            /// <remarks>SWP_NOACTIVATE</remarks>
            DoNotActivate = 0x0010,
            /// <summary>Discards the entire contents of the client area. If this flag is not specified, the valid 
            /// contents of the client area are saved and copied back into the client area after the window is sized or 
            /// repositioned.</summary>
            /// <remarks>SWP_NOCOPYBITS</remarks>
            DoNotCopyBits = 0x0100,
            /// <summary>Retains the current position (ignores X and Y parameters).</summary>
            /// <remarks>SWP_NOMOVE</remarks>
            IgnoreMove = 0x0002,
            /// <summary>Does not change the owner window's position in the Z order.</summary>
            /// <remarks>SWP_NOOWNERZORDER</remarks>
            DoNotChangeOwnerZOrder = 0x0200,
            /// <summary>Does not redraw changes. If this flag is set, no repainting of any kind occurs. This applies to 
            /// the client area, the nonclient area (including the title bar and scroll bars), and any part of the parent 
            /// window uncovered as a result of the window being moved. When this flag is set, the application must 
            /// explicitly invalidate or redraw any parts of the window and parent window that need redrawing.</summary>
            /// <remarks>SWP_NOREDRAW</remarks>
            DoNotRedraw = 0x0008,
            /// <summary>Same as the SWP_NOOWNERZORDER flag.</summary>
            /// <remarks>SWP_NOREPOSITION</remarks>
            DoNotReposition = 0x0200,
            /// <summary>Prevents the window from receiving the WM_WINDOWPOSCHANGING message.</summary>
            /// <remarks>SWP_NOSENDCHANGING</remarks>
            DoNotSendChangingEvent = 0x0400,
            /// <summary>Retains the current size (ignores the cx and cy parameters).</summary>
            /// <remarks>SWP_NOSIZE</remarks>
            IgnoreResize = 0x0001,
            /// <summary>Retains the current Z order (ignores the hWndInsertAfter parameter).</summary>
            /// <remarks>SWP_NOZORDER</remarks>
            IgnoreZOrder = 0x0004,
            /// <summary>Displays the window.</summary>
            /// <remarks>SWP_SHOWWINDOW</remarks>
            ShowWindow = 0x0040,
        }

        [DllImport("user32.dll", SetLastError=true)]
        static public extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);

        [DllImport("user32.dll")]
        static public extern bool SetForegroundWindow(IntPtr hWnd);

        static public readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        static public readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        static public readonly IntPtr HWND_TOP = new IntPtr(0);
        static public readonly IntPtr HWND_BOTTOM = new IntPtr(1);

        /// <summary>
        /// Window handles (HWND) used for hWndInsertAfter
        /// </summary>
        public static class HWND
        {
           public static IntPtr
           NoTopMost = new IntPtr(-2),
           TopMost = new IntPtr(-1),
           Top = new IntPtr(0),
           Bottom = new IntPtr(1);
        }

        /// <summary>
        /// SetWindowPos Flags
        /// </summary>
        public static class SWP
        {
           public static readonly int
           NOSIZE = 0x0001,
           NOMOVE = 0x0002,
           NOZORDER = 0x0004,
           NOREDRAW = 0x0008,
           NOACTIVATE = 0x0010,
           DRAWFRAME = 0x0020,
           FRAMECHANGED = 0x0020,
           SHOWWINDOW = 0x0040,
           HIDEWINDOW = 0x0080,
           NOCOPYBITS = 0x0100,
           NOOWNERZORDER = 0x0200,
           NOREPOSITION = 0x0200,
           NOSENDCHANGING = 0x0400,
           DEFERERASE = 0x2000,
           ASYNCWINDOWPOS = 0x4000;
        }


        //assorted constants needed
        public static uint MF_BYPOSITION = 0x400;
        public static uint MF_REMOVE = 0x1000;
        public static int GWL_STYLE = -16;
        public static int WS_CHILD = 0x40000000; //child window
        public static int WS_BORDER = 0x00800000; //window with border
        public static int WS_DLGFRAME = 0x00400000; //window with double border but no title
        public static int WS_CAPTION = WS_BORDER | WS_DLGFRAME; //window with a title bar 
        public static int WS_SYSMENU = 0x00080000; //window menu 

        //Sets window attributes
        [DllImport("USER32.DLL")]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        //Gets window attributes
        [DllImport("USER32.DLL")]
        public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        public const int SW_HIDE = 0; // Hides the window and activates another window.
        public const int SW_SHOWNORMAL = 1; // Activates and displays a window. If the window is minimized or maximized, the system restores it to its original size and position. An application should specify this flag when displaying the window for the first time.
        public const int SW_SHOWMINIMIZED = 2; // Activates the window and displays it as a minimized window.
        public const int SW_SHOWMAXIMIZED = 3; // Activates the window and displays it as a maximized window.
        public const int SW_MAXIMIZE = 3; // Minimizes the specified window and activates the next top-level window in the Z order.
        public const int SW_SHOWNOACTIVATE = 4; // Displays a window in its most recent size and position. This value is similar to SW_SHOWNORMAL, except that the window is not activated.
        public const int SW_SHOW = 5; // Activates the window and displays it in its current size and position.
        public const int SW_MINIMIZE = 6; // Minimizes the specified window and activates the next top-level window in the Z order.
        public const int SW_SHOWMINNOACTIVE = 7; // Displays the window as a minimized window. This value is similar to SW_SHOWMINIMIZED, except the window is not activated.
        public const int SW_SHOWNA = 8; // Displays the window in its current size and position. This value is similar to SW_SHOW, except that the window is not activated.
        public const int SW_RESTORE = 9; // Activates and displays the window. If the window is minimized or maximized, the system restores it to its original size and position. An application should specify this flag when restoring a minimized window.
        public const int SW_SHOWDEFAULT = 10; // Sets the show state based on the SW_ value specified in the STARTUPINFO structure passed to the CreateProcess function by the program that started the application.
        public const int SW_FORCEMINIMIZE = 11; // Minimizes a window, even if the thread that owns the window is not responding. This flag should only be used when minimizing windows from a different thread.

		[DllImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);



        private static WINDOWPLACEMENT GetPlacement(IntPtr hwnd)
        {
            WINDOWPLACEMENT placement = new WINDOWPLACEMENT();
            placement.length = Marshal.SizeOf(placement);
            GetWindowPlacement(hwnd, ref placement);
            return placement;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowPlacement(
            IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        internal struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public int showCmd;
            public System.Drawing.Point ptMinPosition;
            public System.Drawing.Point ptMaxPosition;
            public System.Drawing.Rectangle rcNormalPosition;
        }


        [DllImport("kernel32.dll", SetLastError=true)]
        public static extern UInt32 WaitForSingleObject(IntPtr hHandle, UInt32 dwMilliseconds);

        public const Int32 INFINITE = -1;
        public const Int32 WAIT_ABANDONED = 0x80;
        public const Int32 WAIT_OBJECT_0 = 0x00;
        public const Int32 WAIT_TIMEOUT = 0x102;
        public const Int32 WAIT_FAILED = -1;


        [Flags]
        public enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VirtualMemoryOperation = 0x00000008,
            VirtualMemoryRead = 0x00000010,
            VirtualMemoryWrite = 0x00000020,
            DuplicateHandle = 0x00000040,
            CreateProcess = 0x000000080,
            SetQuota = 0x00000100,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            QueryLimitedInformation = 0x00001000,
            Synchronize = 0x00100000
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(
             uint processAccess,
             bool bInheritHandle,
             uint processId
        );

        public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        public static IntPtr OpenProcess(Process proc, ProcessAccessFlags flags)
        {
             return OpenProcess((uint)flags, false, (uint)proc.Id);
        }

        [DllImport("psapi.dll")]
        public static extern uint GetProcessImageFileName(
            IntPtr hProcess,
            [Out] StringBuilder lpImageFileName,
            [In] [MarshalAs(UnmanagedType.U4)] int nSize
        );

        [StructLayout(LayoutKind.Sequential, Size=72)]
        public struct PROCESS_MEMORY_COUNTERS
        {
            public uint cb;
            public uint PageFaultCount;
            public UInt64 PeakWorkingSetSize;
            public UInt64 WorkingSetSize;
            public UInt64 QuotaPeakPagedPoolUsage;
            public UInt64 QuotaPagedPoolUsage;
            public UInt64 QuotaPeakNonPagedPoolUsage;
            public UInt64 QuotaNonPagedPoolUsage;
            public UInt64 PagefileUsage;
            public UInt64 PeakPagefileUsage;
        }

        [DllImport("psapi.dll", SetLastError=true)]
        public static extern bool GetProcessMemoryInfo(IntPtr hProcess, out PROCESS_MEMORY_COUNTERS counters, uint size);

        [DllImport("kernel32.dll", SetLastError=true)]
        public static extern uint GetModuleFileName
        (
            [In] IntPtr hModule,
            [Out] StringBuilder lpFilename,
            [In] [MarshalAs(UnmanagedType.U4)]
            int nSize
        );

        public const uint STILL_ACTIVE = 259;
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct STARTUPINFOEX
        {
            public STARTUPINFO StartupInfo;
            public IntPtr lpAttributeList;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct STARTUPINFO
        {
            public Int32 cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwYSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CreateProcess(
            string? lpApplicationName,
            string? lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            [In, MarshalAs(UnmanagedType.LPWStr)] StringBuilder lpEnvironment,  // note the LPWStr => we have to use CREATE_UNICODE_ENVIRONMENT in dwCreationFlags!!
            string? lpCurrentDirectory,
            [In] ref STARTUPINFOEX lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError=true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

        public static bool StartProcess(
            string applicationName,
            string arguments,
            Dictionary<string, string> environment,
            string currentDirectory,
            ProcessWindowStyle windowStyle,
            out PROCESS_INFORMATION processInfo,
            out int win32error 
            )
        {
            var pInfo = new PROCESS_INFORMATION();
            var sInfoEx = new STARTUPINFOEX();
            sInfoEx.StartupInfo.cb = Marshal.SizeOf(sInfoEx);
            sInfoEx.StartupInfo.dwFlags = 0x0001; // (STARTF_USESHOWWINDOW)
            sInfoEx.StartupInfo.wShowWindow = SW_SHOWNORMAL;

            if( windowStyle == ProcessWindowStyle.Normal ) sInfoEx.StartupInfo.wShowWindow = SW_SHOWNORMAL;
            else
            if( windowStyle == ProcessWindowStyle.Minimized ) sInfoEx.StartupInfo.wShowWindow = SW_SHOWMINNOACTIVE;
            else
            if( windowStyle == ProcessWindowStyle.Maximized ) sInfoEx.StartupInfo.wShowWindow = SW_SHOWMAXIMIZED;
            else
            if( windowStyle == ProcessWindowStyle.Hidden ) sInfoEx.StartupInfo.wShowWindow = SW_HIDE;

            // prepare env vars
            var sbEnv = new StringBuilder();
            {
                string[] keys = new string[environment.Count];
                environment.Keys.CopyTo(keys, 0);
                Array.Sort(keys, StringComparer.OrdinalIgnoreCase);


                for( int i=0; i < environment.Count; i++ )
                {
                    var name = keys[i];
                    var value = environment[name] ?? String.Empty;
                    sbEnv.Append( $"{name}={value}\0" );
                }
                sbEnv.Append('\0');
            }

            try
            {
                var appName = applicationName.Trim();
                if( appName.IndexOf(' ') >= 0 )
                {
                    if( !appName.StartsWith('\"') ) appName = "\""+appName;
                    if( !appName.EndsWith('\"') ) appName = appName+"\"";
                }
                var commandLine = appName+" "+arguments;

                if( !CreateProcess(
                    applicationName,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    0x0080410, // (EXTENDED_STARTUPINFO_PERSENT | CREATE_UNICODE_ENVIRONMENT | CREATE_NEW_CONSOLE)
                    sbEnv,
                    currentDirectory,
                    ref sInfoEx,
                    out processInfo
                    ) )
                {
                    win32error = Marshal.GetLastWin32Error();
                    return false;
                }
                win32error = 0;
                return true;
            }
            finally
            {
                // Close process and thread handles
                if (pInfo.hProcess != IntPtr.Zero)
                {
                    CloseHandle(pInfo.hProcess);
                }
                if (pInfo.hThread != IntPtr.Zero)
                {
                    CloseHandle(pInfo.hThread);
                }
            }
        }


        //
        // https://stackoverflow.com/questions/10027341/c-sharp-get-used-memory-in
        //
		public static class PerformanceInfo
		{
			[DllImport( "psapi.dll", SetLastError = true )]
			[return: MarshalAs( UnmanagedType.Bool )]
			public static extern bool GetPerformanceInfo( [Out] out PerformanceInformation PerformanceInformation, [In] int Size );

			[StructLayout( LayoutKind.Sequential )]
			public struct PerformanceInformation
			{
				public int Size;
				public IntPtr CommitTotal;
				public IntPtr CommitLimit;
				public IntPtr CommitPeak;
				public IntPtr PhysicalTotal;
				public IntPtr PhysicalAvailable;
				public IntPtr SystemCache;
				public IntPtr KernelTotal;
				public IntPtr KernelPaged;
				public IntPtr KernelNonPaged;
				public IntPtr PageSize;
				public int HandlesCount;
				public int ProcessCount;
				public int ThreadCount;
			}

			public static Int64 GetPhysicalAvailableMemoryInMiB()
			{
				PerformanceInformation pi = new PerformanceInformation();
				if (GetPerformanceInfo( out pi, Marshal.SizeOf( pi ) ))
				{
					return Convert.ToInt64( (pi.PhysicalAvailable.ToInt64() * pi.PageSize.ToInt64() / 1048576) );
				}
				else
				{
					return -1;
				}

			}

			public static Int64 GetTotalMemoryInMiB()
			{
				PerformanceInformation pi = new PerformanceInformation();
				if (GetPerformanceInfo( out pi, Marshal.SizeOf( pi ) ))
				{
					return Convert.ToInt64( (pi.PhysicalTotal.ToInt64() * pi.PageSize.ToInt64() / 1048576) );
				}
				else
				{
					return -1;
				}

			}
		}

	}
}

#endif
