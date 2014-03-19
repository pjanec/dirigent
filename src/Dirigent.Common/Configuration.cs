using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace Dirigent.Common
{
    public class SharedConfig
    {
        //public Dictionary<string, MachineDef> Machines = new Dictionary<string, MachineDef>();
        public List<ILaunchPlan> Plans = new List<ILaunchPlan>();
    }

    //public class LocalConfig
    //{
    //    public string LocalMachineId = ""; // machine id of the computer where the agent is going to run
    //}
}
