using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Common;

namespace Dirigent.Agent.Core
{
    /// <summary>
    /// Agent handles local applications lauching and killing according to the launch plan
    /// and monitoring the status of local applications.
    /// </summary>
    public class LocalOperations : IDirigentControl
    {
        string machineId;

        /// <summary>
        /// public state of all apps from current launch plan
        /// </summary>
        Dictionary<AppIdTuple, AppState> appsState;

        /// <summary>
        /// control info of all apps from the launch plan that should be run on this machine
        /// </summary>
        Dictionary<AppIdTuple, LocalApp> localApps;

        /// <summary>
        /// cached reference to the currently loaded plan
        /// </summary>
        ILaunchPlan currentPlan;

        /// <summary>
        /// cached reference to all plans as received from master
        /// </summary>
        List<ILaunchPlan> planRepo;

        ILauncherFactory launcherFactory;
        LaunchDepsChecker launchDepChecker;
        LaunchSequencer launchSequencer;


        
        /// <summary>
        /// Whether we should launch apps during tick. False when plan is paused.
        /// </summary>
        //bool planRunning;
        

        IAppInitializedDetectorFactory appInitializedDetectorFactory;

        
        public LocalOperations(
            string machineId,
            ILauncherFactory launcherFactory,
            IAppInitializedDetectorFactory appInitializedDetectorFactory )
        {
            this.launcherFactory = launcherFactory;
            this.appInitializedDetectorFactory = appInitializedDetectorFactory;

            appsState = new Dictionary<AppIdTuple,AppState>();
            localApps = new Dictionary<AppIdTuple,LocalApp>();
            currentPlan = null;
            planRepo = new List<ILaunchPlan>();
            this.machineId = machineId;

            launchSequencer = new LaunchSequencer();
        }

        public AppState  GetAppState(AppIdTuple appIdTuple)
        {
            try
            {
                return appsState[appIdTuple];
            }
            catch
            {
                throw new UnknownAppIdException(appIdTuple);
            }
        }

        /// <summary>
        /// Sets new status info for given app.
        /// To be used for remote apps whose status gets received from master.
        /// </summary>
        /// <param name="appIdTuple"></param>
        /// <param name="appState"></param>
        public void SetRemoteAppState( AppIdTuple appIdTuple, AppState appState )
        {
            appsState[appIdTuple] = appState;
        }

        /// <summary>
        /// Prepares for starting a new plan. Kills all apps from the current plan if any.
        /// </summary>
        /// <param name="plan"></param>
        public void  LoadPlan(ILaunchPlan plan)
        {
            //if (plan == null)
            //{
            //    throw new ArgumentNullException("plan");
            //}
            
            // first kills all apps from previous plan
            StopPlan();
            
            currentPlan = plan;
            
            appsState.Clear();
            localApps.Clear();
            launchDepChecker = null;

            if (plan == null)
            {
                return;
            }

            currentPlan.Running = false;

            foreach( var a in plan.getAppDefs() )
            {
                appsState[a.AppIdTuple] = new AppState()
                    {
                        Initialized = false,
                        Running = false,
                        Started = false
                    };

                if (a.AppIdTuple.MachineId == machineId)
                {
                    localApps[a.AppIdTuple] = new LocalApp()
                        {
                            AppDef = a,
                            launcher = null,
                            appInitDetector = null
                        };
                }
            }

            List<AppWave> waves = LaunchWavePlanner.build( plan.getAppDefs() );
            launchDepChecker = new LaunchDepsChecker( machineId, appsState, waves );
        }

        public ILaunchPlan  GetCurrentPlan()
        {
 	        return currentPlan;
        }

        public IEnumerable<ILaunchPlan> GetPlanRepo()
        {
            return planRepo;
        }

        public void SetPlanRepo(IEnumerable<ILaunchPlan> planRepo)
        {
            if (planRepo == null)
            {
                this.planRepo = new List<ILaunchPlan>();
            }
            else
            {
                this.planRepo = new List<ILaunchPlan>(planRepo);
            }
        }
        
        /// <summary>
        /// Starts launching local apps according to current plan.
        /// </summary>
        public void  StartPlan()
        {
 	        // trigger the launch sequencer
            if (currentPlan != null)
            {
                currentPlan.Running = true;
            }
        }

        /// <summary>
        /// Kills all local apps from current plan.
        /// </summary>
        public void  StopPlan()
        {
 	        // kill all local apps
            foreach( var a in localApps.Keys )
            {
                StopApp( a );
                
                // enable to be plan-started again
                var la = localApps[a];
                var appState = appsState[la.AppDef.AppIdTuple];
                appState.PlanApplied = false;
                
                appState.Started = false;
                appState.StartFailed = false;
                appState.Killed = false;
                appState.Initialized = false;
                appState.Running = false;

            }
            
            // stop the launch sequencer
            if (currentPlan != null)
            {
                currentPlan.Running = false;
            }
        }

