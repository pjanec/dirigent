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

using X = Dirigent.Common.XmlConfigReaderUtils;

namespace Dirigent.Agent
{

    /// <summary>
    /// When the application terminates while in the plan, call the delegate
	/// that makes the Dirigent to start the app again if the plan is running.
    /// </summary>
    public class CrashWatcher : IAppWatcher
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public IAppWatcher.EFlags Flags => IAppWatcher.EFlags.ClearOnLaunch;
        public bool ShallBeRemoved => _shallBeRemoved;
		public LocalApp App => _app;

		public Action? OnCrash;

        private AppState _appState;
        private bool _shallBeRemoved = false;
        private AppDef _appDef;

        private enum eState 
        {
            WaitingForCrash,
            WaitingBeforeRestart,
			Restart,
            Restarting,
			Disabled,
        };

        private  eState _state;

        private LocalApp _app;

        public CrashWatcher( LocalApp app )
        {
            _app = app;
            _appState = _app.AppState;
            _appDef = _app.AppDef;

	        _state =  eState.WaitingForCrash;
        }

        void IAppWatcher.Tick()
        {
            switch( _state )
            {
                case eState.WaitingForCrash:
                {
                    // has the application terminated?
                    if( _appState.Started && !_appState.Running && !_appState.Killed )
                    {
						OnCrash?.Invoke();
						_shallBeRemoved = true;
                        _state = eState.Disabled;
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

    }
}
