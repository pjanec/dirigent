﻿using System;
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

        string rootForRelativePaths;

		double currentTime; // time as set from tick()

        
        public LocalOperations(
            string machineId,
            ILauncherFactory launcherFactory,
            IAppInitializedDetectorFactory appAppInitializedDetectorFactory,
            string rootForRelativePaths )
        {
            this.launcherFactory = launcherFactory;
            this.appAppInitializedDetectorFactory = appAppInitializedDetectorFactory;
            this.rootForRelativePaths = rootForRelativePaths;

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

		// Merges the appdefs from the plan with the current ones (add new, replace existing).
		private void AdoptPlan( ILaunchPlan plan )
		{
			// add record for not yet existing apps
			foreach (var a in plan.getAppDefs())
			{
				if (!appsState.ContainsKey(a.AppIdTuple))
				{
					appsState[a.AppIdTuple] = new AppState()
					{
						Initialized = false,
						Running = false,
						Started = false,
						Dying = false,
                        Disabled = a.Disabled
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

		/// <summary>
		/// Prepares for starting a new plan. 
		/// </summary>
		/// <param name="plan"></param>
		public void SelectPlan(string planName)
		{
			// change the current plan to this one

            if (string.IsNullOrEmpty(planName))
                return;

			if( !planRTInfo.ContainsKey(planName) )
            {
                log.ErrorFormat("Plan {0} not found.", planName);
                return;
            }

            var plan = planRTInfo[planName].Plan;

			AdoptPlan( plan );

			currentPlan = plan;
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
				AdoptPlan( p );
			}

			// update current plan
			// replace also current plan instances so we are using the new one
			// (possible the one created by remoting from a network message),
			// instead of the default one created from local application config
			if (this.currentPlan != null)
			{
				string currentPlanName = currentPlan.Name;
				this.currentPlan = null;
				foreach (var p in planRepo)
				{
					if (p.Name == currentPlanName) // is one of the new plans matching our current plan?
					{
						currentPlan = p;
					}
				}
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

			AdoptPlan( rti.Plan );

			// in the plan is idle
			if ( !rti.State.Running && !rti.State.Killing )
            {
                // trigger the launch sequencer
                rti.State.Running = true;
				rti.State.Killing = false;
				rti.State.TimeStarted = DateTime.UtcNow;

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
			rti.State.Killing = false;
            rti.launchDepChecker = null;

            // if we start the plan again, re-apply the plan to all the apps again...
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

			AdoptPlan( rti.Plan );

            rti.State.Running = false;
			rti.State.Killing = true;

            // attempt to kill all local apps belonging to the current plan
            foreach( var a in rti.Plan.getAppDefs() )
            {
                if( !localApps.ContainsKey( a.AppIdTuple ) )
                    continue;

				// ignore disabled apps
				var appState = appsState[a.AppIdTuple];
				if( appState.Disabled )
					continue;

				// attempt to kill
				// this is non-blocking! does not wait for app to die!
                KillApp( a.AppIdTuple );
                
                // Note:
				// the app status will get reset by processPlan()
				// as soon as the app dies
            }
            
            // stop the launch sequencer
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

            la.launcher = launcherFactory.createLauncher( la.AppDef, rootForRelativePaths );

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

                // this just initiates the dying
				la.launcher.Kill();
                
				// we maintain the launcher instance until the app actually dies
            }
        }


        /// <summary>
        /// Watch if the apps belonging to the plan have already died.
        /// As soon as all apps have died, leave the killing mode and resets local app state
		/// to make them ready for a next plan start.
        /// </summary>
		void processPlanKilling( PlanRuntimeInfo rti )
		{
			if( !rti.State.Killing ) return;

			bool someStillRunning = false;

			// check if some (local or remote) app is still running
			foreach( var a in rti.Plan.getAppDefs() )
			{
				var appState = appsState[a.AppIdTuple];

				if( appState.Running )
				{
					if( !appState.Disabled ) // ignore disabled apps
					{
						someStillRunning = true;
					}
				}

			}

			// if no app left running, 
			if( !someStillRunning )
			{
				// leave the killing mode
				rti.State.Killing = false;

				// reset local app state to
				// enable them to be plan-started again
				foreach( var a in rti.Plan.getAppDefs() )
				{
					if( !localApps.ContainsKey( a.AppIdTuple ) )
						continue;

					var appState = appsState[a.AppIdTuple];

					if( appState.Disabled )	// ignore disabled apps
						continue;

					appState.PlanApplied = false;
					appState.Started = false;
					appState.StartFailed = false;
					appState.Killed = false;
					appState.Initialized = false;
					appState.Running = false;
					appState.Dying = false;
				}


			}

		}

        /// <summary>
        /// Checks dependency conditions and launches apps in wawes
        /// Launches app in sequence, next one earliest after the previous one's separation interval.
        /// </summary>
		void processPlanRunning( PlanRuntimeInfo rti )
		{
            // if plan is stopped, don't start contained apps
			if (!rti.State.Running)
				return;

			// SPECIAL CASE for plans with all-volatile apps
			// Kill the plan as soon as all apps have terminated.
			// So the plan can be started again without being manually Killed first.
			// This is used for utility-plans launching some tools on the stations,
			// where the tools terminate automatically after they are done with their job.
			// Note: this won't kill any app as the Kill is initiated when no app is running any more.
			// (Usual non-volatile plans would not allow next start before prior KillPlan)
			{
			    var currTime = DateTime.UtcNow;

				//bool allLaunched = true;
				//bool allNonVolatileRunning = true;
				bool anyNonVolatileApp = false;	// is there at least one non-volatile?
				bool allAppsProcessed = true;
				bool anyStillRunning = false;
				foreach (var appDef in rti.Plan.getAppDefs())
				{
					var apst = appsState[appDef.AppIdTuple];

		            bool isRemoteApp = appDef.AppIdTuple.MachineId != this.machineId;

					var statusInfoAge = currTime - apst.LastChange;
					bool offline = ( isRemoteApp && statusInfoAge > TimeSpan.FromSeconds(3) );
					{
					}

					//if ( !offline & !(apst.PlanApplied && apst.Started && apst.Initialized))
					//{
					//	allLaunched = false;
					//}

					if (!offline && ! (apst.PlanApplied && (apst.Initialized || apst.StartFailed ) ))
					{
						allAppsProcessed = false;
					}

					if ( !offline && apst.Running)
						anyStillRunning = true;

					if (!appDef.Volatile)
					{
						anyNonVolatileApp = true;

						//if (!apst.Running || offline)
						//{
						//	allNonVolatileRunning = false;
						//}
					}
				}

				if (allAppsProcessed && !anyNonVolatileApp && !anyStillRunning)	// all apps volatile, all launched and none is running  any longer
				{
					// Note: this won't kill any app as no apps are running any more.
					//       It just make the plan startable again.
					KillPlan(rti.Plan.Name);
				}

			}


            // if no plan exists
			// or client re-connected and re-set the plan repo (then we loose the previous RTI)
            if (rti.launchDepChecker == null)
                return;

            // feed the sequencer with apps whose dependencies and constraints have already been satisfied
            if( rti.launchSequencer.IsEmpty() )
            {
                rti.launchSequencer.AddApps( 
                    rti.launchDepChecker.getAppsToLaunch()
                );
            }


            // launch all apps planned for current time
            while(true)
			{
	            // try to get an app to launch and launch it immediately
				AppDef appToLaunch = rti.launchSequencer.GetNext( currentTime );
				if( appToLaunch == null ) break;

				// remember that the app was already processed by the launch plan and should not be touched again
				// note: must be called before StartApp otherwise it would be enlessly re-tried by the launch plan if it throws exception during StartUp
				var la = localApps[appToLaunch.AppIdTuple];
				var appState = appsState[la.AppDef.AppIdTuple];
				appState.PlanApplied = true;
                
				LaunchApp(appToLaunch.AppIdTuple);
			}
		}

		// FIXME: presunout do PlanRuntimeInfo
        void processPlan( ILaunchPlan plan )
        {
            // too frequent message filling up the log - commented out
            //log.Debug("processPlan");

            if( plan==null )
                return;

			var rti = planRTInfo[plan.Name];

			processPlanKilling( rti );

			processPlanRunning( rti );
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
					appState.Dying = la.launcher.Dying;	// if dying=true, then running=true
                    appState.ExitCode = la.launcher.ExitCode;

					// forget about the running process if just exited
					if(	!appState.Running )
					{
						la.launcher = null;
					}

                    tickWachers( la );
                }
                else // not running
                {
                    appState.Running = false;
					appState.Dying = false;
                    //appState.Initialized = false;
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
			this.currentTime = currentTime;

            // refresh to prepare fresh data for app startup condition checks
            refreshLocalAppState();

			// select and run an app from plan if all conditions are met
			foreach (var p in planRepo)
			{
				processPlan(p);
			}
            
            // refresh again to set "WasLaunched" and "Running"
            refreshLocalAppState();
        }

		// update status on the plan
		private void CalculatePlanStatus(PlanRuntimeInfo rti)
		{
			//  Success:
			//      plan running
			//		all apps launched & initialized
			//		all non-volatile apps still running
			// InProgress
			//      plan running
			//      not success and not failure
			// Failure
			//      plan running
			//      some of non-volatile apps launched but not running anymore
			//      some of non-volatile apps failed to run/initialize in given time limit
			// Killing
			//      plan was is in Killing mode and some apps still haven't disappeared
			// None
			//      plan not running (before starting and after killing) 

			if (!rti.State.Running && !rti.State.Killing)
			{
				rti.State.OpStatus = PlanState.EOpStatus.None;
				return;
			}

			if ( rti.State.Killing )
			{
				rti.State.OpStatus = PlanState.EOpStatus.Killing;
				return;
			}

			// here the plan must be Running...


			bool allLaunched = true;
			bool allNonVolatileRunning = true;
			bool anyNonVolatileApp = false;	// is there at least one non-volatile?
			bool allAppsProcessed = true;
			foreach (var appDef in rti.Plan.getAppDefs())
			{
				var apst = appsState[appDef.AppIdTuple];

				if( apst.Disabled )	// ignore disabled apps (as if they are not part of the plan)
					continue;

				if (!(apst.PlanApplied && apst.Started && apst.Initialized))
				{
					allLaunched = false;
				}

				if (! (apst.PlanApplied && (apst.Initialized || apst.StartFailed) ))
				{
					allAppsProcessed = false;
				}


				if (!appDef.Volatile)
				{
					anyNonVolatileApp = true;

					if (!apst.Running)
					{
						allNonVolatileRunning = false;
					}
				}
			}

			var timeSincePlanStart = (DateTime.UtcNow - rti.State.TimeStarted).TotalSeconds;

			PlanState.EOpStatus planStatus = PlanState.EOpStatus.None;
			if (allLaunched && allNonVolatileRunning && anyNonVolatileApp)
			{
				planStatus = PlanState.EOpStatus.Success;
			}
			else
			if (allLaunched && allNonVolatileRunning && !anyNonVolatileApp) // all apps volatile
			{
				planStatus = PlanState.EOpStatus.Success;
			}
			else
			if (rti.Plan.StartTimeout > 0 && timeSincePlanStart >= rti.Plan.StartTimeout)
			{
				// timeout
				planStatus = PlanState.EOpStatus.Failure;
			}
			else
			if( allAppsProcessed	)
			{
				// all apps have been processed but not all of them running
				planStatus = PlanState.EOpStatus.Failure;
			}
			else
			{
				// still some app left that has not been processed by the plan
				planStatus = PlanState.EOpStatus.InProgress;
			}

			rti.State.OpStatus = planStatus;

		}

        public void SetAppEnabled(string planName, AppIdTuple appIdTuple, bool enabled)
        {
            // find the plan
            var plan = planRepo.Find( t => t.Name.Equals(planName, StringComparison.OrdinalIgnoreCase) );
            if( plan == null ) return;

            // find the appdef within the plan
            var appDef = plan.getAppDefs().ToList().Find( t => t.AppIdTuple == appIdTuple );
            if( appDef == null ) return;

            // change the enabled flag
            appDef.Disabled = !enabled;

            // sync to appstate as well
            if( appsState.ContainsKey(appIdTuple) )
            {
                appsState[appIdTuple].Disabled = !enabled;
            }

        }

    }
}
