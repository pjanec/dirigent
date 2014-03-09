using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Common;

namespace Dirigent.Agent.Core
{
    public class LocalApp
    {
        public AppDef AppDef;
        public ILauncher launcher; // null if not launched or if killed
        public IAppInitializedDetector appInitDetector;
    }
}
