using System;
using System.Collections.Generic;
using Dirigent.Common;

namespace Dirigent.Agent
{
	/// <summary>
	/// Description of plan's current state
	/// </summary>
	public class Plan
	{
		public string Name => this.Def.Name;

		public PlanState State = new();

		public List<AppDef> AppDefs => Def.AppDefs;

		public PlanScript? Script;

		public double StartTimeout { get; set; }

		//public System.Collections.Generic.IEnumerable<AppDef> getAppDefs() { return Def.AppDefs; }

		public PlanDef Def;

		Dictionary<AppIdTuple, AppState> _appsState;
		Master _master;

        LaunchDepsChecker? _launchDepChecker;
        LaunchSequencer _launchSequencer; // non-null only when plan is running
		PlanRestarter? _restarter;

		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public Plan( PlanDef def, Master master )
		{
			Def = def;
			_master = master;
			_appsState = _master.AppsState;
			_launchSequencer = new LaunchSequencer();
		}

		public void Tick()
		{
			// tick restarter
			if( _restarter != null )
			{
				_restarter.Tick();
				if( _restarter.ShallBeRemoved )
					_restarter = null;
			}

			ProcessPlanKilling();

			ProcessPlanRunning();

			CalculatePlanStatus();

		}

		/// <summary>
		/// Finds app def by Id. Throws on failure.
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public AppDef FindApp( AppIdTuple id )
		{				
			var appDef = AppDefs.Find( (ad) => ad.Id == id );
			if( appDef is not null )
			{
				return appDef;
			}
			else
			{
				throw new UnknownAppInPlanException( id, Name );
			}
		}

		public void Start()
		{
			AdoptPlan();

			// if the plan is idle
			if ( !State.Running && !State.Killing )
            {
				// FIXME
				//// reset all apps to MAX restart tries
				//SetLocalAppsToMaxRestartTries( rti.Plan.getAppDefs() );

                // trigger the launch sequencer
                State.Running = true;
				State.Killing = false;
				State.TimeStarted = DateTime.UtcNow;

				foreach( var ad in AppDefs )
				{
					ad.PlanApplied = false;
				}        

                List<AppWave> waves = LaunchWavePlanner.build( AppDefs, Name );
                _launchDepChecker = new LaunchDepsChecker( _appsState, waves );
            }                    
		}

        public void Stop()
        {
			log.DebugFormat( "Stop plan {0}", Name );

            State.Running = false;
			State.Killing = false;
            _launchDepChecker = null;

            // if we start the plan again, re-apply the plan to all the apps again...
			foreach( var ad in AppDefs )
            {
                ad.PlanApplied = false;
            }        
        }

        public void Kill()
        {
			log.DebugFormat( "Kill plan {0}", Name );

			AdoptPlan();

            State.Running = false;
			State.Killing = true;

            // kill all apps belonging to the current plan
            foreach( var ad in AppDefs )
            {
				// ignore disabled apps
				var appState = _appsState[ad.Id];
				if( ad.Disabled )
					continue;

				// attempt to kill
				// this is non-blocking! does not wait for app to die!
				// we would like to stop the app indicating "killed" or "start failed"; we simply want neutral "not running".. => resetAppState
                _master.KillApp( ad.Id, Net.KillAppFlags.ResetAppState );
                
                // Note:
				// the app status will get reset by processPlan()
				// as soon as the app dies
            }
            
            // stop the launch sequencer
            _launchDepChecker = null;
        }

        public void Restart()
        {
			log.DebugFormat( "Restart plan {0}", Name );
			_restarter = new PlanRestarter( this );
        }

        /// <summary>
        /// Watch if the apps belonging to the plan have already died.
        /// As soon as all apps have died, leave the killing mode and resets local app state
		/// to make them ready for a next plan start.
        /// </summary>
		void ProcessPlanKilling()
		{
			if( !State.Killing ) return;

			bool someStillRunning = false;

			// check if some (local or remote) app is still running
			foreach( var ad in AppDefs )
			{
				var appState = _appsState[ad.Id];

				if( appState.Running )
				{
					if( !ad.Disabled ) // ignore disabled apps
					{
						someStillRunning = true;
					}
				}

			}

			// if no app left running, 
			if( !someStillRunning )
			{
				// leave the killing mode
				State.Killing = false;

				// reset app state to enable them to be plan-started again
				foreach( var ad in AppDefs )
				{
					var appState = _appsState[ad.Id];

					if( ad.Disabled )	// ignore disabled apps
						continue;

					ad.PlanApplied = false;
				}


			}

		}

