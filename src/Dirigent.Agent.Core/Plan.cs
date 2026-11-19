using System;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent
{
	public record PlanApp
	(
		AppDef Def,
		PlanAppState State
	);

	/// <summary>
	/// Description of plan's current state
	/// </summary>
	public class Plan
	{
		public string Name => this.Def.Name;

		public PlanState State = new();

		public IEnumerable<PlanApp> Apps => _apps.Values;

		public IEnumerable<AppDef> AppDefs => Def.AppDefs; // same order as in the plan definiton


		//public PlanScript? Script;

		public double StartTimeout { get; set; }

		//public System.Collections.Generic.IEnumerable<AppDef> getAppDefs() { return Def.AppDefs; }

		public PlanDef Def;

		Dictionary<AppIdTuple, PlanApp> _apps;
		Dictionary<AppIdTuple, AppState> _appsState;
		Master _master;

        AppLaunchPlanner? _appLaunchPlanner;
		PlanRestarter? _restarter;

		/// <summary>The marking this plan began when it started, while it is under way - it outlives a Stop.</summary>
		class Marking
		{
			public List<Web.MarkOperation> Operations = new();
			public DateTime Started = DateTime.UtcNow;
			public double TimeoutSec;
		}

		Marking? _marking;

		/// <summary>The plan is running and waits for its marking to end before it launches the first app.</summary>
		bool _launchAfterMarking;
		string _requestorId; // last one who asked for some plan operation

		// extra vars last time used when starting a plan; null = leave same vars as were set for the apps what app were recentyl launched
        public Dictionary<string,string>? _vars = null;

		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public Plan( PlanDef def, Master master )
		{
			Def = def;
			_master = master;
			_appsState = _master.AppsState;
			_apps = (from ad in def.AppDefs select ad).ToDictionary( ad => ad.Id, ad => new PlanApp( ad, new PlanAppState() ) );  
			_requestorId = string.Empty;
		}

		public void Tick()
		{
			ProcessMarking();

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
		public PlanApp FindApp( AppIdTuple id )
		{				
			if( _apps.TryGetValue( id, out var pa ) )
				return pa;
			throw new UnknownAppInPlanException( id, Name );
		}

        /// <param name="vars">what env/macro vars to set for each app in the plan; null=no change from prev start of the apps</param>
		public void Start( string requestorId, Dictionary<string,string>? vars=null )
		{
			_requestorId = requestorId;

			log.DebugFormat( "Start plan {0}, vars {1}", Name, Tools.EnvVarListToString(vars) );

			if( Def.ApplyOnStart )
			{
				_master.ApplyPlan("", Name, new AppIdTuple());
			}
			
			AdoptPlan();

			// if the plan is running, check for var change and restart the plan if needed
			if ( State.Running )
            {
				
				if( vars is not null &&  // we want to use our vars
					!DictionaryExtensions.DictionariesEqual( _vars, vars, null ) ) // and they are different from  the previous
				{
					log.Debug($"Plan {Name} new vars ({Tools.EnvVarListToString(vars)}) differ from last start {Tools.EnvVarListToString(_vars)}, triggering restart");
					Restart( requestorId, vars );
					return;
				}
			}

            // remember vars
            _vars = vars;

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

                if( _marking is not null )
				{
					// stopped and started again while the marking of the last start is still under way: no second one,
					// the apps are launched when it ends
					_launchAfterMarking = true;
				}
				else
				if( Def.HasMarking )
				{
					StartMarking();
				}
				else
				{
					_appLaunchPlanner = new AppLaunchPlanner( _appsState, this, IsMachineOnline );
				}
            }                    
		}

		/// <summary>
		/// Draws the lines under the files the plan names - and launches the apps once that is done, or if it cannot be.
		/// Marking never fails a plan: whatever goes wrong is recorded and the apps start anyway.
		/// </summary>
		void StartMarking()
		{
			var marking = new Marking() { TimeoutSec = Def.MarkTimeout };
			PlanMarkResult? failure = null;

			try
			{
				if( Def.ClearFiles.Count > 0 )
				{
					var op = Web.MarkOperation.StartForFileDefIds( _master, Web.MarkOperation.EKind.Clear, Def.ClearFiles, string.Join( ";", Def.ClearFiles ), planOwned: true );
					if( op is not null ) marking.Operations.Add( op );
				}

				if( Def.MarkFiles.Count > 0 )
				{
					var op = Web.MarkOperation.StartForFileDefIds( _master, Web.MarkOperation.EKind.Mark, Def.MarkFiles, string.Join( ";", Def.MarkFiles ), planOwned: true );
					if( op is not null ) marking.Operations.Add( op );
				}
			}
			catch( Exception e )
			{
				log.Warn( $"Plan {Name}: could not start marking the files: {e.Message}" );
				failure = new PlanMarkResult();
				failure.Errors.Add( e.Message );
			}

			if( marking.Operations.Count == 0 )
			{
				if( failure is null )
				{
					failure = new PlanMarkResult();
					failure.Errors.Add( "The file definitions named by the plan match nothing." );
					log.Warn( $"Plan {Name}: the file definitions to mark match nothing." );
				}
				State.MarkResult = failure;
				BeginLaunch();
				return;
			}

			_marking = marking;
			_launchAfterMarking = true;
			State.Phase = "Marking files (" + string.Join( ", ", marking.Operations.Select( o => o.Selection ) ) + ")";
			State.MarkStarted = DateTime.UtcNow;
		}

		/// <summary>The apps may be launched: the plan's time for them to come up counts from now.</summary>
		void BeginLaunch()
		{
			State.TimeStarted = DateTime.UtcNow;
			_appLaunchPlanner = new AppLaunchPlanner( _appsState, this, IsMachineOnline );
		}

		/// <summary>
		/// Follows the marking: when every operation has ended - or the plan's MarkTimeout has - records what it came to and
		/// lets the apps go. Also for a plan that was stopped meanwhile, which only wants the result.
		/// </summary>
		void ProcessMarking()
		{
			if( _marking is null ) return;

			var states = _marking.Operations.Select( o => ( Operation: o, State: o.State( _master ) ) ).ToList();
			var allEnded = states.All( x => x.State is not null && !x.State.IsAlive );

			var timedOut = false;
			if( !allEnded )
			{
				if( ( DateTime.UtcNow - _marking.Started ).TotalSeconds < _marking.TimeoutSec ) return;

				// stop the operations FIRST; the apps are not made to wait for them to say so
				timedOut = true;
				foreach( var x in states.Where( x => x.State is null || x.State.IsAlive ) )
					_master.KillScript( _requestorId, x.Operation.ScriptId );
			}

			var result = new PlanMarkResult() { TimedOut = timedOut };

			foreach( var (op, st) in states )
			{
				PlanOwnedScripts.Remove( op.ScriptId );

				if( st is null || st.IsAlive )
				{
					result.Errors.Add( $"{Web.MarkOperation.Word( op.Kind )} of {op.Selection}: not done in {_marking.TimeoutSec:0} s, stopped." );
					continue;
				}

				if( st.Status == EScriptStatus.Cancelled )
				{
					result.Cancelled = true;
					result.Errors.Add( $"{Web.MarkOperation.Word( op.Kind )} of {op.Selection}: cancelled - its script was killed." );
					continue;
				}

				try
				{
					var report = op.ReportOf( st );
					result.Marked += report.machines.Sum( m => m.marked.Count );
					result.Cleared += report.machines.Sum( m => m.cleared.Count );
					result.Skipped += report.machines.Sum( m => m.skippedNotClearable.Count );
					result.Failed += report.machines.Sum( m => m.failed.Count );
					result.Errors.AddRange( report.errors );
				}
				catch( Exception e )
				{
					result.Errors.Add( $"{Web.MarkOperation.Word( op.Kind )} of {op.Selection}: {e.Message}" );
				}
			}

			if( result.TimedOut || result.Cancelled || result.Failed > 0 || result.Errors.Count > 0 )
				log.Warn( $"Plan {Name}: marking the files did not go cleanly (timed out: {result.TimedOut}, cancelled: {result.Cancelled}): {string.Join( "; ", result.Errors )}" );

			_marking = null;
			State.Phase = string.Empty;
			State.MarkResult = result;

			if( _launchAfterMarking && State.Running )
				BeginLaunch();

			_launchAfterMarking = false;
		}

		private bool IsMachineOnline( string machineId )
		{
			var state = _master.GetClientState(machineId);
			return state is not null && state.Connected;
		}

        public void Stop( string requestorId )
        {
			_requestorId = requestorId;

			log.DebugFormat( "Stop plan {0}", Name );

			// the marking, if one is under way, is not cut short - it ends by itself, bounded by MarkTimeout
			_launchAfterMarking = false;

            State.Running = false;
			State.Killing = false;
            _appLaunchPlanner = null;
        }

        public void Kill( string requestorId )
        {
			_requestorId = requestorId;
			log.DebugFormat( "Kill plan {0}", Name );

			AdoptPlan();

            State.Running = false;
			State.Killing = true;

            // kill all apps belonging to the current plan
            foreach( var (id, pa) in _apps )
            {
				var ad = pa.Def;

				// ignore disabled apps
				var appState = _appsState[id];
				if( ad.Disabled )
					continue;

				// attempt to kill
				// this is non-blocking! does not wait for app to die!
				// we would like to stop the app indicating "killed" or "start failed"; we simply want neutral "not running".. => resetAppState
                _master.KillApp( requestorId, id, Net.KillAppFlags.ResetAppState );
                
                // Note:
				// the app status will get reset by processPlan()
				// as soon as the app dies
            }
            
            // stop the launch sequencer
			_launchAfterMarking = false;
            _appLaunchPlanner = null;
        }

        /// <param name="vars">what env/macro vars to set for each app in the plan; null=no change from prev start of the apps</param>
        public void Restart( string requestorId, Dictionary<string,string>? vars=null )
        {
			_requestorId = requestorId;
			log.DebugFormat( "Restart plan {0}, vars {1}", Name, Tools.EnvVarListToString(vars) );

			_restarter = new PlanRestarter( requestorId, this, vars );
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
			foreach( var pa in _apps.Values )
			{
				var ad = pa.Def;
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
			//if(false) // DISABLED 
			//// - Explicit kill needed before being able to start again
			//// The reason is that we can reliably detect that the plan has succeeded
			//// With autokill the plan state goes None - InProgress - None and it's not clear if it was run at all
			//// if not asking quickly enough...
			//{
			//    var currTime = DateTime.UtcNow;
			//
			//	bool anyNonVolatileApp = false;	// is there at least one non-volatile?
			//	bool allAppsProcessed = true;
			//	bool anyStillRunning = false;
			//	foreach (var app in _apps.Values )
			//	{
			//		var ad = app.Def;
			//		var apst = _appsState[ad.Id];
			//
			//		bool offline = apst.IsOffline;
			//
			//		if (!offline && ! (app.State.PlanApplied && (apst.Initialized || apst.StartFailed ) ))
			//		{
			//			allAppsProcessed = false;
			//		}
			//
			//		if ( !offline && apst.Running)
			//			anyStillRunning = true;
			//
			//		if (!ad.Volatile)
			//		{
			//			anyNonVolatileApp = true;
			//		}
			//	}
			//
			//	if (allAppsProcessed && !anyNonVolatileApp && !anyStillRunning) // all apps volatile, all launched and none is running  any longer
			//	{
			//		// Note: this won't kill any app as no apps are running any more.
			//		//       It just make the plan startable again.
			//		Kill( _requestorId );
			//	}
			//
			//}


            // if no plan exists
			// or client re-connected and re-set the plan repo (then we loose the previous RTI)
            if (_appLaunchPlanner == null)
                return;

            // launch all apps planned for current time
            while(true)
			{
	            // try to get an app to launch and launch it immediately
				PlanApp? appToLaunch = _appLaunchPlanner.GetNextAppToLaunch( currentTime );
				if( appToLaunch is null ) break;

				// note: this will set also the Master's PlanApplied flag (the important one), not just the AppState flag from agent (just informative one)
				_master.StartApp( _requestorId, appToLaunch.Def.Id, Name, Net.StartAppFlags.SetPlanApplied, _vars );
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

			// still marking files: nothing is launched yet, so nothing can have failed - and the time for the apps
			// to come up has not begun
			if( _launchAfterMarking && _marking is not null )
			{
				State.OpStatus = PlanState.EOpStatus.InProgress;
				return;
			}


			bool allLaunched = true;
			bool allNonVolatileRunning = true;
			bool anyNonVolatileApp = false;	// is there at least one non-volatile?
			bool allAppsProcessed = true;
			foreach (var app in _apps.Values )
			{
				var ad = app.Def;
				var apst = _appsState[ad.Id];

				if( ad.Disabled )	// ignore disabled apps (as if they are not part of the plan)
					continue;

				if (!(app.State.PlanApplied && apst.Started && apst.Initialized))
				{
					allLaunched = false;
				}

				if (! (app.State.PlanApplied && (apst.Initialized || apst.StartFailed) ))
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

		/// <summary>
		/// Forces setting the PlanApplied flag so that the app launch planner does not
		/// provides the app to be started again (the app might already have been started manually)
		/// </summary>
		/// <param name="id"></param>
		public void SetPlanApplied( AppIdTuple id )
		{
            var app = FindApp( id );
            app.State.PlanApplied = true;
		}

        public void ClearPlanApplied()
        {
            foreach( var app in _apps.Values )
			{
				app.State.PlanApplied = false;
			}        
		}

	}
}
