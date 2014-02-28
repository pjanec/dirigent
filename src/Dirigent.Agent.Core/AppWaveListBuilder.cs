using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Common;

namespace Dirigent.Agent.Core
{
    public class AppWave
    {
        public List<AppDef> apps;
        public AppWave(List<AppDef> apps)
        {
            this.apps = apps;
        }
    }
    
    
    public static class AppWaveListBuilder
    {
        /// <summary>
        /// Build the list of waves as the result of application interdependencies.
        /// 
        /// The first wawe will contain apps that do not depend on any other app.
        /// The second wave will contain the apps that depend on those from the first wave.
        /// Etc. untill all apps are processed.
        /// </summary>
        static List<AppWave> build(List<AppDef> launchPlan)
        {

            // seznam zbyvajicich aplikaci
            // seznam uz pouzitych aplikaci (ktere uz byly vlozeny do vln)
            // Prochazim seznam zbylych aplikaci a pro kazdou hledam, zda vsechny jeji zavislosti uz se nachazi
            // v seznamu pouzitych aplikaci. Pokud ano, zkopiruju aplikaci do aktualni vlny. Pro projiti
            // vsech aplikaci pak vezmu aplikace z aktulne vytvorene vlny, smazu je ze zbyvajicich a vlozim do pouzitych.
            
            List<AppDef> remaining = new List<AppDef>( launchPlan ); // those not yet moved to any of the waves
            List<AppDef> used = new List<AppDef>(); // those already moved to some of waves
            List<AppDef> currentWave = new List<AppDef>(); // the wave currently being built
            
            // allow fast lookup of appdef by its name
            Dictionary<string, AppDef> dictApps = new Dictionary<string, AppDef>();
            foreach (var app in remaining)
            {
                dictApps[app.AppId] = app;
            }

            var waves = new List<AppWave>(); // the resulting list of waves

            // For each of the remaining apps check whether all its dependencias were already moved to some of prevoiusly
            // built waves; if so, add the app to the current wave and move it from remaining to used.
            while (remaining.Count > 0)
            {

                foreach (var app in remaining)
                {
                    bool allDepsSatisfied = true;

                    foreach (var depName in app.Dependencies)
                    {
                        if (!dictApps.ContainsKey(depName))
                        {
                            // throw exception "Unknown dependency"
                        }

                        var dep = dictApps[depName];
                        if (!used.Contains(dep))
                        {
                            allDepsSatisfied = false;
                            break;
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
