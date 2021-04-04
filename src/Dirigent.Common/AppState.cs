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
    [ProtoBuf.ProtoContract]
    [DataContract]
    public class AppState
    {
        bool started;
        bool startFailed;
        bool running;
        bool killed;
		bool dying;
		bool restarting;
        bool initialized;
        int exitCode;
        bool planApplied;
        bool disabled;
        DateTime lastChange = DateTime.UtcNow;
		int cpu; // percentage of CPU usage
		int gpu; // percentage of GPU usage
		int memory; // MBytes of memory allocated
        string planName; // in what plan's context the app was started

		public const int RESTARTS_UNLIMITED = -1;  // keep restarting forever
		public const int RESTARTS_UNITIALIZED = -2; // not yet set, will be set by the AppRestarter on first app restart, based on app's configuration
		int restartsRemaining = RESTARTS_UNITIALIZED;

		/// <summary>
		/// process was launched successfully
		/// </summary>
        [ProtoBuf.ProtoMember(1)]
		[DataMember]
        public bool Started
        {
            get { return started; }
            set { started = value; changed(); }
        }

        /// <summary>
        /// process was launched but failed to start
        /// </summary>
        [ProtoBuf.ProtoMember(2)]
        [DataMember]
        public bool StartFailed
        {
            get { return startFailed; }
            set { startFailed = value; changed(); }
        }

        /// <summary>
        /// process is currently running
        /// </summary>
        [ProtoBuf.ProtoMember(3)]
        [DataMember]
        public bool Running
        {
            get { return running; }
            set { running = value; changed(); }
        }

        /// <summary>
        /// forced to terminate	by KillApp request (not by a KillPlan)
        /// </summary>
        [ProtoBuf.ProtoMember(4)]
        [DataMember]
        public bool Killed
        {
            get { return killed; }
            set { killed = value; changed(); }
        }

        /// <summary>
        /// Still dying (after termination request)
        /// </summary>
        [ProtoBuf.ProtoMember(5)]
        [DataMember]
        public bool Dying
        {
            get { return dying; }
            set { dying = value; changed(); }
        }

        /// <summary>
        /// Just being restarted (waiting until dies in order to be lanuched again)
        /// </summary>
        [ProtoBuf.ProtoMember(6)]
        [DataMember]
        public bool Restarting
        {
            get { return restarting; }
            set { restarting = value; changed(); }
        }

        /// <summary>
        /// Process init condition satisfied;
        /// 
        /// By default true upon launching but can be immediately reset by a freshly instantiated AppWatcher acting like an InitDetector.
        /// This is to avoid app to stay in unitialized if an Initdetector-class watcher is not defined
        /// </summary>
        [ProtoBuf.ProtoMember(7)]
        [DataMember]
        public bool Initialized
        {
            get { return initialized; }
            set { initialized = value; changed(); }
        }

        /// <summary>
        /// process was processed by the launch plan already, won't be touched by the launch plan again (until plan is stopped)
        /// </summary>
        [ProtoBuf.ProtoMember(8)]
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
        [ProtoBuf.ProtoMember(9)]
        [DataMember]
        public bool Disabled
        {
            get { return disabled; }
            set { disabled = value; changed(); }
        }

        /// <summary>
        /// process exit code; valid only if is Started && !Running && !Killed
        /// </summary>
        [ProtoBuf.ProtoMember(10)]
        [DataMember]
        public int ExitCode
        {
            get { return exitCode; }
            set { exitCode = value; }
        }

        /// <summary>
        /// Timne of the last change in the application state.
        /// </summary>
        [ProtoBuf.ProtoMember(11)]
        [DataMember]
        public DateTime LastChange
        {
            get { return lastChange; }
            set { lastChange = value; }
        }

        /// <summary>
        ///	percentage of CPU usage
        /// </summary>
        [ProtoBuf.ProtoMember(12)]
        [DataMember]
        public int CPU
        {
            get { return cpu; }
            set { cpu = value; }
        }

        /// <summary>
        ///	percentage of GPU usage
        /// </summary>
        [ProtoBuf.ProtoMember(13)]
        [DataMember]
        public int GPU
        {
            get { return gpu; }
            set { gpu = value; }
        }

        /// <summary>
        ///	MBytes of memory allocated
        /// </summary>
        [ProtoBuf.ProtoMember(14)]
        [DataMember]
        public int Memory
        {
            get { return memory; }
            set { memory = value; }
        }

		/// <summary>
		///	How many restart tries to make before giving up
		/// </summary>
        [ProtoBuf.ProtoMember(15)]
		[DataMember]
		public int RestartsRemaining
		{
			get { return restartsRemaining; }
			set { restartsRemaining = value; }
		}

        /// <summary>
        /// In what plan's context the app was started. Current plan for apps launched directly via LaunchApp.
        /// </summary>
        [ProtoBuf.ProtoMember(16)]
        [DataMember]
        public string PlanName
        {
            get { return planName; }
            set { planName = value; changed(); }
        }

        void changed()
        {
            lastChange = DateTime.UtcNow;
        }
    }


}
