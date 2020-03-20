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
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        AppState appState;
        bool shallBeRemoved = false;
        WindowPos pos;
        int processId;
        Regex titleRegExp;


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



        void ApplyWindowSettings( System.IntPtr handle )
        {
            if( pos.Rect != Rectangle.Empty )
            {
                log.DebugFormat("WindowPositioner:  SetWindowPos {0}", pos.Rect.ToString() );

                Screen screen = Screen.PrimaryScreen;
                if( pos.Screen != 0 )
                {
                    var allScreens = Screen.AllScreens;
                    if( pos.Screen > 0 && pos.Screen <= allScreens.Length )
                    {
                        screen = allScreens[pos.Screen-1];
                    } 
                }

                WinApi.SetWindowPos(
                    handle,
                    WinApi.HWND.Top, // ignored, see flags below
                    screen.WorkingArea.Left + pos.Rect.Left, screen.WorkingArea.Top + pos.Rect.Top, pos.Rect.Width, pos.Rect.Height,
                    WinApi.SetWindowPosFlags.ShowWindow | WinApi.SetWindowPosFlags.IgnoreZOrder
                 );
            }

            if( pos.SendToBack )
            {
                log.DebugFormat("WindowPositioner:   SendToBack");
                WinApi.SetWindowPos(
                    handle,
                    WinApi.HWND.Bottom,
                    0, 0, 0, 0, 
                    WinApi.SetWindowPosFlags.IgnoreMove | WinApi.SetWindowPosFlags.IgnoreResize
                 );
            }

            // NOT SURE WHETHER THIS DOES ANYTHING USEFULL, seems to work fine without this flag
            // maybe just with combination with keep=1
            if( pos.BringToFront )
            {
                if( !pos.Topmost ) // just trying, not verified that it's really needed
                {
                    log.DebugFormat("WindowPositioner:   BringToFront");
                    WinApi.SetWindowPos(
                        handle,
                        WinApi.HWND.NoTopMost,
                        0, 0, 0, 0, 
                        WinApi.SetWindowPosFlags.IgnoreMove | WinApi.SetWindowPosFlags.IgnoreResize
                     );
                }
                else
                {
                    log.DebugFormat("WindowPositioner:   BringToFront IGNORED (window is already TopMost)");
                }

                WinApi.SetForegroundWindow( handle );    
            }

            if( pos.Topmost )
            {
                log.DebugFormat("WindowPositioner:   TopMost");
                WinApi.SetWindowPos(
                    handle,
                    WinApi.HWND.TopMost,
                    0, 0, 0, 0,
                    WinApi.SetWindowPosFlags.IgnoreResize | WinApi.SetWindowPosFlags.IgnoreMove
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
                    log.DebugFormat("WindowPositioner:   Restore");
                    WinApi.ShowWindowAsync( handle, WinApi.SW_RESTORE );
                }
                else
                if( pos.WindowStyle == EWindowStyle.Minimized )
                {
                    log.DebugFormat("WindowPositioner:   Minimize");
                    WinApi.ShowWindowAsync( handle, WinApi.SW_MINIMIZE );
                }
                else
                if( pos.WindowStyle == EWindowStyle.Maximized )
                {
                    log.DebugFormat("WindowPositioner:   Maximize");
                    WinApi.ShowWindowAsync( handle, WinApi.SW_MAXIMIZE );
                }
                else
                if( pos.WindowStyle == EWindowStyle.Hidden )
                {
                    log.DebugFormat("WindowPositioner:   Hide");
                    WinApi.ShowWindowAsync( handle, WinApi.SW_HIDE );
                }
            } 
        }

        bool ApplyWindowSettingsToListedWindows(List<WinApi.WinInfo> windows)
        {
            bool found = false;
            foreach( var w in windows )
            {
                // apply pos for matching titles
                var m = titleRegExp.Match( w.Title );
                if( m != null && m.Success )
                {
                    log.DebugFormat("WindowPositioner: Applying settings to handle 0x{0:X8}, title \"{1}\", pid {2}", w.Handle, w.Title, processId );

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

            var list = WinApi.GetProcessWindows( processId );
            bool found = ApplyWindowSettingsToListedWindows( list );

            // look in child subprocess windows as well
            var childProcessPids = WinApi.GetChildProcesses( processId );
            foreach( var childPid in childProcessPids )
            {
                var list2 = WinApi.GetProcessWindows( childPid );
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