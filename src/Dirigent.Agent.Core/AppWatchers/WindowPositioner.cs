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

using X = Dirigent.Common.XmlConfigReaderUtils;

namespace Dirigent.Agent.Core
{

    class WindowPos
    {
        public Rectangle Rect = Rectangle.Empty;
        public int Screen = 0; // 0=primary, 1=first screen, 2=seconds screen etc.
        public string TitleRegExp; // .net regexp for window name
        public bool Keep; // keep the window in the position (if false, apply the position just once)
        public bool Topmost; // set the window's on top flag
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

        #endregion

        public WindowPositioner(AppDef appDef, AppState appState, int processId, XElement xml)
        {
            this.appState = appState;
            //this.pos = pos;
            this.processId = processId;

            parseXml( xml );

            if( pos.Rect == System.Drawing.Rectangle.Empty ) throw new InvalidAppConfig(appDef.AppIdTuple, "WindowPos: Invalid rectangle");
            if( string.IsNullOrEmpty( pos.TitleRegExp ))  throw new InvalidAppConfig(appDef.AppIdTuple, "WindowPos: Invalid regexp");

            titleRegExp = new Regex( pos.TitleRegExp );
        }

        // <WindowPos titleregexp=".*?\s-\sNotepad" rect="10,50,300,200" screen="1" keep="1" />
        void parseXml( XElement xml )
        {
            pos = new WindowPos();
            
            if( xml != null )
            {
                var xrect = xml.Attribute("rect");
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

                pos.Screen = X.getIntAttr(xml, "screen", 0);
                pos.TitleRegExp = X.getStringAttr(xml, "titleregexp", null);
                pos.Keep = X.getBoolAttr(xml, "keep", false);
                pos.Topmost = X.getBoolAttr(xml, "topmost", false);
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

        void ApplyPos( System.IntPtr handle )
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
                HWND.Top,
                pos.Rect.Left, pos.Rect.Top, pos.Rect.Width, pos.Rect.Height,
                SetWindowPosFlags.ShowWindow
             );

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
            foreach( var w in list )
            {
                // apply pos for matching titles
                var m = titleRegExp.Match( w.Title );
                if( m != null && m.Success )
                {
                    ApplyPos( w.Handle );
                    
                    if( !pos.Keep )
                    {
                        shallBeRemoved = true; // positioner has fired, it is no longer needed
                    }
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