        public void  RestartPlan()
        {
 	        StopPlan();
            StartPlan();
        }

        /// <summary>
        /// Launches a local app if not already running.
        /// </summary>
        /// <param name="appIdTuple"></param>
        public void  StartApp(AppIdTuple appIdTuple)
        {
            if( !(localApps.ContainsKey(appIdTuple) ))
            {
                throw new NotALocalApp(appIdTuple, machineId);
            }

            var la = localApps[appIdTuple];
            var appState = appsState[la.AppDef.AppIdTuple];

            // don't do anything if the app is already running
            if( la.launcher != null && la.launcher.Running )
            {
                return;
            }
            
            // launch the application
            appState.Started = false;
            appState.StartFailed = false;
            appState.Killed = false;
            
            la.launcher = launcherFactory.createLauncher( la.AppDef );

            try
            {
                la.launcher.Launch();
                appState.Started = true;

                // instantiate the app init detector
                la.appInitDetector = appInitializedDetectorFactory.create(la.AppDef, appsState[appIdTuple], la.AppDef.InitializedCondition);
            }
            catch // app launching failed
            {
                appState.StartFailed = true;
                // use just dummy detector
                la.appInitDetector = new NeverInitializedInitDetector();
                throw;
            }
        }

        public void  RestartApp(AppIdTuple appIdTuple)
        {
 	        StopApp( appIdTuple );
            StartApp( appIdTuple );
        }

        /// <summary>
        /// Kills a local app.
        /// </summary>
        /// <param name="appIdTuple"></param>
        public void  StopApp(AppIdTuple appIdTuple)
        {
            if( !(localApps.ContainsKey(appIdTuple) ))
            {
                throw new NotALocalApp(appIdTuple, machineId);
            }

            var la = localApps[appIdTuple];
            var appState = appsState[la.AppDef.AppIdTuple];

            if( la.launcher != null ) // already started?
            {
                if (la.launcher.Running)
                {
                    appState.Killed = true;
                }

                la.launcher.Kill();
                la.launcher = null;

                
            }
        }


        /// <summary>
        /// Checks dependency conditions and launches apps in wawes
        /// Launches max. one app at a time, next one earliest after the previous one's separation interval.
        /// </summary>
        void processPlan( double currentTime )
        {
            // if plan is stopped, don't start contained apps
            if( currentPlan==null || !currentPlan.Running )
                return;

            // if no plan exists
            if (launchDepChecker == null)
                return;

            // feed the sequencer with apps whose dependencies and constraints have already been satisfied
            if( launchSequencer.IsEmpty() )
            {
                launchSequencer.AddApps( 
                    launchDepChecker.getAppsToLaunch()
                );
            }


            // try to get an app to launch and launch it immediately
            
            AppDef appToLaunch = launchSequencer.GetNext( currentTime );
            if( appToLaunch != null )
            {
                // remember that the app was already processed by the launch plan and should not be touched again
                // note: must be called before StartApp otherwise it would be enlessly re-tried by the launch plan if it throws exception during StartUp
                var la = localApps[appToLaunch.AppIdTuple];
                var appState = appsState[la.AppDef.AppIdTuple];
                appState.PlanApplied = true;
                
                StartApp(appToLaunch.AppIdTuple);
            }
        }

        /// <summary>
        /// Updates the status info for all local apps.
        /// </summary>
        void refreshLocalAppState()
        {
            // traverse all local apps, update the stored public status of the app
            foreach( var la in localApps.Values )
            {
                var appState = appsState[la.AppDef.AppIdTuple];

                if( la.launcher != null ) // already launched
                {
                    appState.Running = la.launcher.Running;
                    appState.ExitCode = la.launcher.ExitCode;

                    if( !appState.Initialized
                              &&
                        la.appInitDetector.IsInitialized()
                        )
                    {
                        appState.Initialized = true;
                    }
                }
                else // not yet launched or killed
                {
                    appState.Running = false;
                    appState.Initialized = false;
                }
            }
         }

        /// <summary>
        /// Launches apps accordin to current launch plan (if not stopped) and
        /// updates the status of local apps.
        /// 
        /// To be called periodically (for example twice a second).
        /// Note that at most one app gets launched each call.
        /// </summary>
        public void tick( double currentTime )
        {
            // refresh to prepare fresh data for app startup condition checks
            refreshLocalAppState();
            
            // select and run an app from plan if all conditions are met
            processPlan( currentTime );
            
            // refresh again to set "WasLaunched" and "Running"
            refreshLocalAppState();
        }
    }
}
