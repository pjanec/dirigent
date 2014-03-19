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

        public bool Equals(ILaunchPlan other)
        {
            if (other == null)
                return false;

            if (this.Name == other.Name
                  &&
                this.appDefs.SequenceEqual( other.getAppDefs() )
                )
                return true;
            else
                return false;
        }

        public override bool Equals(Object obj)
        {
            if (obj == null)
                return false;

            LaunchPlan personObj = obj as LaunchPlan;
            if (personObj == null)
                return false;
            else
                return Equals(personObj);
        }

        public override int GetHashCode()
        {
            return this.Name.GetHashCode();
        }

    
    }
}
