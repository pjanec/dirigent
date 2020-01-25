using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace Dirigent.Common
{

    /// <summary>
    /// App status shared among all Dirigent participants.
    /// </summary>
    [DataContract]
    public class AppState
    {
        bool started;
        bool startFailed;
        bool running;
        bool killed;
		bool dying;
        bool initialized;
        int exitCode;
        bool planApplied;
        bool disabled;
        DateTime lastChange = DateTime.UtcNow;
		int cpu; // percentage of CPU usage
		int gpu; // percentage of GPU usage
		int memory; // MBytes of memory allocated
    
        /// <summary>
        /// process was launched successfully
        /// </summary>
        [DataMember]
        public bool Started
        {
            get { return started; }
            set { started = value; changed(); }
        }

        /// <summary>
        /// process was launched but failed to start
        /// </summary>
        [DataMember]
        public bool StartFailed
        {
            get { return startFailed; }
            set { startFailed = value; changed(); }
        }

        /// <summary>
        /// process is currently running
        /// </summary>
        [DataMember]
        public bool Running
        {
            get { return running; }
            set { running = value; changed(); }
        }

        /// <summary>
        /// forced to terminate	by KillApp request (not by a KillPlan)
        /// </summary>
        [DataMember]
        public bool Killed
        {
            get { return killed; }
            set { killed = value; changed(); }
        }

        /// <summary>
        /// Still dying (after termination request)
        /// </summary>
        [DataMember]
        public bool Dying
        {
            get { return dying; }
            set { dying = value; changed(); }
        }

        /// <summary>
        /// Process init condition satisfied;
        /// 
        /// By default true upon launching but can be immediately reset by a freshly instantiated AppWatcher acting like an InitDetector.
        /// This is to avoid app to stay in unitialized if an Initdetector-class watcher is not defined
        /// </summary>
        [DataMember]
        public bool Initialized
        {
            get { return initialized; }
            set { initialized = value; changed(); }
        }

        /// <summary>
        /// process was processed by the launch plan already, won't be touched by the launch plan again (until plan is stopped)
        /// </summary>
        [DataMember]
        public bool PlanApplied
        {
            get { return planApplied; }
            set { planApplied = value; changed(); }
        }

        /// <summary>
        /// Whether the app has been disabled from execution as part of the plan;
        /// This is set by the owner 
        /// </summary>
        [DataMember]
        public bool Disabled
        {
            get { return disabled; }
            set { disabled = value; changed(); }
        }

        /// <summary>
        /// process exit code; valid only if is Started && !Running && !Killed
        /// </summary>
        [DataMember]
        public int ExitCode
        {
            get { return exitCode; }
            set { exitCode = value; }
        }

        /// <summary>
        /// Timne of the last change in the application state.
        /// </summary>
        [DataMember]
        public DateTime LastChange
        {
            get { return lastChange; }
            set { lastChange = value; }
        }

        /// <summary>
        ///	percentage of CPU usage
        /// </summary>
        [DataMember]
        public int CPU
        {
            get { return cpu; }
            set { cpu = value; }
        }

        /// <summary>
        ///	percentage of GPU usage
        /// </summary>
        [DataMember]
        public int GPU
        {
            get { return gpu; }
            set { gpu = value; }
        }

        /// <summary>
        ///	MBytes of memory allocated
        /// </summary>
        [DataMember]
        public int Memory
        {
            get { return memory; }
            set { memory = value; }
        }

        void changed()
        {
            lastChange = DateTime.UtcNow;
        }
    }


}
