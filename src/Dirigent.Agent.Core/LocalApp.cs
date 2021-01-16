using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Common;

namespace Dirigent.Agent.Core
{
    /// <summary>
    /// The control structure of an application that should be running on a local machine
    /// </summary>
    public class LocalApp
    {
        public AppDef AppDef;
        public Launcher launcher; // null if not launched or if killed
        //public IAppInitializedDetector appInitDetector;
        public List<IAppWatcher> watchers;
    }
}
