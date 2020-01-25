using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace Dirigent.Common
{

    /// <summary>
    /// Plan execution status
    /// </summary>
    [DataContract]
    public class PlanState
    {
		[DataMember]
		public bool Running;  // currently taking care of apps (launching, keeping alive...); mutually exclusive with Running
		public bool Killing; //	currently killing apps; mutually exclusive with Running

		public enum EOpStatus
		{
			None,	 // plan not running, not controlling apps
			InProgress,	 // still launching
			Success,  // all apps started and initialized and running
			Failure,  // launching some of the apps failed (start failure, init failure, crash...)
			Killing	// we are killing a plan and some apps are still dying
		}

        [DataMember]
		public EOpStatus OpStatus; // status to report to the user; determined fromthe state of contained apps


        [DataMember]
		public DateTime TimeStarted; // to calculate app-start timeout causing plan failure

    }


}
