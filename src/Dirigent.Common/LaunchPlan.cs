using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace Dirigent.Common
{
    [DataContract]
    public class LaunchPlan : ILaunchPlan
    {
        [DataMember]
        List<AppDef> appDefs;

        [DataMember]
        string name;

        [DataMember]
        bool running;
        
        public LaunchPlan( string name, List<AppDef> appDefs )
        {
            this.name = name;
            this.appDefs = appDefs;
            this.running = false;
        }

        public IEnumerable<AppDef> getAppDefs()
        {
            return appDefs;
        }

        public string Name
        {
            get { return name; }
        }

        public bool Running
        {
            get { return running; }
            set { running = value; }
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
