using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dirigent.Common;
using System.Xml.Linq;

namespace Dirigent.Agent.Core
{
	
	/// <summary>
	/// Restarts given application.
	/// Instantiated upon RestartApp request on the agent where the associated app is local.
	/// Kills the app, waits until it disappears and then starts it again.
	/// When done, deactivates itself.
	/// </summary>
	public class AppRestarter
	{
		/// <summary>
		/// Done restating, can be removed from the system.
		/// </summary>
		public bool ShallBeRemoved { get; protected set; }

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
		
        AppDef appDef;

        enum eState 
        {
            Killing,
			WaitingForDeath, // app is terminating
            WaitingBeforeRestart, // giving some time before we restart it
            Starting,
			Finished
        };

        eState state;

        readonly double RESTART_DELAY = 0.5; // howlong to wait before restarting the app

        DateTime waitingStartTime;
		IDirigentControl localOps;
		
		public AppRestarter(AppDef appDef, IDirigentControl localOps, XElement xml)
		{
            this.appDef = appDef;
			this.localOps = localOps;

            parseXml( xml );

			Reset();
        }

        void parseXml( XElement xml )
        {
            //pos = new WindowPos();
            
            //if( xml != null )
            //{
            //    var xrect = xml.Attribute("rect");
            //    if( xrect != null )
            //    {
            //        var myRegex = new Regex(@"\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*");
            //        var m = myRegex.Match( (string) xrect );
            //        if( m != null && m.Success)
            //        {
            //            pos.Rect = new System.Drawing.Rectangle(
            //                int.Parse(m.Groups[1].Value),
            //                int.Parse(m.Groups[2].Value),
            //                int.Parse(m.Groups[3].Value),
            //                int.Parse(m.Groups[4].Value)
            //            );
            //        }
            //    }

            //    pos.Screen = X.getIntAttr(xml, "screen", 0);
            //    pos.TitleRegExp = X.getStringAttr(xml, "titleregexp", null);
            //    pos.Keep = X.getBoolAttr(xml, "keep", false);
            //    pos.Topmost = X.getBoolAttr(xml, "topmost", false);
            //}
        }



        public void Tick()
        {
			var appState = localOps.GetAppState( appDef.AppIdTuple ); 

            switch( state )
            {
                case eState.Killing:
                {
                    if( appState.Running )
                    {
                        localOps.KillApp( appDef.AppIdTuple );

                        log.DebugFormat("AppRestarter: Waiting for app to die appid {0}", appDef.AppIdTuple );
						state = eState.WaitingForDeath;
                    }
					else
					{
						// go right to starting
						state = eState.Starting;
					}
                    break;
                }

                case eState.WaitingForDeath:
                {
                    // has the application terminated?
                    if( !appState.Running )
                    {
                        state = eState.WaitingBeforeRestart;
                        waitingStartTime = DateTime.Now;

                        log.DebugFormat("AppRestarter: Waiting before restart appid {0}", appDef.AppIdTuple );
                    }
                    break;
                }

                case eState.WaitingBeforeRestart:
                {
                    var waitTime = (DateTime.Now - waitingStartTime).TotalSeconds;
                    if( waitTime > RESTART_DELAY )
                    {
                        state = eState.Starting;
                    }
                    break;
                }

                case eState.Starting:
                {
					localOps.LaunchApp( appDef.AppIdTuple );

                    state = eState.Finished;
					break;
                }

                case eState.Finished:
                {
                    // do nothing, wait until this instance is removed
                    ShallBeRemoved = true;
					break;
                }
            }

        }

		public void Reset()
		{
			ShallBeRemoved = false;
			state = eState.Killing;
		}
	}
}
