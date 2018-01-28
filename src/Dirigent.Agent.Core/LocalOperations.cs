using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using Dirigent.Common;

namespace Dirigent.Agent.Core
{
    /// <summary>
    /// Agent handles local applications lauching and killing according to the launch plan
    /// and monitoring the status of local applications.
    /// </summary>
    public class LocalOperations : IDirigentControl
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

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
        /// execution state of all plans
        /// </summary>
        Dictionary<string, PlanRuntimeInfo> planRTInfo = new Dictionary<string, PlanRuntimeInfo>();

        /// <summary>
        /// cached reference to the currently (from local UI) operated plan
		/// makes sense just for local UI, nothing global
        /// </summary>
        ILaunchPlan currentPlan;

        /// <summary>
        /// cached reference to all plans as received from master
        /// </summary>
        List<ILaunchPlan> planRepo;

        ILauncherFactory launcherFactory;

        /// <summary>
        /// Whether we should launch apps during tick. False when plan is paused.
        /// </summary>
        //bool planRunning;
        

        IAppInitializedDetectorFactory appAppInitializedDetectorFactory;

        
        public LocalOperations(
            string machineId,
            ILauncherFactory launcherFactory,
            IAppInitializedDetectorFactory appAppInitializedDetectorFactory )
        {
            this.launcherFactory = launcherFactory;
            this.appAppInitializedDetectorFactory = appAppInitializedDetectorFactory;

            appsState = new Dictionary<AppIdTuple,AppState>();
            localApps = new Dictionary<AppIdTuple,LocalApp>();
            currentPlan = null;
            planRepo = new List<ILaunchPlan>();
            this.machineId = machineId;
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

        public Dictionary<AppIdTuple, AppState> GetAllAppsState()
        {
            return new Dictionary<AppIdTuple, AppState>(appsState);
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

		// always locally cached plan state
		public PlanState GetPlanState(string planName)
		{
			if (planRTInfo.ContainsKey(planName))
			{
				var rti = planRTInfo[planName];
				// we dynamiclaly recalculate plan status on every request
				CalculatePlanStatus(rti);
				return rti.State;
			}
			// for unknown plan	return default state
			return new PlanState();
		}

		public void SetPlanState(string planName, PlanState state)
		{
			var rti = planRTInfo[planName];
			rti.State = state;
		}

		/// <summary>
		/// Prepares for starting a new plan. Merges the appdefs from the plan with the current ones (add new, replace existing).
		/// </summary>
		/// <param name="plan"></param>
		public void SelectPlan(string planName)
		{
			//if (plan == null)
			//{
			//    throw new ArgumentNullException("plan");
			//}

			// stop the current plan
			// FIXME: shall we stop? Now we have many plans. No one can be stopped implicitely...
			//StopPlan();

			// change the current plan to this one

            if (string.IsNullOrEmpty(planName))
                return;

			var plan = planRTInfo[planName].Plan;
			currentPlan = plan;

			// add record for not yet existing apps
			foreach (var a in plan.getAppDefs())
			{
				if (!appsState.ContainsKey(a.AppIdTuple))
				{
					appsState[a.AppIdTuple] = new AppState()
					{
						Initialized = false,
						Running = false,
						Started = false
					};
				}

				if (a.AppIdTuple.MachineId == machineId)
				{
					if (!localApps.ContainsKey(a.AppIdTuple))
					{
						localApps[a.AppIdTuple] = new LocalApp()
						{
							AppDef = a,
							launcher = null,
							watchers = new List<IAppWatcher>()
						};
					}
					else // app already exists, just update its appdef to be used on next launch
					{
						localApps[a.AppIdTuple].AppDef = a;
					}
				}
			}
		}

		public ILaunchPlan GetCurrentPlan()
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

			// populate planRTInfo accordingly
			foreach (var p in planRepo)
			{
				planRTInfo[p.Name] = new PlanRuntimeInfo(p);
			}
        }
        
        /// <summary>
        /// Starts launching local apps according to current plan.
        /// </summary>
        public void  StartPlan( string planName )
        {
            if (string.IsNullOrEmpty(planName))
                return;

			var rti = planRTInfo[planName];

			if (!rti.State.Running)
            {
                // trigger the launch sequencer
                rti.State.Running = true;
				//rti.planState.OpMode = PlanState.EOpMode.Started;

                List<AppWave> waves = LaunchWavePlanner.build(rti.Plan.getAppDefs());
                rti.launchDepChecker = new LaunchDepsChecker(machineId, appsState, waves);
            }                    
        }

        /// <summary>
        /// Stops executing a launch plan (stop starting next applications)
        /// </summary>
        public void StopPlan( string planName )
        {
            if (string.IsNullOrEmpty(planName))
                return;

			var rti = planRTInfo[planName];

            rti.State.Running = false;
            rti.launchDepChecker = null;

            foreach (var a in rti.Plan.getAppDefs())
            {
                appsState[a.AppIdTuple].PlanApplied = false;
            }        
        }

        /// <summary>
        /// Kills all local apps from current plan.
        /// </summary>
        public void  KillPlan( string planName )
        {
            if (string.IsNullOrEmpty(planName))
                return;
            
            var rti = planRTInfo[planName];

            // kill all local apps belonging to the current plan
            foreach( var a in rti.Plan.getAppDefs() )
            {
                if( !localApps.ContainsKey( a.AppIdTuple ) )
                    continue;


                KillApp( a.AppIdTuple );
                
                // enable to be plan-started again
                var la = localApps[a.AppIdTuple];
                var appState = appsState[la.AppDef.AppIdTuple];
                appState.PlanApplied = false;
                
                appState.Started = false;
                appState.StartFailed = false;
                appState.Killed = false;
                appState.Initialized = false;
                appState.Running = false;

            }
            
            // stop the launch sequencer
            rti.State.Running = false;
            rti.launchDepChecker = null;
        }

        public void  RestartPlan( string planName )
        {
			KillPlan( planName );
            StartPlan( planName );
        }

        /// <summary>
        /// Launches a local app if not already running.
        /// </summary>
        /// <param name="appIdTuple"></param>
        public void  LaunchApp(AppIdTuple appIdTuple)
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

            log.DebugFormat("Launching app {0}", la.AppDef.AppIdTuple);

            
            // launch the application
            appState.Started = false;
            appState.StartFailed = false;
            appState.Killed = false;
            
            la.watchers.Clear();

            la.launcher = launcherFactory.createLauncher( la.AppDef );

            try
            {
                la.launcher.Launch();
                appState.Started = true;
                appState.Initialized = true; // a watcher can set it to false upon its creation if it works like an AppInitDetector

                //// instantiate app watchers
                //foreach( var watcherDef in la.AppDef.Watchers )
                //{
                //    var w = appWatcherFactory.create( la.AppDef, appsState[appIdTuple], la.launcher.ProcessId, watcherDef);
                //    la.watchers.Add( w );
                //}

                // instantiate init detector (also a watcher)
                {
                    // compatibility with InitialCondition="timeout 2.0" ... convert to XML definition <timeout>2.0</timeout>
                    {
                        if( !string.IsNullOrEmpty( la.AppDef.InitializedCondition ) )
                        {
                            string name;
                            string args;
                            AppInitializedDetectorFactory.ParseDefinitionString( la.AppDef.InitializedCondition, out name, out args );
                            string xmlString = string.Format("<{0}>{1}</{0}>", name, args);

                            var aid = appAppInitializedDetectorFactory.create( la.AppDef, appsState[appIdTuple], la.launcher.ProcessId, XElement.Parse(xmlString) );
                            log.DebugFormat("Adding InitDetector {0}, pid {1}", aid.GetType().Name, la.launcher.ProcessId );
                            la.watchers.Add( aid );
                        }
                    }

                    foreach( var xml in la.AppDef.InitDetectors )
                    {
                        var aid = appAppInitializedDetectorFactory.create( la.AppDef, appsState[appIdTuple], la.launcher.ProcessId, XElement.Parse(xml));
                    
                        log.DebugFormat("Adding InitDetector {0}, pid {1}", aid.GetType().Name, la.launcher.ProcessId );
                        la.watchers.Add( aid );
                    }
                }

                // instantiate window positioners
                foreach( var xml in la.AppDef.WindowPosXml )
                {
                    log.DebugFormat("Adding WindowsPositioner, pid {0}", la.launcher.ProcessId );
                    var wpo = new WindowPositioner( la.AppDef, appsState[appIdTuple], la.launcher.ProcessId, XElement.Parse(xml) );
                    la.watchers.Add( wpo );
                }

                // instantiate autorestarter
                if( la.AppDef.RestartOnCrash )
                {
                    log.DebugFormat("Adding AutoRestarter, pid {0}", la.launcher.ProcessId );
                    var ar = new AutoRestarter( la.AppDef, appsState[appIdTuple], la.launcher.ProcessId, new XElement("Autorestart") );
                    la.watchers.Add( ar );
                }

            }
            catch( Exception ex ) // app launching failed
            {
                log.ErrorFormat("Exception: App \"{0}\"start failure {1}", la.AppDef.AppIdTuple, ex);

                appState.StartFailed = true;
                throw;
            }
        }

