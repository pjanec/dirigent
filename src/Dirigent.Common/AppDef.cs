using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace Dirigent.Common
{

    /// <summary>
    /// Definition of an application in a launch plan
    /// </summary>
    public class AppDef
    {
        /// <summary>
        /// Unique application name; together with MachineId makes a unique name across all applications on all machines.
        /// </summary>
        public string AppId;

        public string ExeFullPath;
        public string StartupDir;
        public string CmdLineArgs;
        public int StartupOrder;
        public bool RestartOnCrash;
        public string MachineId;

        /// <summary>
        /// AppIds of applications that need to be initialized before this app can be started 
        /// </summary>
        public List<string> Dependencies;

        //public IAppInitializedDetector AppInitializedDetector;

        public bool Enabled;
    }

}
