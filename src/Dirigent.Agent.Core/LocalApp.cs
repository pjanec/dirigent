﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

namespace Dirigent
{

	/// <summary>
	/// Description of applications's current state and methods to control it (launch/kill/restart..)
	/// </summary>
	public class LocalApp
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public AppIdTuple Id { get; private set; }

        /// <summary>
        /// Definition the will be used the next time the app is started.
        /// </summary>
		public AppDef UpcomingAppDef { get; private set; }

        /// <summary>
        /// Definition used when recently launching the app.
        /// null if never launched yet.
        /// </summary>
		public AppDef RecentAppDef { get; private set; }

        ///<summary>Current state of the app. Published to master.</summary>
		public AppState AppState = new();

		//public AppScript? AppScript;

        public Process? Process => Launcher?.Process;
        public int ProcessId => Process?.Id ?? -1;

        ///<summary>Starts/kills the app process. Null if app is not supposed to be running (not launched)</summary>
		public Launcher? Launcher { get; private set; }



        ///<summary>All watchers currently installed on this app</summary>
        private AppWatcherCollection _watchers = new AppWatcherCollection();

        private SharedContext _sharedContext;

		// extra vars last time used when starting an app
        public Dictionary<string,string> _vars = new();



		public LocalApp( AppDef ad, SharedContext sharedContext )
		{
            Id = ad.Id;
            RecentAppDef = ad;
			UpcomingAppDef = ad;
            AppState.PlanName = ad.PlanName;
            _sharedContext = sharedContext;
		}

		/// <summary>
        /// Sets the app def to be usef for next launch
        /// </summary>
        /// <param name="ad"></param>
        public void UpdateAppDef(AppDef ad)
		{
			UpcomingAppDef = ad;
		}

		public void Tick()
        {
            Launcher?.Tick();

            _watchers.Tick();

            RefreshAppState();
        }

        /// <summary>
        /// Launch the app with the "upcoming" app definition that replaces the "Recent" one
        /// on successful launch.
        /// </summary>
        /// <param name="vars">what env/macro vars to set for a process; null=no change from prev start</param>
        public void StartApp( bool resetRestartsToMax=true, Net.StartAppFlags flags=0, Dictionary<string,string>? vars=null )
        {
            StartApp( UpcomingAppDef, resetRestartsToMax, flags, vars );
        }

        public void StartApp( AppDef appDef, bool resetRestartsToMax=true, Net.StartAppFlags flags=0, Dictionary<string,string>? vars=null )
        {
            log.DebugFormat("Start app {0} {1}, vars {2}", Id, flags, Tools.EnvVarListToString(vars) );
            
            // restart app if running with different vars than what we want right now
            if( Launcher != null && Launcher.Running )
            {
                if( vars is not null )
                {
                    //// compare values (consider empty values as non-exiting variables)
                    //var varsOrig = (from kv in _vars where !String.IsNullOrEmpty(kv.Value) select kv).ToDictionary( x=>x, y=>y );
                    //var varsNew = (from kv in vars where !String.IsNullOrEmpty(kv.Value) select kv).ToDictionary( x=>x, y=>y );
                    
                    // we DO compare also empty variables (as they might delete something from the inherited environment )
                    var varsOrig = _vars;
                    var varsNew = vars;

                    if( !DictionaryExtensions.DictionariesEqual( varsOrig, varsNew, null ) ) // and they are different from  the previous
                    {
                        log.Debug($"App {Id} new vars ({Tools.EnvVarListToString(vars)}) differ from last start {Tools.EnvVarListToString(_vars)}, triggering restart");
                        RestartApp( vars );
                    }
                }
                return;  // app is either running or just restarted, nothing more needed
            }
            
            // remember vars (only if some provided)
            if( vars is not null )
            {
                _vars = vars;
            }

			if( resetRestartsToMax )
			{
				AppState.RestartsRemaining = AppState.RESTARTS_UNITIALIZED;
			}

            // launch the application
            AppState.Started = false;
            AppState.StartFailed = false;
            AppState.Killed = false;
            //AppState.Disabled = appDef.Disabled;
            
            // remove watchers that might have left from previous run
            _watchers.RemoveHavingFlags( IAppWatcher.EFlags.ClearOnLaunch );

            Launcher = new Launcher( appDef, _sharedContext, _vars );

            try
            {
                // process PlanApplied requests
                if( (flags & Net.StartAppFlags.SetPlanApplied) != 0 )
                {
                    AppState.PlanApplied = true;
                }

                if( Launcher.Launch() )
                {
                    // now we know the process was launched with the most recent settings
                    RecentAppDef = appDef;
                    AppState.Started = true;
                    AppState.Initialized = true; // a watcher can set it to false upon its creation if it works like an AppInitDetector
                    AppState.Running = true;  // will be updated in periodical refresh, here we think the app is running as it ahs been just started
    			    AppState.PlanName = appDef.PlanName;

                    #if Windows
				    // install main window styler if we specified the style explicitly
				    if (RecentAppDef.WindowStyle != EWindowStyle.NotSet)
				    {
					    var w = new MainWindowStyler( this );
					    _watchers.ReinstallWatcher( w );
				    }
                    #endif

				    // instantiate init detector (also a watcher)
				    {
                        // compatibility with InitialCondition="timeout 2.0" ... convert to XML definition <timeout>2.0</timeout>
                        {
                            if( !string.IsNullOrEmpty( RecentAppDef.InitializedCondition ) )
                            {
                                string name;
                                string args;
                                AppInitializedDetectorFactory.ParseDefinitionString( RecentAppDef.InitializedCondition, out name, out args );
                                string xmlString = string.Format("<{0}>{1}</{0}>", name, args);

                                var aid = _sharedContext.AppInitializedDetectorFactory.create( this, XElement.Parse(xmlString) );
                                _watchers.ReinstallWatcher( aid );
                            }
                        }

                        foreach( var xml in RecentAppDef.InitDetectors )
                        {
                            var aid = _sharedContext.AppInitializedDetectorFactory.create( this, XElement.Parse(xml));
                            if( aid != null )
                            {
                                _watchers.ReinstallWatcher( aid );
                            }
                        }
                    }

                    #if Windows
				    // instantiate window positioners
				    foreach (var xml in RecentAppDef.WindowPosXml)
				    {
					    var wpo = new WindowPositioner( this, XElement.Parse(xml));
					    _watchers.ReinstallWatcher(wpo);
				    }
                    #endif

				    // instantiate crash watcher
				    if ( RecentAppDef.RestartOnCrash )
                    {
                        var ar = new CrashWatcher( this );
					    ar.OnCrash += () =>
					    {
						    // Activate restarter, continue counting down the number of remaining restarts
						    // as set in appState.RestartsRemaining.
                            _watchers.ReinstallWatcher( new AppRestarter( this, true, vars:vars ) );
					    };
                        _watchers.ReinstallWatcher( ar );
                    }
                }
            }
            catch( Exception ex ) // app launching failed
            {
                log.ErrorFormat("Exception: App \"{0}\"start failure {1}", RecentAppDef, ex);

                AppState.StartFailed = true;
                throw;
            }
        }

