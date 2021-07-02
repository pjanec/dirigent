using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Dirigent
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

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);
        private string _requestorId;
		

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
        Plan _plan;
        Dictionary<string,string>? _vars=null;
		
		public PlanRestarter( string requestorId, Plan plan, Dictionary<string,string>? vars=null )
		{
            _requestorId = requestorId;
            _plan = plan;    
            _vars = vars;

			Reset();
        }


        public void Tick()
        {
			var planState = _plan.State; 

            switch( state )
            {
                case eState.Killing:
                {
                    if( planState.Running )
                    {
                        _plan.Kill( _requestorId );

                        log.DebugFormat("PlanRestarter: Waiting for plan to die; plan= {0}", _plan.Name );
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

                        log.DebugFormat("PlanRestarter: Waiting before restart; plan {0}", _plan.Name );
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
					_plan.Start( _requestorId, _vars );

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
