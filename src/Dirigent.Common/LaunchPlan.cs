using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace Dirigent.Common
{
    [ProtoBuf.ProtoContract]
    [DataContract]
    public class LaunchPlan : ILaunchPlan
    {
        [ProtoBuf.ProtoMember(1)]
        [DataMember]
        List<AppDef> appDefs;

        [ProtoBuf.ProtoMember(2)]
        [DataMember]
        string name;
        
        [ProtoBuf.ProtoMember(3)]
        [DataMember]
        double startTimeout;
        
        public LaunchPlan( string name, List<AppDef> appDefs, double startTimeout=-1.0 )
        {
            this.name = name;
            this.appDefs = appDefs;
            this.startTimeout = startTimeout;
        }

        public IEnumerable<AppDef> getAppDefs()
        {
            return appDefs;
        }

        public string Name
        {
            get { return name; }
        }

        //public bool Running
        //{
        //    get { return running; }
        //    set { running = value; }
        //}

		public double StartTimeout
		{
			get { return startTimeout; }
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
