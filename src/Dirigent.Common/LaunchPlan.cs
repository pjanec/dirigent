using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dirigent.Common
{
    [Serializable]
    public class LaunchPlan : ILaunchPlan
    {
        List<AppDef> appDefs;
        string name;
        
        public LaunchPlan( string name, List<AppDef> appDefs )
        {
            this.name = name;
            this.appDefs = appDefs;
        }

        public IEnumerable<AppDef> getAppDefs()
        {
            return appDefs;
        }

        public string Name
        {
            get { return name; }
        }
    }
}
