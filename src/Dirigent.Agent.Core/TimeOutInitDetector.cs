using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dirigent.Common;
using System.Globalization;

namespace Dirigent.Agent.Core
{
    public class TimeOutInitDetector : IAppInitializedDetector
    {
        double TimeOut = 0.0;
        long InitialTicks;

        public TimeOutInitDetector(AppDef appDef, AppState appState, string args)
        {
            try
            {
                TimeOut = Double.Parse( args, CultureInfo.InvariantCulture );
            }
            catch
            {
                throw new InvalidAppInitDetectorArguments(Name, args);
            }

            InitialTicks = DateTime.UtcNow.Ticks;
        }

        public bool IsInitialized()
        {
            var ts = new TimeSpan(DateTime.UtcNow.Ticks - InitialTicks);
            double delta = Math.Abs(ts.TotalSeconds);
            if( delta >= TimeOut )
            {
                return true;
            }
            return false;
        }

        static public string Name { get { return "timeout"; } }
        static public IAppInitializedDetector create(AppDef appDef, AppState appState, string args)
        {
            return new TimeOutInitDetector(appDef, appState, args);
        }
    }
}
