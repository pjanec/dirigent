using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using Dirigent.Common;
using System.Globalization;
using System.Diagnostics;

using X = Dirigent.Common.XmlConfigReaderUtils;

namespace Dirigent.Agent.Core
{
    public class TimeOutInitDetector : IAppInitializedDetector
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        double TimeOut = 0.0;
        long InitialTicks;
        AppState appState;
        int processId;
        AppDef appDef;
        bool shallBeRemoved = false;

        //<TimeOut>2.0</TimeOut>
        public TimeOutInitDetector(AppDef appDef, AppState appState, int processId, XElement xml)
        {
            this.appState = appState;
            this.processId = processId;
            this.appDef = appDef;

            
            try
            {
                var timeString = xml.Value;

                TimeOut = Double.Parse( timeString, CultureInfo.InvariantCulture );
            }
            catch
            {
                throw new InvalidAppInitDetectorArguments(Name, xml.ToString());
            }

            appState.Initialized = false; // will be set to true as soon as the exit code condition is met

            InitialTicks = DateTime.UtcNow.Ticks;
            log.DebugFormat("TimeOutInitDetector: Waiting {0} sec, appid {1}, pid {2}", TimeOut, appDef.AppIdTuple, processId );
        }

        bool IsInitialized()
        {
            var ts = new TimeSpan(DateTime.UtcNow.Ticks - InitialTicks);
            double delta = Math.Abs(ts.TotalSeconds);
            if( delta >= TimeOut )
            {
                log.DebugFormat("TimeOutInitDetector: Timeout, reporting INITIALIZED appid {0} pid {1}", appDef.AppIdTuple, processId );
                return true;
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

        static public string Name { get { return "timeout"; } }
        static public IAppInitializedDetector create(AppDef appDef, AppState appState, int processId, XElement xml)
        {
            return new TimeOutInitDetector(appDef, appState, processId, xml );
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
