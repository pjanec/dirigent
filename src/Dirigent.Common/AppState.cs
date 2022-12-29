using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;

namespace Dirigent
{

	/// <summary>
	/// App status shared among all Dirigent participants.
	/// </summary>
	//[MessagePack.MessagePackObject]
	public class AppState
	{
		[Flags]
		public enum FL
		{
			Started         = 1 << 0,
			StartFailed     = 1 << 1,
			Running         = 1 << 2,
			Killed          = 1 << 3,
			Dying           = 1 << 4,
			Restarting      = 1 << 5,
			Initialized     = 1 << 6,
			PlanApplied     = 1 << 7,
			//Disabled        = 1 << 8,
		}

		//[MessagePack.Key( 1 )]
		public FL _flags; // note: public because of MessagePacks refuses to serialize private fields

		////[MessagePack.Key( 2 )]
		//protected int exitCode;

		// UTC time of last update, recalculated to local time (as the clock migh differ on different computers)
		// On Agent, the agent's UTC time of last update
		// On Master, the UTC time of last update recalculated to master's local time
		// On Gui, the UTC time of last update recalculated to gui's local time
		//[MessagePack.Key( 3 )]
		public DateTime _lastChange = DateTime.UtcNow;  // note: public because of MessagePacks refuses to serialize private fields

		////[MessagePack.Key( 4 )]
		//protected float cpu { get; private set; }; // percentage of CPU usage; negative = N/A

		////[MessagePack.Key( 5 )]
		//protected float gpu; // percentage of GPU usage

		////[MessagePack.Key( 6 )]
		//protected float memory; // MBytes of memory allocated; negative = N/A

		////[MessagePack.Key( 7 )]
		//protected string? planName; // in what plan's context the app was started

		public const int RESTARTS_UNLIMITED = -1;  // keep restarting forever
		public const int RESTARTS_UNITIALIZED = -2; // not yet set, will be set by the AppRestarter on first app restart, based on app's configuration

		////[MessagePack.Key( 8 )]
		//int restartsRemaining = RESTARTS_UNITIALIZED;

		bool Is( FL value )
		{
			return ( _flags & value ) == value;
		}

		void Set( FL value, bool toSet )
		{
			if( toSet ) _flags |= value;
			else _flags &= ~value;

			changed();
		}

		/// <summary>
		/// process was launched successfully
		/// </summary>
		[MessagePack.IgnoreMember]
		public bool Started
		{
			get { return Is( FL.Started ); }
			set { Set( FL.Started, value ); }
		}

		/// <summary>
		/// process was launched but failed to start
		/// </summary>
		[MessagePack.IgnoreMember]
		public bool StartFailed
		{
			get { return Is( FL.StartFailed ); }
			set { Set( FL.StartFailed, value ); }
		}

		/// <summary>
		/// process is currently running
		/// </summary>
		[MessagePack.IgnoreMember]
		public bool Running
		{
			get { return Is( FL.Running ); }
			set { Set( FL.Running, value ); }
		}

		/// <summary>
		/// forced to terminate	by KillApp request (not by a KillPlan)
		/// </summary>
		[MessagePack.IgnoreMember]
		public bool Killed
		{
			get { return Is( FL.Killed ); }
			set { Set( FL.Killed, value ); changed(); }
		}

		/// <summary>
		/// Still dying (after termination request)
		/// </summary>
		[MessagePack.IgnoreMember]
		public bool Dying
		{
			get { return Is( FL.Dying ); }
			set { Set( FL.Dying, value ); changed(); }
		}

		/// <summary>
		/// Just being restarted (waiting until dies in order to be lanuched again)
		/// </summary>
		[MessagePack.IgnoreMember]
		public bool Restarting
		{
			get { return Is( FL.Restarting ); }
			set { Set( FL.Restarting, value ); changed(); }
		}

		/// <summary>
		/// Process init condition satisfied;
		///
		/// By default true upon launching but can be immediately reset by a freshly instantiated AppWatcher acting like an InitDetector.
		/// This is to avoid app to stay in unitialized if an Initdetector-class watcher is not defined
		/// </summary>
		[MessagePack.IgnoreMember]
		public bool Initialized
		{
			get { return Is( FL.Initialized ); }
			set { Set( FL.Initialized, value ); changed(); }
		}

		/// <summary>
		/// Indicates that the app has been attempted to start a part of some plan.
		/// Just an indication for being able to show the "Planned" state, not affecting any logic.
		/// Availale to any endpoint receiving app states. NOT USED BY MASTER!
        /// WARNING: This is trying to mimic the value of Master's AppDef.PlanApplied variable.
		///          As master does not publish its internal PlanApplied, this is just an approximation.
        ///          BUT (theoretically) might be set also on different occasion when
        ///          starting the app with non-empty planName from some reason! 
		/// </summary>
		[MessagePack.IgnoreMember]
		public bool PlanApplied
		{
			get { return Is( FL.PlanApplied ); }
			set { Set( FL.PlanApplied, value ); changed(); }
		}

		///// <summary>
		///// Whether the app has been disabled from execution as part of the plan;
		///// This is set by the owner
		///// </summary>
		//[MessagePack.IgnoreMember]
		//public bool Disabled
		//{
		//	get { return Is( FL.Disabled ); }
		//	set { Set( FL.Disabled, value ); }
		//}

		/// <summary>
		/// process exit code; valid only if is Started && !Running && !Killed
		/// </summary>
		//[MessagePack.Key( 4 )]
		public int ExitCode;

		/// <summary>
		/// Timne of the last change in the application state.
		/// </summary>
		//[MessagePack.Key( 5 )]
		public DateTime LastChange
		{
			get { return _lastChange; }
			set { _lastChange = value; }
		}

		/// <summary>
		///	percentage of CPU usage
		/// </summary>
		//[MessagePack.Key( 6 )]
		public float CPU;

		/// <summary>
		///	percentage of GPU usage
		/// </summary>
		//[MessagePack.Key( 7 )]
		public float GPU;

		/// <summary>
		///	MBytes of memory allocated
		/// </summary>
		//[MessagePack.Key( 8 )]
		public float Memory;

		/// <summary>
		///	How many restart tries to make before giving up
		/// </summary>
		//[MessagePack.Key( 9 )]
		public int RestartsRemaining = RESTARTS_UNITIALIZED;

		string? planName;

		/// <summary>
		/// In what plan's context the app was started. Current plan for apps launched directly via LaunchApp.
		/// </summary>
		//[MessagePack.Key( 10 )]
		public string? PlanName
		{
			get { return planName; }
			set { planName = value; changed(); }
		}

		/// <summary>
		/// -1 = N/A (app not running).
		/// </summary>
		//[MessagePack.Key( 11 )]
		public int PID;

		void changed()
		{
			_lastChange = DateTime.UtcNow;
		}

		[MessagePack.IgnoreMember]
		public bool IsOffline => DateTime.UtcNow - _lastChange  > TimeSpan.FromSeconds(3); 

		public static AppState GetDefault( AppDef ad )
		{
			return new AppState()
			{
				Initialized = false,
				Running = false,
				Started = false,
				Dying = false,
				//Disabled = ad.Disabled
				_lastChange = DateTime.MinValue
			};
		}

	}


}
