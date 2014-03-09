using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Common;

namespace Dirigent.Agent.Core
{
    /// <summary>
    /// Looks for application that are ready for launching because
    /// all their dependencies have been satisfied.
    /// Provides a list of apps that can be launched.
    /// </summary>
    public class LaunchDepsChecker
    {
        string machineId;
        Dictionary<AppIdTuple, AppState> appsState;    
        List<AppWave> launchWaves;

        public LaunchDepsChecker(
            string machineId,
            Dictionary<AppIdTuple, AppState> appsState,
            List<AppWave> launchWaves           )
        {
            this.machineId = machineId;
            this.appsState = appsState;
            //this.localApps = localApps;

            this.launchWaves = launchWaves;
        }

        public IEnumerable<AppDef> getAppsToLaunch()
        {
            List<AppDef> appsToLaunch = new List<AppDef>();

            // find each app that
            //  - is local
            //  - was not yet launched
            //  - all of its dependencies are satisfied (dependent apps already initialized)
            foreach( var wave in launchWaves )
            {
                foreach( var appDef in wave.apps )
                {
                    if( appDef.AppIdTuple.MachineId == machineId ) // is local
                    {
                        var aps = appsState[appDef.AppIdTuple];
                        //var la = localApps[appDef.AppIdTuple];
                        if( aps.WasLaunched == false ) // not yet started
                        {
                            if( areAllDepsSatisfied( appDef ) )
                            {
                                appsToLaunch.Add( appDef );
                            }    
                        }
                    }
                }
            }

            return appsToLaunch;
        }

            //if( !launchSequencerEnabled )
            //    return;

        bool areAllDepsSatisfied( AppDef appDef )
        {
            // pokud jsou vsechny aplikace na kterych ta nase zavisi spousteny
            bool allDepsSatisfied = true;

            if (appDef.Dependencies != null)
            {
                foreach (var depName in appDef.Dependencies)
                {
                    AppIdTuple depId = AppIdTuple.fromString(depName, appDef.AppIdTuple.MachineId);

                    if (!appsState.ContainsKey(depId))
                    {
                        // throw exception "Unknown dependency"
                        throw new UnknownDependencyException(depName);
                    }

                    var dep = appsState[depId];
                    if (!dep.Initialized)
                    {
                        allDepsSatisfied = false;
                        break;
                    }

                }
            }
            return allDepsSatisfied;
        }

    }


}
