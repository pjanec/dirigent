using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Common;

namespace Dirigent.Agent.Core
{
    /// <summary>
    /// Checks whether app that should be running is already initialized
    /// </summary>
    class AppInfo
    {
        public AppDef AppDef;
        public int PID;
        public AppState AppState;
    }
    
    public class AppStateMonitor
    {
        List<AppInfo> apps;
        string machineId;

        public AppStateMonitor(List<AppDef> appDefs, string machineId)
        {
            apps = new List<AppInfo>();
            this.machineId = machineId;

            foreach( var appDef in appDefs )
            {
                var info = new AppInfo()
                {
                    AppDef = appDef,
                    AppState = new AppState()
                    {
                        Initialized = false,
                        Running = false,
                        WasLaunched = false
                    },
                    PID = -1
                };
                apps.Add(info);
            }
        }

        /// <summary>
        /// Checks and updates the application state
        ///     if was run then apply the initialization check
        /// </summary>
        void evaluate()
        {
            //foreach( 
        }

        /// <summary>
        /// Returns the public status of an application from an appId
        /// </summary>
        /// <param name="appId"></param>
        /// <returns></returns>
        public AppState getAppState( string appId )
        {
            var a = findById( appId );
            return a.AppState;
        }
        
        AppInfo findById( string appId )
        {
            foreach( var a in apps )
            {
                if( a.AppDef.AppId == appId )
                {
                    return a;
                }
            }
            throw new UnknownAppIdException(appId);
        }

        /// <summary>
        /// Informs app monitor that an app has been launched.
        /// It does not meen it is still running - Runnig will be set to false.
        /// Whether it runs or not needs to be checked by evaluate().
        /// </summary>
        /// <param name="appId">unique app identifier</param>
        /// <param name="PID">process id as returned from application launching operation</param>
        public void setAppLaunched( string appId, int PID )
        {
            AppInfo a = findById( appId );
            a.PID = PID;
            a.AppState.WasLaunched = true;
        }


    }
}
