using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using Dirigent.Common;
using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Windows.Forms;
using System.Management;

using X = Dirigent.Common.XmlConfigReaderUtils;

namespace Dirigent.Agent.Core
{

    enum EWindowStyle
    {
        NotSet,
        Normal,
        Minimized,
        Maximized,
        Hidden
    }
    
    class WindowPos
    {
        public Rectangle Rect = Rectangle.Empty;
        public int Screen = 0; // 0=primary, 1=first screen, 2=seconds screen etc.
        public string TitleRegExp; // .net regexp for window name
        public bool Keep = false; // keep the window in the position (if false, apply the position just once)
        public bool Topmost = false; // set the window's on top flag
        public bool BringToFront = false; // bring the window to front and focusing it; usefull with keep=1 to keep window focused and visible
        public bool SendToBack = false; // bring the window to back below all other windows
        //public bool SetFocus = false; // focus the window
        public EWindowStyle WindowStyle = EWindowStyle.NotSet;
    }
    
    /// <summary>
    /// Scans the windows belonging to the process. If it finds one with matching title
    /// it sets the position and size of the window and stops operating.
    /// </summary>
    public class WindowPositioner : IAppWatcher
    {
        AppState appState;
        bool shallBeRemoved = false;
        WindowPos pos;
        int processId;
        Regex titleRegExp;


        #region WINAPI

        [DllImport("user32.dll")]
        static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn,
            IntPtr lParam);

