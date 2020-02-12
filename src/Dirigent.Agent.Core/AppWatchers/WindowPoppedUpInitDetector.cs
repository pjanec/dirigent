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

namespace Dirigent.Agent.Core
{
    /// <summary>
    /// Fires when a window belonging to the current process pops up, having a title matching given regular expression
    /// </summary>
    public class WindowPoppedUpInitDetector : IAppInitializedDetector
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        //double TimeOut = 0.0;
        //long InitialTicks;
        AppState appState;
        int processId;
        AppDef appDef;
        bool shallBeRemoved = false;
        
        string titleRegExpString;
        Regex titleRegExp;

        // args example: titleregexp=".*?\s-\sNotepad"
        void parseArgs( XElement xml )
        {
            if( xml != null )
            {
                titleRegExpString = X.getStringAttr(xml, "TitleRegExp", null, ignoreCase:true);

                titleRegExp = new Regex( titleRegExpString );
            }
        }
        
        public WindowPoppedUpInitDetector(AppDef appDef, AppState appState, int processId, XElement xml)
        {
            this.appState = appState;
            this.processId = processId;
            this.appDef = appDef;

            try
            {
                parseArgs( xml );
            }
            catch
            {
                throw new InvalidAppInitDetectorArguments(Name, xml.ToString());
            }

            appState.Initialized = false; // will be set to true as soon as the exit code condition is met

            log.DebugFormat("WindowPoppedUpInitDetector: Waiting for window with titleRegExp {0}, appid {1}, pid {2}", titleRegExpString, appDef.AppIdTuple, processId );
        }

        bool IsInitialized()
        {
            Process pr = null;
            try
            {
                pr = Process.GetProcessById(processId);
            }
            catch( ArgumentException )
            {
            }

            if( pr != null )
            {
                bool found = false;
                var windows = WinApi.GetProcessWindows( processId );
                foreach( var w in windows )
                {
                    // apply pos for matching titles
                    var m = titleRegExp.Match( w.Title );
                    if( m != null && m.Success )
                    {
                        log.DebugFormat("WindowPoppedUpInitDetector: Found matching window handle 0x{0:X8}, title \"{1}\", pid {2}", w.Handle, w.Title, processId );
                    
                        found = true;
                        shallBeRemoved = true;
                    }
                }
                return found;
            }
            else
            {
                shallBeRemoved = true;
                return false;
            }
        }

        bool IAppInitializedDetector.IsInitialized
        {
            get
            {
                return IsInitialized();
            }
        }

        static public string Name { get { return "WindowPoppedUp"; } }
        static public IAppInitializedDetector create(AppDef appDef, AppState appState, int processId, XElement xml)
        {
            return new WindowPoppedUpInitDetector(appDef, appState, processId, xml);
        }

        void IAppWatcher.Tick()
        {
            if( IsInitialized() )
            {
                appState.Initialized = true;
                shallBeRemoved = true;
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
