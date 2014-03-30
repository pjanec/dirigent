using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace Dirigent.Common
{

    /// <summary>
    /// App status shared among all Dirigent participants.
    /// </summary>
    [Serializable]
    public class AppState
    {
        public bool Started; // process was launched successfully
        public bool StartFailed; // process was launched but failed to start
        public bool Running; // process is currently running
        public bool Killed; // forced to terminate
        public bool Initialized; // process init condition satisfied
        public int ExitCode; // process exit code; valid only if is Started && !Running && !Killed
        public bool PlanApplied; // process was processed by the launch plan already, won't be touched by the launch plan again (until plan is stopped)
    }


}
