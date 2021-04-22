#if Windows

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

namespace Dirigent.Agent
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

        public IAppWatcher.EFlags Flags => IAppWatcher.EFlags.ClearOnLaunch;
        public bool ShallBeRemoved => _shallBeRemoved;
		public LocalApp App => _app;

        private LocalApp _app;
        private AppState _appState;
        private AppDef _appDef;
        private bool _shallBeRemoved = false;
        private WindowPos _pos;
        private int _processId;
        private Regex _titleRegExp;


        public WindowPositioner( LocalApp app, XElement xml)
        {
            _app = app;
            _appState = _app.AppState;
			_appDef = _app.AppDef;
            _processId = app.Launcher.ProcessId;

            parseXml( xml );

            //if( pos.Rect == System.Drawing.Rectangle.Empty ) throw new InvalidAppConfig(appDef.AppIdTuple, "WindowPos: Missing Rectangle attribute");
            if( string.IsNullOrEmpty( _pos.TitleRegExp ))  throw new InvalidAppConfig(_appDef.AppIdTuple, "WindowPos: Missing TitleRegExp atribute");

            _titleRegExp = new Regex( _pos.TitleRegExp );
        }

        // <WindowPos titleregexp=".*?\s-\sNotepad" rect="10,50,300,200" screen="1" keep="1" />
        void parseXml( XElement xml )
        {
            _pos = new WindowPos();
            
            if( xml != null )
            {
                var xrect = X.Attribute( xml, "Rect", true );
                if( xrect != null )
                {
                    var myRegex = new Regex(@"\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*");
                    var m = myRegex.Match( (string) xrect );
                    if( m != null && m.Success)
                    {
                        _pos.Rect = new System.Drawing.Rectangle(
                            int.Parse(m.Groups[1].Value),
                            int.Parse(m.Groups[2].Value),
                            int.Parse(m.Groups[3].Value),
                            int.Parse(m.Groups[4].Value)
                        );
                    }
                }

                _pos.Screen = X.getIntAttr(xml, "Screen", 0, ignoreCase:true);
                _pos.TitleRegExp = X.getStringAttr(xml, "TitleRegExp", null, ignoreCase:true);
                _pos.Keep = X.getBoolAttr(xml, "Keep", false, ignoreCase:true);
                _pos.Topmost = X.getBoolAttr(xml, "TopMost", false, ignoreCase:true);
                _pos.BringToFront = X.getBoolAttr(xml, "BringToFront", false, ignoreCase:true);
                _pos.SendToBack = X.getBoolAttr(xml, "SendToBack", false, ignoreCase:true);
                //pos.SetFocus = X.getBoolAttr(xml, "SetFocus", false, ignoreCase:true);

                string wsstr = X.getStringAttr(xml, "WindowStyle", null, ignoreCase:true);
                if( wsstr != null )
                {
                    if (wsstr.ToLower() == "minimized") _pos.WindowStyle = EWindowStyle.Minimized;
                    else
                    if (wsstr.ToLower() == "maximized") _pos.WindowStyle = EWindowStyle.Maximized;
                    else
                    if (wsstr.ToLower() == "normal") _pos.WindowStyle = EWindowStyle.Normal;
                    else
                    if (wsstr.ToLower() == "hidden") _pos.WindowStyle = EWindowStyle.Hidden;
                }
            }
        }



        void ApplyWindowSettings( System.IntPtr handle )
        {
            if( _pos.Rect != Rectangle.Empty )
            {
                log.DebugFormat("WindowPositioner:  SetWindowPos {0}", _pos.Rect.ToString() );

                Screen screen = Screen.PrimaryScreen;
                if( _pos.Screen != 0 )
                {
                    var allScreens = Screen.AllScreens;
                    if( _pos.Screen > 0 && _pos.Screen <= allScreens.Length )
                    {
                        screen = allScreens[_pos.Screen-1];
                    } 
                }

                WinApi.SetWindowPos(
                    handle,
                    WinApi.HWND.Top, // ignored, see flags below
                    screen.WorkingArea.Left + _pos.Rect.Left, screen.WorkingArea.Top + _pos.Rect.Top, _pos.Rect.Width, _pos.Rect.Height,
                    WinApi.SetWindowPosFlags.ShowWindow | WinApi.SetWindowPosFlags.IgnoreZOrder
                 );
            }

            if( _pos.SendToBack )
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
            if( _pos.BringToFront )
            {
                if( !_pos.Topmost ) // just trying, not verified that it's really needed
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

            if( _pos.Topmost )
            {
                log.DebugFormat("WindowPositioner:   TopMost");
                WinApi.SetWindowPos(
                    handle,
                    WinApi.HWND.TopMost,
                    0, 0, 0, 0,
                    WinApi.SetWindowPosFlags.IgnoreResize | WinApi.SetWindowPosFlags.IgnoreMove
                 );
            }

            if( _pos.WindowStyle != EWindowStyle.NotSet )
            {
                if( _pos.WindowStyle == EWindowStyle.Normal )
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
                if( _pos.WindowStyle == EWindowStyle.Minimized )
                {
                    log.DebugFormat("WindowPositioner:   Minimize");
                    WinApi.ShowWindowAsync( handle, WinApi.SW_MINIMIZE );
                }
                else
                if( _pos.WindowStyle == EWindowStyle.Maximized )
                {
                    log.DebugFormat("WindowPositioner:   Maximize");
                    WinApi.ShowWindowAsync( handle, WinApi.SW_MAXIMIZE );
                }
                else
                if( _pos.WindowStyle == EWindowStyle.Hidden )
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
                var m = _titleRegExp.Match( w.Title );
                if( m != null && m.Success )
                {
                    log.DebugFormat("WindowPositioner: Applying settings to handle 0x{0:X8}, title \"{1}\", pid {2}", w.Handle, w.Title, _processId );

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
                p = Process.GetProcessById( _processId ); // throws if process noed not exist
                if( p == null || p.HasExited ) throw new Exception("dummy"); // force the catch block to run
            }
            catch
            {
                _shallBeRemoved = true;
                return; // do nothing if process has terminated
            }

            var list = WinApi.GetProcessWindows( _processId );
            bool found = ApplyWindowSettingsToListedWindows( list );

            // look in child subprocess windows as well
            var childProcessPids = WinApi.GetChildProcesses( _processId );
            foreach( var childPid in childProcessPids )
            {
                var list2 = WinApi.GetProcessWindows( childPid );
                found |= ApplyWindowSettingsToListedWindows( list2 );
            }

            // at least one window has been found
            if( found )
            { 
                if( !_pos.Keep )
                {
                    _shallBeRemoved = true; // positioner has fired, it is no longer needed
                }
            }
        }
    }
}

#endif