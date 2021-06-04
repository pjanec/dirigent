using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Xml.Linq;

namespace Dirigent
{
    public class ExitCodeInitDetector : IAppInitializedDetector
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

        public IAppWatcher.EFlags Flags => IAppWatcher.EFlags.ClearOnLaunch;
		public bool ShallBeRemoved { get; set; }
		public LocalApp App => _app;

        private List<int> _exitCodes = new List<int>(); // list of exit codes meaning that the app was succesfull
        private AppState _appState;
        private LocalApp _app;

        public ExitCodeInitDetector( LocalApp app, XElement xml)
        {
            _app = app;
            _appState = app.AppState;

            try
            {
                string args = xml.Value;

                // " -1, 1, 2,3, 6-8 "
                foreach(var token in args.Split(','))
                {
                    // fill the exitCodes
                    var trimmed = token.Trim();
                    Match m;
                    m = Regex.Match( trimmed, "(-?\\d+)-(-?\\d+)" ); // "1-6" or "-4-8" or "-4--1"
                    if (m.Success)
                    {
                        var lo = int.Parse(m.Groups[1].Value);
                        var hi = int.Parse(m.Groups[2].Value);
                        if( lo <= hi )
                        {
                            for( var i = lo; i <= hi; i++ )
                            {
                                _exitCodes.Add(i);
                            }
                        }
                    }
                    else
                    {
                        m = Regex.Match( trimmed, "(-?\\d+)" );
                        if (m.Success)
                        {
                            var i = int.Parse(m.Groups[0].Value);
                            _exitCodes.Add(i);
                        }
                    }
                }

                // no exit codes specified??
                if (_exitCodes.Count == 0)
                {
                    throw new InvalidAppInitDetectorArguments(Name, args);
                }

                _appState.Initialized = false; // will be set to true as soon as the exit code condition is met

            }
            catch( Exception ex )
            {
                if (ex is FormatException || ex is OverflowException)
                {
                    throw new InvalidAppInitDetectorArguments(Name, xml.ToString());
                }
                throw;
            }
        }

        static public string Name { get { return "exitcode"; } }
        static public IAppInitializedDetector create( LocalApp app, XElement xml)
        {
            return new ExitCodeInitDetector( app, xml);
        }


        bool IsInitialized()
        {
            if( _appState.Started && !_appState.Running && !_appState.Killed )
            {
                log.DebugFormat("ExitCodeInitDetector: App Exited" );

                if( _exitCodes.Contains( _appState.ExitCode ) )
                {
                    log.DebugFormat("ExitCodeInitDetector: ExitCode {0} Found, reporting APP INITIALIZED", _appState.ExitCode );
                    return true;
                }
                else
                {
                    log.DebugFormat("ExitCodeInitDetector: ExitCode {0} NOT Found", _appState.ExitCode );
                }
            }
            return false;
        }

        bool IAppInitializedDetector.IsInitialized
        {
            get
            {
                return IsInitialized();
            }
        }

        void IAppWatcher.Tick()
        {
            if( IsInitialized() )
            {
                _appState.Initialized = true;
                ShallBeRemoved = true;
            }
        }

    }
}