        /// <summary>
        /// Checks dependency conditions and launches apps in wawes
        /// Launches app in sequence, next one earliest after the previous one's separation interval.
        /// </summary>
		void ProcessPlanRunning()
		{
            // time base for plan sequencing (real time in seconds)
			double currentTime = (double)DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;


            // if plan is stopped, don't start contained apps
			if (!State.Running)
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
				foreach (var ad in AppDefs )
				{
					var apst = _appsState[ad.Id];

					bool offline = apst.IsOffline;

					//if ( !offline & !(apst.PlanApplied && apst.Started && apst.Initialized))
					//{
					//	allLaunched = false;
					//}

					if (!offline && ! (ad.PlanApplied && (apst.Initialized || apst.StartFailed ) ))
					{
						allAppsProcessed = false;
					}

					if ( !offline && apst.Running)
						anyStillRunning = true;

					if (!ad.Volatile)
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
					Kill();
				}

			}


            // if no plan exists
			// or client re-connected and re-set the plan repo (then we loose the previous RTI)
            if (_launchDepChecker == null)
                return;

            // feed the sequencer with apps whose dependencies and constraints have already been satisfied
            if( _launchSequencer.IsEmpty() )
            {
                _launchSequencer.AddApps( 
                    _launchDepChecker.getAppsToLaunch()
                );
            }


            // launch all apps planned for current time
            while(true)
			{
	            // try to get an app to launch and launch it immediately
				AppDef? appToLaunch = _launchSequencer.GetNext( currentTime );
				if( appToLaunch is null ) break;

				// remember that the app was already processed by the launch plan and should not be touched again
				// note: must be called before StartApp otherwise it would be enlessly re-tried by the launch plan if it throws exception during StartUp
				//var appState = _appsState[appToLaunch.Id];
				appToLaunch.PlanApplied = true;
                
				_master.StartApp( appToLaunch.Id, Name, Net.StartAppFlags.SetPlanApplied );
			}
		}

		// update status on the plan
		private void CalculatePlanStatus()
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

			if ( !State.Running && !State.Killing)
			{
				State.OpStatus = PlanState.EOpStatus.None;
				return;
			}

			if ( State.Killing )
			{
				State.OpStatus = PlanState.EOpStatus.Killing;
				return;
			}

			// here the plan must be Running...


			bool allLaunched = true;
			bool allNonVolatileRunning = true;
			bool anyNonVolatileApp = false;	// is there at least one non-volatile?
			bool allAppsProcessed = true;
			foreach (var ad in AppDefs )
			{
				var apst = _appsState[ad.Id];

				if( ad.Disabled )	// ignore disabled apps (as if they are not part of the plan)
					continue;

				if (!(ad.PlanApplied && apst.Started && apst.Initialized))
				{
					allLaunched = false;
				}

				if (! (ad.PlanApplied && (apst.Initialized || apst.StartFailed) ))
				{
					allAppsProcessed = false;
				}


				if (!ad.Volatile)
				{
					anyNonVolatileApp = true;

					if (!apst.Running)
					{
						allNonVolatileRunning = false;
					}
				}
			}

			var timeSincePlanStart = (DateTime.UtcNow - State.TimeStarted).TotalSeconds;

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
			if (StartTimeout > 0 && timeSincePlanStart >= StartTimeout)
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

			State.OpStatus = planStatus;
		}

		void AdoptPlan()
		{
			//// Add AppState record for not yet existing apps
			//// FIXME
			//// This keeps the AppState small until some plan is operated
			//// Is this desired?
			//// Shouldn't we fill the AppsState with all apps from all plans?
			//foreach( var a in AppDefs )
			//{
			//	if (!_appsState.ContainsKey( a.Id))
			//	{
			//		_appsState[a.Id] = AppState.GetDefault(a);
			//	}
			//}
		}
	}
}
