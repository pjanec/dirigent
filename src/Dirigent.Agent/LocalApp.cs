using System;
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

		public AppIdTuple Id => this.AppDef.AppIdTuple;

        ///<summary>Definition used when recently launching the app.</summary>
		public AppDef AppDef { get; private set; }

        ///<summary>Current state of the app. Published to master.</summary>
		public AppState AppState = new();

		//public AppScript? AppScript;

        ///<summary>Starts/kills the app process. Null if app is not supposed to be running (not launched)</summary>
		public Launcher? Launcher { get; private set; }



        ///<summary>All watchers currently installed on this app</summary>
        private AppWatcherCollection _watchers = new AppWatcherCollection();

        private SharedContext _sharedContext;


		public LocalApp( AppDef appDef, SharedContext sharedContext )
		{
			AppDef = appDef;
            _sharedContext = sharedContext;
		}

		public void UpdateAppDef( AppDef appDef )
		{
			AppDef = appDef;
		}

		public void Launch()
		{
			
		}

        public void Tick()
        {
            _watchers.Tick();
        }

        class FakeCtrl : IDirigentControl {}

        public void LaunchAppInternal( bool resetRestartsToMax, string planName )
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
			AppState.PlanName = planName;
            
            // remove watchers that might have left from previous run
            _watchers.RemoveMatching( IAppWatcher.EFlags.ClearOnLaunch );

            Launcher = new Launcher( new FakeCtrl(), AppDef, _sharedContext, planName );

            try
            {
                Launcher.Launch();
                AppState.Started = true;
                AppState.Initialized = true; // a watcher can set it to false upon its creation if it works like an AppInitDetector

                #if Windows
				// install main window styler if we specified the style explicitly
				if (AppDef.WindowStyle != EWindowStyle.NotSet)
				{
					var w = new MainWindowStyler( this );
					_watchers.ReinstallWatcher( w );
				}
                #endif

				// instantiate init detector (also a watcher)
				{
                    // compatibility with InitialCondition="timeout 2.0" ... convert to XML definition <timeout>2.0</timeout>
                    {
                        if( !string.IsNullOrEmpty( AppDef.InitializedCondition ) )
                        {
                            string name;
                            string args;
                            AppInitializedDetectorFactory.ParseDefinitionString( AppDef.InitializedCondition, out name, out args );
                            string xmlString = string.Format("<{0}>{1}</{0}>", name, args);

                            var aid = _sharedContext.AppInitializedDetectorFactory.create( this, XElement.Parse(xmlString) );
                            _watchers.ReinstallWatcher( aid );
                        }
                    }

                    foreach( var xml in AppDef.InitDetectors )
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
				foreach (var xml in AppDef.WindowPosXml)
				{
					var wpo = new WindowPositioner( this, XElement.Parse(xml));
					_watchers.ReinstallWatcher(wpo);
				}
                #endif

				// instantiate crash watcher
				if ( AppDef.RestartOnCrash )
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
            catch( Exception ex ) // app launching failed
            {
                log.ErrorFormat("Exception: App \"{0}\"start failure {1}", AppDef.AppIdTuple, ex);

                AppState.StartFailed = true;
                throw;
            }
        }
	}
}
