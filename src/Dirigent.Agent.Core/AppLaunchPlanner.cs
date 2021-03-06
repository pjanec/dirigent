﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dirigent
{
    /// <summary>
    /// Builds a dependency graph of the applications in the plan.
    /// Sequences the application launch based on the dependencies and the time separation between apps.
    /// </summary>
    /// <remarks>
    /// Sorts app based on their dependencies into launch waves.
    ///   - first those not dependent on anything
    ///   - second = those dependent on the first
    ///   - third = those dependent on the second etc.
    /// Provides the application that are ready for being launched because all their dependencies have been satisfied.
    /// (a satisifed dependency is an app that has been started & initialized).
    /// Takes into account also the defined time separation between launching apps.
    /// </remarks>
    public class AppLaunchPlanner
    {
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

        /// <summary>
        /// Applications are launched in waves; in a wave there are apps depending on apps from
        /// the previously launched wave; the order of apps in a wave is not importanst.
        /// </summary>
        class AppWave
        {
            public List<PlanApp> Apps;

            public AppWave( List<PlanApp> apps )
            {
                this.Apps = apps;
            }
        }


        Dictionary<AppIdTuple, AppState> _appsState;    
        Plan _plan;
        List<AppWave> _launchWaves;
        LaunchSequencer _launchSequencer;

        public AppLaunchPlanner(
            Dictionary<AppIdTuple, AppState> appsState,
            Plan plan
            )
        {
            _appsState = appsState;
            _plan = plan;


			// reset PlanApplied flag
            _plan.ClearPlanApplied();

            _launchWaves = BuildLaunchWaves();

			_launchSequencer = new LaunchSequencer();
        }

        /// <summary>
        /// Get next app that can be started (according to the dependency graph and to the current initialization state of the apps).
        /// The app is expected to be started after calling this...
        /// </summary>
        /// <param name="currentTime">real time in seconds, must be always ascending</param>
        /// <returns>null if no more app ready for the time</returns>
        public PlanApp? GetNextAppToLaunch( double currentTime )
        {
            FetchApps();

            var ad = _launchSequencer.GetNext( currentTime );
    //        if( ad is not null )
    //        {
				//// remember that the app was already processed by the launch plan and should not be touched again
				//// note: must be called before StartApp otherwise it would be enlessly re-tried by the launch plan if it throws exception during StartUp
                // [UPDATE]: this is now set by master whenever actually starting the app from the plan
                //_plan.SetPlanApplied(ad.Id);
    //        }
            return ad;

        }

        void FetchApps()
        {
            // feed the sequencer with apps whose dependencies and constraints have already been satisfied
            if( _launchSequencer.IsEmpty() )
            {
                _launchSequencer.AddApps( 
                    GetAppsToLaunch()
                );
            }

        }

        /// <summary>
        /// Get apps from the plan that have not yet been launched (within this plan) and also have oll their dependencies satisfied.
        /// </summary>
        /// <returns></returns>
        List<PlanApp> GetAppsToLaunch()
        {
            List<PlanApp> appsToLaunch = new();

            // find each app that
            //  - is local
            //  - was not yet launched
            //  - all of its dependencies are satisfied (dependent apps already initialized)
            foreach( var wave in _launchWaves )
            {
                foreach( var app in wave.Apps )
                {
                    if ( !app.State.PlanApplied ) // not yet processed by the plan
                    {
                        if( AreAllDepsSatisfied( app.Def ) )
                        {
                            appsToLaunch.Add( app );
                        }    
                    }
                }
            }

            return appsToLaunch;
        }

        bool AreAllDepsSatisfied( AppDef appDef )
        {
            // pokud jsou vsechny aplikace na kterych ta nase zavisi spousteny
            bool allDepsSatisfied = true;

            if (appDef.Dependencies != null)
            {
                foreach (var depName in appDef.Dependencies)
                {
                    AppIdTuple depId = AppIdTuple.fromString(depName, appDef.Id.MachineId);

                    if (!_appsState.ContainsKey(depId))
                    {
                        // throw exception "Unknown dependency"
                        throw new UnknownDependencyException(depName);
                    }

                    var dep = _appsState[depId];
                    if (!dep.Initialized)
                    {
                        allDepsSatisfied = false;
                        break;
                    }

                }
            }
            return allDepsSatisfied;
        }

        /// <summary>
        /// Builds the list of waves as the result of application interdependencies.
        /// 
        /// The first wawe will contain apps that do not depend on any other app.
        /// The second wave will contain the apps that depend on those from the first wave.
        /// Etc. untill all apps are processed.
        /// </summary>
        List<AppWave> BuildLaunchWaves()
        {

            // seznam zbyvajicich aplikaci
            // seznam uz pouzitych aplikaci (ktere uz byly vlozeny do vln)
            // Prochazim seznam zbylych aplikaci a pro kazdou hledam, zda vsechny jeji zavislosti uz se nachazi
            // v seznamu pouzitych aplikaci. Pokud ano, zkopiruju aplikaci do aktualni vlny. Pro projiti
            // vsech aplikaci pak vezmu aplikace z aktulne vytvorene vlny, smazu je ze zbyvajicich a vlozim do pouzitych.
            
            List<PlanApp> remaining = (from t in _plan.Apps where !t.Def.Disabled select t).ToList(); // those not yet moved to any of the waves
            List<PlanApp> used = new(); // those already moved to some of waves
            
            // allow fast lookup of appdef by its name
            Dictionary<AppIdTuple, PlanApp> dictApps = new();
            foreach (var app in remaining)
            {
                dictApps[app.Def.Id] = app;
            }

            var waves = new List<AppWave>(); // the resulting list of waves

            // For each of the remaining apps check whether all its dependencias were already moved to some of prevoiusly
            // built waves; if so, add the app to the current wave and move it from remaining to used.
            while (remaining.Count > 0)
            {

                List<PlanApp> currentWave = new(); // the wave currently being built

                foreach (var app in remaining)
                {
                    var ad = app.Def;
                    bool allDepsSatisfied = true;

                    if (ad.Dependencies != null)
                    {
                        foreach (var depName in ad.Dependencies)
                        {
                            AppIdTuple depId = AppIdTuple.fromString(depName, ad.Id.MachineId);

                            if( dictApps.TryGetValue( depId, out var dep ) )  // dependency found in the plan
                            {
                                if( !used.Contains(dep) )  // but not yet placed to any previous wave
                                {
                                    allDepsSatisfied = false;  // meaning it can't be satisfied and we shall NOT try to run it in this wave
                                    break;
                                }
                            }
                        }
                    }
                    if (allDepsSatisfied)
                    {
                        currentWave.Add(app);
                    }
                }

                if( currentWave.Count > 0  )
                {
                    // move apps that were added to the current wave from remaining to used
                    foreach (var app in currentWave)
                    {
                        remaining.Remove(app);
                        used.Add(app);
                    }

                    // add current wave to the resulting list of wawes
                    waves.Add(new AppWave( currentWave ) );
                }
                else
                {
                    // circular dependency???
                    log.Warn($" {_plan.Name}: Can't build launch wave, perhaps circular dependency?");
                    break;
                }
            }

            
            for( int i=0; i < waves.Count; i++ )
            {
                var w = waves[i];
                log.Debug($"Launch wave #{i}: {string.Join(", ", from app in w.Apps select app.Def.Id)}");
            }

            return waves;
        }


    }


}
