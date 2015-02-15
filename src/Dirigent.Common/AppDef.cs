using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Dirigent.Common
{

    /// <summary>
    /// Definition of an application in a launch plan
    /// </summary>
    [DataContract]
    public class AppDef : IEquatable<AppDef>
    {
        /// <summary>
        /// Unique application name; together with MachineId makes a unique name across all applications on all machines.
        /// </summary>
        [DataMember]
        public AppIdTuple AppIdTuple;

        [DataMember]
        public string ExeFullPath;

        [DataMember]
        public string StartupDir;

        [DataMember]
        public string CmdLineArgs;

        [DataMember]
        public int StartupOrder;

        [DataMember]
        public bool RestartOnCrash;

        [DataMember]
        public string InitializedCondition; //  immediate | timeout 5.23 | exitcode 0 | mutex "mymutex1"

        //[DataMember]
        //public List<string> Watchers = new List<string>();

        [DataMember]
        public double SeparationInterval; // seconds before next app can be started on the same computer

        /// <summary>
        /// AppIds of applications that need to be initialized before this app can be started 
        /// </summary>
        [DataMember]
        public List<string> Dependencies;

        [DataMember]
        public bool Enabled;

        [DataMember]
        public bool KillTree; // False = just the process started will be killed; True = all processes originating form the one started are killed also

        [DataMember]
        public ProcessWindowStyle WindowStyle = ProcessWindowStyle.Normal;

        [DataMember]
        public string WindowPosXml;



        public bool Equals(AppDef other)
        {
            if (other == null)
                return false;

            if (
                this.AppIdTuple == other.AppIdTuple &&
                this.ExeFullPath == other.ExeFullPath &&
                this.StartupDir == other.StartupDir &&
                this.CmdLineArgs == other.CmdLineArgs &&
                this.StartupOrder == other.StartupOrder &&
                this.RestartOnCrash == other.RestartOnCrash &&
                this.InitializedCondition == other.InitializedCondition &&
                this.SeparationInterval == other.SeparationInterval &&
                ( // either both dependecies are null or they are the same list
                  (this.Dependencies == null && other.Dependencies==null)
                    ||
                  (
                    this.Dependencies != null &&
                    other.Dependencies != null &&
                    this.Dependencies.SequenceEqual(other.Dependencies)
                  )
                ) &&
                this.WindowStyle == other.WindowStyle &&
                this.WindowPosXml == other.WindowPosXml &&
                //this.Watchers.SequenceEqual(other.Watchers) &&
                true
            )
                return true;
            else
                return false;
        }

        public override bool Equals(Object obj)
        {
            if (obj == null)
                return false;

            AppDef personObj = obj as AppDef;
            if (personObj == null)
                return false;
            else
                return Equals(personObj);
        }

        public override int GetHashCode()
        {
            return this.AppIdTuple.GetHashCode() ^ this.ExeFullPath.GetHashCode();
        }

        public static bool operator ==(AppDef person1, AppDef person2)
        {
            if ((object)person1 == null || ((object)person2) == null)
                return Object.Equals(person1, person2);

            return person1.Equals(person2);
        }

        public static bool operator !=(AppDef person1, AppDef person2)
        {
            if (person1 == null || person2 == null)
                return !Object.Equals(person1, person2);

            return !(person1.Equals(person2));
        }
    }

}
