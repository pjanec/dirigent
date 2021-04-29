using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Common;

namespace Dirigent.Agent
{
    /// <summary>
    /// Looks for application that are ready for launching because
    /// all their dependencies have been satisfied.
    /// Provides a list of apps that can be launched.
    /// </summary>
    public class LaunchDepsChecker
    {
        Dictionary<AppIdTuple, AppState> appsState;    
        List<AppWave> launchWaves;

        public LaunchDepsChecker(
            Dictionary<AppIdTuple, AppState> appsState,
            List<AppWave> launchWaves           )
        {
            this.appsState = appsState;

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
                foreach( var ad in wave.apps )
                {
                    var aps = appsState[ad.Id];
                    if ( !ad.PlanApplied ) // not yet processed by the plan
                    {
                        if( areAllDepsSatisfied( ad ) )
                        {
                            appsToLaunch.Add( ad );
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
                    AppIdTuple depId = AppIdTuple.fromString(depName, appDef.Id.MachineId);

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
