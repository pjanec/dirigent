#if Windows

using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using System.Text;
using Dirigent.Common;
using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Management;

using X = Dirigent.Common.XmlConfigReaderUtils;

namespace Dirigent.Agent
{
    /// <summary>
    /// Fires when a window belonging to the current process pops up, having a title matching given regular expression
    /// </summary>
    public class WindowPoppedUpInitDetector : IAppInitializedDetector
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

        public IAppWatcher.EFlags Flags => IAppWatcher.EFlags.ClearOnLaunch;
        public bool ShallBeRemoved => _shallBeRemoved;
		public LocalApp App => _app;

        private AppState _appState;
        private int _processId;
        private AppDef _appDef;
        private bool _shallBeRemoved = false;
        
        private string _titleRegExpString = string.Empty;
        private Regex _titleRegExp;
        private LocalApp _app;

        // args example: titleregexp=".*?\s-\sNotepad"
        void parseArgs( XElement xml )
        {
            _titleRegExpString = X.getStringAttr(xml, "TitleRegExp", ignoreCase:true);

            _titleRegExp = new Regex( _titleRegExpString );
        }
        
        public WindowPoppedUpInitDetector( LocalApp app, XElement xml)
        {
            _app = app;
            _appState = _app.AppState;
            _appDef = _app.RecentAppDef;
            _processId = _app.ProcessId;

            try
            {
                parseArgs( xml );
            }
            catch
            {
                throw new InvalidAppInitDetectorArguments(Name, xml.ToString());
            }

            if( _titleRegExp is null )
            {
                throw new InvalidAppInitDetectorArguments(Name, xml.ToString());
            }

            _appState.Initialized = false; // will be set to true as soon as the exit code condition is met

            log.DebugFormat("WindowPoppedUpInitDetector: Waiting for window with titleRegExp {0}, appid {1}, pid {2}", _titleRegExpString, _appDef.Id, _app.ProcessId );
        }

        bool IsInitialized()
        {
            if( !_appState.Running || _app.Process is null  )
            {
				_shallBeRemoved = true;
				return false; // do nothing if process has terminated
            }

            bool found = false;
            var windows = WinApi.GetProcessWindows( _processId );
            foreach( var w in windows )
            {
                // apply pos for matching titles
                var m = _titleRegExp.Match( w.Title );
                if( m != null && m.Success )
                {
                    log.DebugFormat("WindowPoppedUpInitDetector: Found matching window handle 0x{0:X8}, title \"{1}\", pid {2}", w.Handle, w.Title, _processId );
                    
                    found = true;
                    _shallBeRemoved = true;
                }
            }
            return found;
        }

        bool IAppInitializedDetector.IsInitialized => IsInitialized();

        static public string Name { get { return "WindowPoppedUp"; } }
        static public IAppInitializedDetector create( LocalApp app, XElement xml)
        {
            return new WindowPoppedUpInitDetector( app, xml );
        }

        void IAppWatcher.Tick()
        {
            if( IsInitialized() )
            {
                _appState.Initialized = true;
                _shallBeRemoved = true;
            }
        }

        bool IAppWatcher.ShallBeRemoved
        {
            get
            {
                return _shallBeRemoved;
            }
        }

    }
}

#endif