        private const uint WM_GETTEXT = 0x000D;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, StringBuilder lParam);

        [Flags()]
        private enum SetWindowPosFlags : uint
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
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        static readonly IntPtr HWND_TOP = new IntPtr(0);
        static readonly IntPtr HWND_BOTTOM = new IntPtr(1);

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

        private const int SW_HIDE = 0; // Hides the window and activates another window.
        private const int SW_SHOWNORMAL = 1; // Activates and displays a window. If the window is minimized or maximized, the system restores it to its original size and position. An application should specify this flag when displaying the window for the first time.
        private const int SW_SHOWMINIMIZED = 2; // Activates the window and displays it as a minimized window.
        private const int SW_SHOWMAXIMIZED = 3; // Activates the window and displays it as a maximized window.
        private const int SW_SHOWNOACTIVATE = 4; // Displays a window in its most recent size and position. This value is similar to SW_SHOWNORMAL, except that the window is not activated.
        private const int SW_RESTORE = 9; // Activates and displays the window. If the window is minimized or maximized, the system restores it to its original size and position. An application should specify this flag when restoring a minimized window.
        private const int SW_MINIMIZE = 6; // Minimizes the specified window and activates the next top-level window in the Z order.
        private const int SW_MAXIMIZE = 3; // Minimizes the specified window and activates the next top-level window in the Z order.
        private const int SW_SHOW = 5; // Activates the window and displays it in its current size and position.
        private const int SW_FORCEMINIMIZE = 11; // Minimizes a window, even if the thread that owns the window is not responding. This flag should only be used when minimizing windows from a different thread.
        private const int SW_SHOWDEFAULT = 10; // Sets the show state based on the SW_ value specified in the STARTUPINFO structure passed to the CreateProcess function by the program that started the application.
        private const int SW_SHOWMINNOACTIVE = 7; // Displays the window as a minimized window. This value is similar to SW_SHOWMINIMIZED, except the window is not activated.
        private const int SW_SHOWNA = 8; // Displays the window in its current size and position. This value is similar to SW_SHOW, except that the window is not activated.


        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);


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

        #endregion


        public WindowPositioner(AppDef appDef, AppState appState, int processId, XElement xml)
        {
            this.appState = appState;
            //this.pos = pos;
            this.processId = processId;

            parseXml( xml );

            //if( pos.Rect == System.Drawing.Rectangle.Empty ) throw new InvalidAppConfig(appDef.AppIdTuple, "WindowPos: Missing Rectangle attribute");
            if( string.IsNullOrEmpty( pos.TitleRegExp ))  throw new InvalidAppConfig(appDef.AppIdTuple, "WindowPos: Missing TitleRegExp atribute");

            titleRegExp = new Regex( pos.TitleRegExp );
        }

        // <WindowPos titleregexp=".*?\s-\sNotepad" rect="10,50,300,200" screen="1" keep="1" />
        void parseXml( XElement xml )
        {
            pos = new WindowPos();
            
            if( xml != null )
            {
                var xrect = X.Attribute( xml, "Rect", true );
                if( xrect != null )
                {
                    var myRegex = new Regex(@"\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*");
                    var m = myRegex.Match( (string) xrect );
                    if( m != null && m.Success)
                    {
                        pos.Rect = new System.Drawing.Rectangle(
                            int.Parse(m.Groups[1].Value),
                            int.Parse(m.Groups[2].Value),
                            int.Parse(m.Groups[3].Value),
                            int.Parse(m.Groups[4].Value)
                        );
                    }
                }

                pos.Screen = X.getIntAttr(xml, "Screen", 0, ignoreCase:true);
                pos.TitleRegExp = X.getStringAttr(xml, "TitleRegExp", null, ignoreCase:true);
                pos.Keep = X.getBoolAttr(xml, "Keep", false, ignoreCase:true);
                pos.Topmost = X.getBoolAttr(xml, "TopMost", false, ignoreCase:true);
                pos.BringToFront = X.getBoolAttr(xml, "BringToFront", false, ignoreCase:true);
                pos.SendToBack = X.getBoolAttr(xml, "SendToBack", false, ignoreCase:true);
                //pos.SetFocus = X.getBoolAttr(xml, "SetFocus", false, ignoreCase:true);

                string wsstr = X.getStringAttr(xml, "WindowStyle", null, ignoreCase:true);
                if( wsstr != null )
                {
                    if (wsstr.ToLower() == "minimized") pos.WindowStyle = EWindowStyle.Minimized;
                    else
                    if (wsstr.ToLower() == "maximized") pos.WindowStyle = EWindowStyle.Maximized;
                    else
                    if (wsstr.ToLower() == "normal") pos.WindowStyle = EWindowStyle.Normal;
                    else
                    if (wsstr.ToLower() == "hidden") pos.WindowStyle = EWindowStyle.Hidden;
                }
            }
        }


        // from http://stackoverflow.com/questions/2531828/how-to-enumerate-all-windows-belonging-to-a-particular-process-using-net/2584672#2584672

        delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

        static IEnumerable<IntPtr> EnumerateProcessWindowHandles(int processId)
        {
            var handles = new List<IntPtr>();

            foreach (ProcessThread thread in Process.GetProcessById(processId).Threads)
                EnumThreadWindows(thread.Id, 
                    (hWnd, lParam) => { handles.Add(hWnd); return true; }, IntPtr.Zero);

            return handles;
        }

        struct WinInfo
        {
            public System.IntPtr Handle;
            public string Title;
        }

        private List<int> GetChildProcesses(int pid)
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
        
        List<WinInfo> GetProcessWindows( int processId )
        {
            var list = new List<WinInfo>();

            foreach (var handle in EnumerateProcessWindowHandles( processId ) )
            {
                StringBuilder message = new StringBuilder(1000);
                SendMessage(handle, WM_GETTEXT, message.Capacity, message);
                list.Add( new WinInfo() { Handle = handle, Title = message.ToString() } );
            }

            return list;
        }

        void ApplyWindowSettings( System.IntPtr handle )
        {
            if( pos.Rect != Rectangle.Empty )
            {
            
                Screen screen;
                if( pos.Screen == 0 )
                {
                    screen = Screen.PrimaryScreen;
                }
                else
                {
                    var allScreens = Screen.AllScreens;
                    if( pos.Screen > 0 && pos.Screen <= allScreens.Length )
                    {
                        screen = allScreens[pos.Screen-1];
                    } 
                }

                SetWindowPos(
                    handle,
                    HWND.Top, // ignored, see flags below
                    pos.Rect.Left, pos.Rect.Top, pos.Rect.Width, pos.Rect.Height,
                    SetWindowPosFlags.ShowWindow | SetWindowPosFlags.IgnoreZOrder
                 );
            }

            if( pos.SendToBack )
            {
                SetWindowPos(
                    handle,
                    HWND.Bottom,
                    0, 0, 0, 0, 
                    SetWindowPosFlags.IgnoreMove | SetWindowPosFlags.IgnoreResize
                 );
            }

            // NOT SURE WHETHER THIS DOES ANYTHING USEFULL, seems to work fine without this flag
            // maybe just with combination with keep=1
            if( pos.BringToFront )
            {
                if( !pos.Topmost ) // just trying, not verified that it's really needed
                {
                    SetWindowPos(
                        handle,
                        HWND.NoTopMost,
                        0, 0, 0, 0, 
                        SetWindowPosFlags.IgnoreMove | SetWindowPosFlags.IgnoreResize
                     );
                }

                SetForegroundWindow( handle );    
            }

            if( pos.Topmost )
            {
                SetWindowPos(
                    handle,
                    HWND.TopMost,
                    0, 0, 0, 0,
                    SetWindowPosFlags.IgnoreResize | SetWindowPosFlags.IgnoreMove
                 );
            }

            if( pos.WindowStyle != EWindowStyle.NotSet )
            {
                if( pos.WindowStyle == EWindowStyle.Normal )
                {
                    //WINDOWPLACEMENT wp = GetPlacement( handle );
                    //if( wp.showCmd == SW_HIDE )
                    //{
                    //    ShowWindow( handle, SW_SHOW );
                    //    ShowWindow( handle, SW_RESTORE );
                    //}
                    ShowWindow( handle, SW_RESTORE );
                }
                else
                if( pos.WindowStyle == EWindowStyle.Minimized )
                {
                    ShowWindow( handle, SW_MINIMIZE );
                }
                else
                if( pos.WindowStyle == EWindowStyle.Maximized )
                {
                    ShowWindow( handle, SW_MAXIMIZE );
                }
                else
                if( pos.WindowStyle == EWindowStyle.Hidden )
                {
                    ShowWindow( handle, SW_HIDE );
                }
            } 
        }

        bool ApplyWindowSettingsToListedWindows(List<WinInfo> windows)
        {
            bool found = false;
            foreach( var w in windows )
            {
                // apply pos for matching titles
                var m = titleRegExp.Match( w.Title );
                if( m != null && m.Success )
                {
                    ApplyWindowSettings( w.Handle );
                    found = true;
                }
            }
            return found;
        }
        

        void IAppWatcher.Tick()
        {
            // if a window with given title exists, reposition it and stop operating

            // is process still existing?
            Process p;
            try
            {
                p = Process.GetProcessById( processId ); // throws if process noed not exist
                if( p == null || p.HasExited ) throw new Exception("dummy"); // force the catch block to run
            }
            catch
            {
                shallBeRemoved = true;
                return; // do nothing if process has terminated
            }

            var list = GetProcessWindows( processId );
            bool found = ApplyWindowSettingsToListedWindows( list );

            // look in child subprocess windows as well
            var childProcessPids = GetChildProcesses( processId );
            foreach( var childPid in childProcessPids )
            {
                var list2 = GetProcessWindows( childPid );
                found |= ApplyWindowSettingsToListedWindows( list2 );
            }

            // at least one window has been found
            if( found )
            { 
                if( !pos.Keep )
                {
                    shallBeRemoved = true; // positioner has fired, it is no longer needed
                }
            }
        }

        bool IAppWatcher.ShallBeRemoved
        {
            get
            {
                return shallBeRemoved;
            }
        }
    }
}
