using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dirigent.Common;
using System.Xml.Linq;

using X = Dirigent.Common.XmlConfigReaderUtils;

namespace Dirigent.Agent
{
	
	/// <summary>
	/// Instantiated for a conrete app, on the agent where the associated app is local,
	/// and just for one single restart operation (removed after each restart).
	/// Waits until the app disappears, then starts it again and deactivates itself.
	/// Counts the remaining number of restarts (stored in appState).
	/// Once a limit is reached, deactivates itself (marks for removal).
	/// When a new restarter is added, it can either continue the counting down
	/// the number of restarts or reset the number of restarts to the AppDef-configured value.
	/// </summary>
	public class AppRestarter : Disposable, IAppWatcher
	{
		/// <summary>
		/// Done restating, can be removed from the system.
		/// </summary>
		public bool ShallBeRemoved { get; protected set; }

        public IAppWatcher.EFlags Flags => 0; // this one should stay after launch
		public LocalApp App => _app;
		
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);
		
        private AppDef _appDef;
        private AppState _appState; // reference to a live writable appState - we will modify it here

        private enum eState 
        {
            Init,
			WaitingForDeath, // app is terminating
			WaitBeforeRestart,
            WaitingBeforeRestart, // giving some time before we restart it
			Restart,
			Disabled
        };

        private eState _state;

        private DateTime _waitingStartTime;
		private LocalApp _app;
		private bool _waitBeforeRestart;
		
        double RESTART_DELAY = 1.0; // how long to wait before restarting the app
        int MAX_TRIES = -1; // how many times to try restarting before giving up; -1 = forever

		public AppRestarter( LocalApp app, bool waitBeforeRestart )
		{
			_app = app;
			_appDef = _app.RecentAppDef;
			_appState = _app.AppState;

            parseXml();

			Reset(waitBeforeRestart);
        }

        void parseXml()
        {
			XElement? xml = null;
			if( !String.IsNullOrEmpty( _appDef.RestarterXml ) )
			{
				var rootedXmlString = String.Format("<root>{0}</root>", _appDef.RestarterXml);
				var xmlRoot = XElement.Parse(rootedXmlString);
				xml = xmlRoot.Element("Restarter");
			}

			if( xml == null ) return;
			
			RESTART_DELAY = X.getDoubleAttr(xml, "delay", RESTART_DELAY, true);
			MAX_TRIES = X.getIntAttr(xml, "maxTries", MAX_TRIES, true);
        }



        public void Tick()
        {
            switch( _state )
            {
                case eState.Init:
                {
					_appState.Restarting = true;

                    if( _appState.Running )
                    {
                        log.DebugFormat("AppRestarter: Waiting for app to die appid {0}", _appDef.Id );
						_state = eState.WaitingForDeath;
                    }
					else
					{
						if( _waitBeforeRestart )
						{
							_state = eState.WaitBeforeRestart;
						}
						else
						{
							// go right to starting
							_state = eState.Restart;
						}
					}
                    break;
                }

                case eState.WaitingForDeath:
                {
                    // has the application terminated?
                    if( !_appState.Running )
                    {
                        _state = eState.WaitBeforeRestart;
                        _waitingStartTime = DateTime.Now;

                    }
                    break;
                }

                case eState.WaitBeforeRestart:
                {
                    log.DebugFormat("AppRestarter: Waiting before restart appid {0}", _appDef.Id );
                    _waitingStartTime = DateTime.Now;
                    _state = eState.WaitingBeforeRestart;
					break;
				}
                case eState.WaitingBeforeRestart:
                {
                    var waitTime = (DateTime.Now - _waitingStartTime).TotalSeconds;
                    if( waitTime > RESTART_DELAY )
                    {
                        _state = eState.Restart;
                    }
                    break;
                }

                case eState.Restart:
                {
					bool launch = false;

					if( _appState.RestartsRemaining == AppState.RESTARTS_UNLIMITED )
					{
						launch = true;
					}
					else
					// if < 0, don't limit the number of restarts...
					if( _appState.RestartsRemaining > 0 )
					{
						_appState.RestartsRemaining--;
						launch = true;
					}

					if( launch )
					{
						// start the app again (and leave the number of restarts as is)
						_app.StartApp( false );
					}
					
					// deactivate itself
					_appState.Restarting = false;
	                ShallBeRemoved = true;
					_state = eState.Disabled;

					break;
				}
				
				case eState.Disabled:
				{
					// do nothing
					break;
				}
            }

        }

		/// <summary>
		/// Starts watching for death from the beginning
		/// </summary>
		public void Reset(bool waitBeforeRestart)
		{
			this._waitBeforeRestart = waitBeforeRestart;

			if( _appState.RestartsRemaining == AppState.RESTARTS_UNITIALIZED )
			{
				InitReseToMax();
			}
			else
			{
				InitContinue();
			}
		}
		
		void InitReseToMax()
		{
			// reset to max remaining tries
			_appState.RestartsRemaining = MAX_TRIES;

			InitContinue();
		}

		void InitContinue()
		{
			if( _appState.RestartsRemaining == 0 ) // no more tries, deactivate itself
			{
				ShallBeRemoved = true;
				_state = eState.Disabled;
			}
			else  // let's continue waiting for death and restartin
			{
				ShallBeRemoved = false;
				_state = eState.Init;
			}
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			// Make sure we don't leave the restarting flag on the app if we are removed in the middle
			// of operation
			_appState.Restarting = false;
		}

	}
}
