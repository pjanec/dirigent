using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace Dirigent.Common
{
    public class SharedConfig
    {
        public Dictionary<string, MachineDef> Machines = new Dictionary<string, MachineDef>();
        public Dictionary<string, ILaunchPlan> Plans = new Dictionary<string, ILaunchPlan>();

        /// <summary>
        /// TCP port for internal communication between agent and master
        /// </summary>
        public int MasterPort = 0;
        public string MasterName = ""; // machineId of machine where master server shall be started

        public string LocalMachineName = ""; // machine id of the computer where the agent is going to run
    }

    public class LocalConfig
    {
        public string LocalMachineId = ""; // machine id of the computer where the agent is going to run
    }
}
