using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dirigent.Common;
using System.Xml.Linq;

namespace Dirigent.Agent.Core
{
	
	/// <summary>
	/// Restarts given plan.
	/// Instantiated upon RestartPlan request on all agents.
	/// Kills the plan first, waits until it is finished (Status=None) and then starts it again.
	/// Note: Each agent operates on its local level only in distributed manner - each handle just
	///       its local applications from the plan.
	/// When done, deactivates itself.
	/// </summary>
	public class PlanRestarter
	{
		/// <summary>
		/// Done restating, can be removed from the system.
		/// </summary>
		public bool ShallBeRemoved { get; protected set; }

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
		
        string planName;

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
		
		public PlanRestarter(string planName, IDirigentControl localOps, XElement xml)
		{
            this.planName = planName;
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
			var planState = localOps.GetPlanState( planName ); 

            switch( state )
            {
                case eState.Killing:
                {
                    if( planState.Running )
                    {
                        localOps.KillPlan( planName );

                        log.DebugFormat("PlanRestarter: Waiting for plan to die; plan= {0}", planName );
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
                    // has the plan terminated?
                    if( !planState.Running && !planState.Killing )
                    {
                        state = eState.WaitingBeforeRestart;
                        waitingStartTime = DateTime.Now;

                        log.DebugFormat("PlanRestarter: Waiting before restart; plan {0}", planName );
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
					localOps.StartPlan( planName );

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
