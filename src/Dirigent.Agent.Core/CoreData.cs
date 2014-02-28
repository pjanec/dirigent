using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Common;

namespace Dirigent.Agent.Core
{
    public class Data
    {
        public Configuration config;

        /// <summary>
        /// The state of applications from the currently loaded launch plan
        /// </summary>
        public Dictionary<string, AppState> apps;

        /// <summary>
        /// The state of all the machines
        /// </summary>
        public Dictionary<string, MachineState> machines;
    }

}
