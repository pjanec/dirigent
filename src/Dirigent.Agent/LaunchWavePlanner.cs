using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Common;

namespace Dirigent.Agent
{
    /// <summary>
    /// Applications are launched in waves; in a wave there are apps depending on apps from
    /// the previously launched wave; the order of apps in a wave is not importanst.
    /// </summary>
    public class AppWave
    {
        public List<AppDef> apps;
        public AppWave(List<AppDef> apps)
        {
            this.apps = apps;
        }
    }


    public class CircularDependencyException : Exception
    {
    }

    public class UnknownDependencyException : Exception
    {
        public UnknownDependencyException(string message)
                : base(message)
        {
        }
    }
    
    /// <summary>
    /// Calculates the launch order of all applications in given plan.
    /// </summary>
    public static class LaunchWavePlanner
    {
        /// <summary>
        /// Builds the list of waves as the result of application interdependencies.
        /// 
        /// The first wawe will contain apps that do not depend on any other app.
        /// The second wave will contain the apps that depend on those from the first wave.
        /// Etc. untill all apps are processed.
        /// </summary>
        public static List<AppWave> build(IEnumerable<AppDef> launchPlan)
        {

            // seznam zbyvajicich aplikaci
            // seznam uz pouzitych aplikaci (ktere uz byly vlozeny do vln)
            // Prochazim seznam zbylych aplikaci a pro kazdou hledam, zda vsechny jeji zavislosti uz se nachazi
            // v seznamu pouzitych aplikaci. Pokud ano, zkopiruju aplikaci do aktualni vlny. Pro projiti
            // vsech aplikaci pak vezmu aplikace z aktulne vytvorene vlny, smazu je ze zbyvajicich a vlozim do pouzitych.
            
            List<AppDef> remaining = (from t in launchPlan where !t.Disabled select t).ToList(); // those not yet moved to any of the waves
            List<AppDef> used = new List<AppDef>(); // those already moved to some of waves
            
            // allow fast lookup of appdef by its name
            Dictionary<AppIdTuple, AppDef> dictApps = new Dictionary<AppIdTuple, AppDef>();
            foreach (var app in remaining)
            {
                dictApps[app.Id] = app;
            }

            var waves = new List<AppWave>(); // the resulting list of waves

            // For each of the remaining apps check whether all its dependencias were already moved to some of prevoiusly
            // built waves; if so, add the app to the current wave and move it from remaining to used.
            while (remaining.Count > 0)
            {

                List<AppDef> currentWave = new List<AppDef>(); // the wave currently being built

                foreach (var app in remaining)
                {
                    bool allDepsSatisfied = true;

                    if (app.Dependencies != null)
                    {
                        foreach (var depName in app.Dependencies)
                        {
                            AppIdTuple depId = AppIdTuple.fromString(depName, app.Id.MachineId);

                            if (!dictApps.ContainsKey(depId))
                            {
                                // throw exception "Unknown dependency"
                                throw new UnknownDependencyException(depName);
                            }

                            var dep = dictApps[depId];
                            if (!used.Contains(dep))
                            {
                                allDepsSatisfied = false;
                                break;
                            }

                        }
                    }
                    if (allDepsSatisfied)
                    {
                        currentWave.Add(app);
                    }
                }

                // if there are no app in current wave, there must be some circular dependency
                // as there is no app that does not depend on 
                if (currentWave.Count == 0)
                {
                    // throw exception "Circular dependency somewhere"
                    throw new CircularDependencyException();
                }
                
                // move apps that were added to the current wave from remaining to used
                foreach (var app in currentWave)
                {
                    remaining.Remove(app);
                    used.Add(app);
                }

                // add current wave to the resulting list of wawes
                waves.Add(new AppWave(currentWave));
            }

            return waves;
        }
    }
}
