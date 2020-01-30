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

    /// <summary>
    /// When the application terminates while in the plan, call the delegate
	/// that makes the Dirigent to start the app again if the plan is running.
    /// </summary>
    public class CrashWatcher : IAppWatcher
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        AppState appState;
        bool shallBeRemoved = false;
        int processId;
        AppDef appDef;

        enum eState 
        {
            WaitingForCrash,
            WaitingBeforeRestart,
			Restart,
            Restarting,
			Disabled,
        };

        eState state;

		public delegate void OnCrashDelegate();
		public OnCrashDelegate OnCrash;

        public CrashWatcher(AppDef appDef, AppState appState, int processId, XElement xml)
        {
            this.appState = appState;

            parseXml( xml );

            this.processId = processId;
            this.appDef = appDef;

	        state =  eState.WaitingForCrash;
        }

        void parseXml( XElement xml )
        {
        }

        void IAppWatcher.Tick()
        {
            switch( state )
            {
                case eState.WaitingForCrash:
                {
                    // has the application terminated?
                    if( appState.Started && !appState.Running && !appState.Killed )
                    {
						if( OnCrash != null ) OnCrash();
						shallBeRemoved = true;
                        state = eState.Disabled;
                    }
                    break;
                }
                case eState.Disabled:
                {
                    // do nothing, wait until watcher get destroyed when the app is started again
                    break;
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
