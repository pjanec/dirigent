using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dirigent.Common
{
    [Serializable]
    public class AppIdTuple
    {
        public string MachineId { get; private set; }
        public string AppId { get; private set; }

        public AppIdTuple(string machineId, string appId)
        {
            MachineId = machineId;
            AppId = appId;
        }

        public AppIdTuple(string machineIdDotAppId)
        {
            AppIdTuple x = fromString(machineIdDotAppId, "");
            MachineId = x.MachineId;
            AppId = x.AppId;
        }

        /// <summary>
        /// Parses string in form "machineId.appId". Uses defaultMachineId if not present in the string.
        /// </summary>
        /// <param name="machineIdDotAppId"></param>
        /// <param name="defaultMachineId"></param>
        /// <returns></returns>
        static public AppIdTuple fromString(string machineIdDotAppId, string defaultMachineId)
        {
            int dotIndex = machineIdDotAppId.IndexOf('.');
            if( dotIndex < 0 )
            {
                return new AppIdTuple(
                    defaultMachineId,
                    machineIdDotAppId
                );
            }
            else
            {
                return new AppIdTuple(
                    machineIdDotAppId.Substring(0, dotIndex),
                    machineIdDotAppId.Substring(dotIndex+1)
                );
            }
        }

        public override string ToString()
        {
            return MachineId+"."+AppId;
        }

        public override bool Equals(System.Object obj)
        {
            // If parameter is null return false.
            if (obj == null)
            {
                return false;
            }

            // If parameter cannot be cast to AppIdTuple return false.
            AppIdTuple p = obj as AppIdTuple;
            if ((System.Object)p == null)
            {
                return false;
            }

            // Return true if the fields match:
            return (MachineId == p.MachineId) && (AppId == p.AppId);
        }

        public bool Equals(AppIdTuple p)
        {
            // If parameter is null return false:
            if ((object)p == null)
            {
                return false;
            }

            // Return true if the fields match:
            return (MachineId == p.MachineId) && (AppId == p.AppId);
        }

        public override int GetHashCode()
        {
            return MachineId.GetHashCode() ^ AppId.GetHashCode();
        }
    }
}
