using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

using Dirigent.Common;

namespace Dirigent.Agent.Core
{
    /// <summary>
    /// Additional data describing how plan is executed on local agent
    /// </summary>
    public class PlanRuntimeInfo
    {
        //private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		//public ILaunchPlan Plan; // current plan
		public PlanState State;
		//public bool Running { get; set;  }

		
        public LaunchDepsChecker launchDepChecker;
        public LaunchSequencer launchSequencer; // non-null only when plan is running


        public PlanRuntimeInfo()
        {
			launchSequencer = new LaunchSequencer();
			State = new PlanState();
        }
    }
}