        public void  RestartApp(AppIdTuple appIdTuple)
        {
 	        KillApp( appIdTuple );
            LaunchApp( appIdTuple );
        }

        /// <summary>
        /// Kills a local app.
        /// </summary>
        /// <param name="appIdTuple"></param>
        public void  KillApp(AppIdTuple appIdTuple)
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

                log.DebugFormat("Killing app {0}", la.AppDef.AppIdTuple);

                la.launcher.Kill();
                la.launcher = null;

                
            }
        }


        /// <summary>
        /// Checks dependency conditions and launches apps in wawes
        /// Launches max. one app at a time, next one earliest after the previous one's separation interval.
        /// </summary>
		// FIXME: presunout do PlanRuntimeInfo
        void processPlan( double currentTime, ILaunchPlan plan )
        {
            // too frequent message filling up the log - commented out
            //log.Debug("processPlan");

            if( plan==null )
                return;

			var rti = planRTInfo[plan.Name];

            // if plan is stopped, don't start contained apps
			if (!rti.State.Running)
				return;

            // if no plan exists
            if (rti.launchDepChecker == null)
                return;

            // feed the sequencer with apps whose dependencies and constraints have already been satisfied
            if( rti.launchSequencer.IsEmpty() )
            {
                rti.launchSequencer.AddApps( 
                    rti.launchDepChecker.getAppsToLaunch()
                );
            }


            // try to get an app to launch and launch it immediately
            
            AppDef appToLaunch = rti.launchSequencer.GetNext( currentTime );
            if( appToLaunch != null )
            {
                // remember that the app was already processed by the launch plan and should not be touched again
                // note: must be called before StartApp otherwise it would be enlessly re-tried by the launch plan if it throws exception during StartUp
                var la = localApps[appToLaunch.AppIdTuple];
                var appState = appsState[la.AppDef.AppIdTuple];
                appState.PlanApplied = true;
                
                LaunchApp(appToLaunch.AppIdTuple);
            }
        }

        void tickWachers( LocalApp la )
        {
            var toRemove = new List<IAppWatcher>();
            foreach( var w in la.watchers )
            {
                w.Tick();
                if( w.ShallBeRemoved ) toRemove.Add(w);
            }
            
            foreach( var w in toRemove )
            {
                log.DebugFormat("Removing watcher {0}, pid {1}", w.ToString(), la.launcher.ProcessId);

                la.watchers.Remove(w);
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

                    tickWachers( la );
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
			foreach (var p in planRepo)
			{
				processPlan(currentTime, p);
			}
            
            // refresh again to set "WasLaunched" and "Running"
            refreshLocalAppState();
        }

		// update status on the plan
		private void CalculatePlanStatus(PlanRuntimeInfo rti)
		{
			// TODO
			//  Success:
			//		all apps launched & initialized
			//		all non-volatile apps still running
			// InProgress
			//      not success and not failure
			// Failure
			//      some of non-volatile apps launched but not running anymore
			//      some of non-volatile apps 
		}
    }
}
