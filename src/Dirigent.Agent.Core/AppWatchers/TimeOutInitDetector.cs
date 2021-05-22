using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Globalization;
using System.Diagnostics;

using X = Dirigent.XmlConfigReaderUtils;

namespace Dirigent
{
    public class TimeOutInitDetector : IAppInitializedDetector
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

        bool IAppWatcher.ShallBeRemoved => _shallBeRemoved;
        public IAppWatcher.EFlags Flags => IAppWatcher.EFlags.ClearOnLaunch;
		public LocalApp App => _app;


        private double _timeOut = 0.0;
        private long _initialTicks;
        private AppState _appState;
        private AppDef _appDef;
        private bool _shallBeRemoved = false;
        private LocalApp _app;

        //<TimeOut>2.0</TimeOut>
        public TimeOutInitDetector( LocalApp app, XElement xml)
        {
            _app = app;
            this._appState = app.AppState;
            this._appDef = app.RecentAppDef;

            
            try
            {
                var timeString = xml.Value;

                _timeOut = Double.Parse( timeString, CultureInfo.InvariantCulture );
            }
            catch
            {
                throw new InvalidAppInitDetectorArguments(Name, xml.ToString());
            }

            _appState.Initialized = false; // will be set to true as soon as the exit code condition is met

            _initialTicks = DateTime.UtcNow.Ticks;
            log.DebugFormat("TimeOutInitDetector: Waiting {0} sec, appid {1}", _timeOut, _appDef.Id );
        }

        bool IsInitialized()
        {
            var ts = new TimeSpan(DateTime.UtcNow.Ticks - _initialTicks);
            double delta = Math.Abs(ts.TotalSeconds);
            if( delta >= _timeOut )
            {
                log.DebugFormat("TimeOutInitDetector: Timeout, reporting INITIALIZED appid {0}", _appDef.Id );
                return true;
            }
            return false;
        }

        bool IAppInitializedDetector.IsInitialized => IsInitialized();

        static public string Name { get { return "timeout"; } }
        static public IAppInitializedDetector create( LocalApp app, XElement xml)
        {
            return new TimeOutInitDetector( app, xml );
        }

        void IAppWatcher.Tick()
        {
            if( IsInitialized() )
            {
                _appState.Initialized = true;
                _shallBeRemoved = true;
            }
        }

    }
}
