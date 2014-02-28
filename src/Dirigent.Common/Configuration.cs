using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace Dirigent.Common
{
    public class Configuration
    {
        public Dictionary<string, MachineDef> Machines;
        public Dictionary<string, ILaunchPlan> Plans;

        /// <summary>
        /// TCP port for internal communication between agent and master
        /// </summary>
        public int IntercomPort;
    }

}