        /// <param name="vars">what env/macro vars to set for a process; null=no change</param>
        public void RestartApp( Dictionary<string, string>? vars )
        {
            log.Debug( $"RestartApp {Id} vars {Tools.EnvVarListToString(vars)}" );

			// kill (will do nothing if not running)
			KillApp();

	        // setup restarter (reset to MAX tries)
			AppState.RestartsRemaining = AppState.RESTARTS_UNITIALIZED; // will reset to max tries
            _watchers.ReinstallWatcher( new AppRestarter( this, waitBeforeRestart: false, vars:vars ) ); // restart immediately (no waiting)
        }

        public void KillApp( Net.KillAppFlags flags=0 )
        {
			log.DebugFormat( "Kill app {0} {1}", Id, flags );

            if( Launcher != null ) // already started?
            {
                if (Launcher.Running)
                {
                    AppState.Killed = true;
                }

                log.DebugFormat("Killing app {0}", Id);

                // this just initiates the dying
				Launcher.Kill( flags );
                
				// we maintain the launcher instance until the app actually dies
            }
			else // not started
			{

				// try to adopt before killing
				if( RecentAppDef.AdoptIfAlreadyRunning )
				{
					var launcher = new Launcher( UpcomingAppDef, _sharedContext );
					if( launcher.AdoptAlreadyRunning() )
					{
						launcher.Kill( flags );
					}
				}
				
			}

			// Remove potential pending AppRestarter
			// to avoid the app being restarted automatically
			// after this explicit Kill (the user wants the app to stop until said otherwie)
            _watchers.RemoveWatchersOfType<AppRestarter>();
            _watchers.RemoveWatchersOfType<CrashWatcher>();

            if( (flags & Net.KillAppFlags.ResetAppState) != 0 )
            {
				AppState.PlanApplied = false;
				AppState.Started = false;
				AppState.StartFailed = false;
				AppState.Killed = false;
				AppState.Initialized = false;
				AppState.Running = false;
				AppState.Dying = false;
				AppState.Restarting = false;
			}
        }

        /// <summary>
        /// Updates the status info for all local apps.
        /// </summary>
        void RefreshAppState()
        {
            if( Launcher != null ) // already launched
            {
                AppState.Running = Launcher.Running;
				AppState.Dying = Launcher.Dying;	// if dying=true, then running=true
                AppState.ExitCode = Launcher.ExitCode;

				// nullify launcher if the process not running any more
				if(	!AppState.Running )
				{
					Launcher.Dispose();
					Launcher = null;
				}
            }
            else // not running
            {
                AppState.Running = false;
				AppState.Dying = false;
            }
        }
	}
}
