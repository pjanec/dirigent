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
        /// <summary>
        /// process was launched successfully
        /// </summary>
        public bool Started;

        /// <summary>
        /// process was launched but failed to start
        /// </summary>
        public bool StartFailed;

        /// <summary>
        /// process is currently running
        /// </summary>
        public bool Running;

        /// <summary>
        /// forced to terminate
        /// </summary>
        public bool Killed;
        
        /// <summary>
        /// Process init condition satisfied;
        /// 
        /// By default true upon launching but can be immediately reset by a freshly instantiated AppWatcher acting like an InitDetector.
        /// This is to avoid app to stay in unitialized if an Initdetector-class watcher is not defined
        /// </summary>
        public bool Initialized;
        
        /// <summary>
        /// process exit code; valid only if is Started && !Running && !Killed
        /// </summary>
        public int ExitCode;

        /// <summary>
        /// process was processed by the launch plan already, won't be touched by the launch plan again (until plan is stopped)
        /// </summary>
        public bool PlanApplied;
    }


}
