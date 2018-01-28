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
		public bool Running;

		public enum EOpStatus
		{
			None,
			InProgress,
			Success,
			Failure
		}

        [DataMember]
		public EOpStatus OpStatus;
    }


}
