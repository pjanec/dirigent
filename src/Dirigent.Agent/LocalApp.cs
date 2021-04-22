﻿using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Dirigent.Common;

namespace Dirigent.Agent
{

	/// <summary>
	/// Description of applications's current state
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

        ///<summary>Starts/kills the app process. Null if app is not supposed to be running (not launched)</summary>
		public Launcher? Launcher { get; private set; }



        ///<summary>All watchers currently installed on this app</summary>
        private AppWatcherCollection _watchers = new AppWatcherCollection();

        private SharedContext _sharedContext;


		public LocalApp( AppDef ad, SharedContext sharedContext )
		{
            Id = ad.Id;
			UpcomingAppDef = ad;
            AppState.PlanName = ad.PlanName;
            _sharedContext = sharedContext;
		}

		public void UpdateAppDef(AppDef ad)
		{
			UpcomingAppDef = ad;
		}

		//public void Launch()
		//{

		//}

		public void Tick()
        {
            _watchers.Tick();
        }

        class FakeCtrl : IDirigentControl {}

        public void LaunchApp( bool resetRestartsToMax=true )
        {
            LaunchApp( UpcomingAppDef, resetRestartsToMax );
        }

        public void LaunchApp( AppDef appDef, bool resetRestartsToMax=true )
        {
            // don't do anything if the app is already running
            if( Launcher != null && Launcher.Running )
            {
                return;
            }

			if( resetRestartsToMax )
			{
				AppState.RestartsRemaining = AppState.RESTARTS_UNITIALIZED;
			}

            log.DebugFormat("Launching app {0}", Id);

            
            // launch the application
            AppState.Started = false;
            AppState.StartFailed = false;
            AppState.Killed = false;
            
            // remove watchers that might have left from previous run
            _watchers.RemoveMatching( IAppWatcher.EFlags.ClearOnLaunch );

            Launcher = new Launcher( new FakeCtrl(), appDef, _sharedContext );

            try
            {
                if( Launcher.Launch() )
                {
                    // now we know the process was launched with the most recent settings
                    RecentAppDef = appDef;
                    AppState.Started = true;
                    AppState.Initialized = true; // a watcher can set it to false upon its creation if it works like an AppInitDetector
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
                            _watchers.ReinstallWatcher( new AppRestarter( this, true ) );
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
	}
}