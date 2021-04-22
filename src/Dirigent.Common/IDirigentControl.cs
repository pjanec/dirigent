using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Runtime.Serialization;

namespace Dirigent.Common
{
	[ProtoBuf.ProtoContract]
	[DataContract]
	public struct KillAllArgs
	{
		[ProtoBuf.ProtoMember( 1 )]
		public string MachineId; // where to kill the apps; null or empty means everywhere
	}

	public enum EShutdownMode
	{
		PowerOff = 0,
		Reboot = 1
	}

	[ProtoBuf.ProtoContract]
	[DataContract]
	public struct ShutdownArgs
	{
		[ProtoBuf.ProtoMember( 1 )]
		public EShutdownMode Mode;
	}

	[ProtoBuf.ProtoContract]
	[DataContract]
	public struct TerminateArgs
	{
		[ProtoBuf.ProtoMember( 1 )]
		public bool KillApps;  // kill all local apps before terminating

		[ProtoBuf.ProtoMember( 2 )]
		public string MachineId; // where to kill the apps; null or empty means everywhere
	}

	public enum EDownloadMode
	{
		Manual = 0,  // shows a dialog offering to restart the dirigent once the dirigent binaries have been manually overwritten
	}

	[ProtoBuf.ProtoContract]
	[DataContract]
	public struct ReinstallArgs
	{
		[ProtoBuf.ProtoMember( 1 )]
		public EDownloadMode DownloadMode;

		[ProtoBuf.ProtoMember( 2 )]
		public string Url;
	}


	[ProtoBuf.ProtoContract]
	[DataContract]
	public struct ReloadSharedConfigArgs
	{
		[ProtoBuf.ProtoMember( 1 )]
		public bool KillApps;  // kill all local apps before terminating
	}

	/// <summary>
	/// </summary>
	public interface IDirigentControl
	{
		/// <summary>
		/// Checkis if an app is the local one
		/// </summary>
		/// <param name="id"></param>
		/// <returns>true if local, false if remote</returns>
		bool IsLocalApp( AppIdTuple id ) { return false; }

		/// <summary>
		/// Get the status of an application (no matter whether local or remote)
		/// If used on a LocalOperation instance, returns reference to a live writable
		/// state - you can modify the app state.
		/// If used on non-owning instance, you should not modify the state (it
		/// is likely to be overwritten soon anyway).
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		AppState? GetAppState( AppIdTuple id ) { return null; }

		/// <summary>
		/// Get the status of a plan
		/// Taken from local storage; the plan state is synchronized through plan manip command (Start/Stop/Kill...)
		/// </summary>
		PlanState? GetPlanState( string planName ) { return null; }

		/// <summary>
		/// Get the status of all applications (no matter whether locl or remote)
		/// </summary>
		/// <returns></returns>
		Dictionary<AppIdTuple, AppState> GetAllAppsState() { return new Dictionary<AppIdTuple, AppState>(); }

		/// <summary>
		/// Get the status of all applications (no matter whether locl or remote)
		/// </summary>
		/// <returns></returns>
		Dictionary<string, PlanState> GetAllPlansState() { return new Dictionary<string, PlanState>(); }

		/// <summary>
		/// Set the status of a remote application (received from another agent through master for example)
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		void SetRemoteAppState( AppIdTuple id, AppState state ) {}

		/// <summary>
		/// Set the status of a plan (updated after connecting an agent to an already running master)
		/// </summary>
		void SetPlanState( string planName, PlanState state ) {}

		// works for local ops only, not over the net - selects current plan for GUI
		void SelectPlan( string planName ) {}

		/// <summary>
		/// Returns the currently loaded launch plan (or null if none loaded yet)
		/// Works in local scope only (always returns a locally selected plan)
		/// </summary>
		/// <returns></returns>
		ILaunchPlan? GetCurrentPlan() { return null; }

		/// <summary>
		/// Returns the available launch plans.
		/// </summary>
		/// <returns></returns>
		IEnumerable<PlanDef> GetPlanRepo() { return new List<PlanDef>(); }

		/// <summary>
		/// Sets a new plan repository to be returned by GetPlanRepo().
		/// Does not affect the current plan.
		/// </summary>
		/// <param name="planRepo"></param>
		void SetPlanRepo( IEnumerable<ILaunchPlan> planRepo ) {}

		// launch the applications from the plan
		void StartPlan( string planName ) {}

		// stop launching next planned applications from the plan
		void StopPlan( string planName ) {}

		// stop all apps from the plan
		void KillPlan( string planName ) {}

		// kill everything from the plan and start againho planu
		void RestartPlan( string planName ) {}

		// run specific app
		void LaunchApp( AppIdTuple id ) {}

		// kill and then start given app (must be part of some plan)
		void RestartApp( AppIdTuple id ) {}

		// kill specified app
		void KillApp( AppIdTuple id ) {}

		// disable (do not run as part of plan)
		void SetAppEnabled( string planName, AppIdTuple id, bool enabled ) {}

		// set the value of environment variables to be inherited by processes started from Dirigent
		// format of string: VAR1=VALUE1::VAR2=VALUE2
		void SetVars( string vars ) {}

		// stops/kills all apps, plans, restarters, everything
		void KillAll( KillAllArgs args ) {}

		// Terminates the dirigent on all stations; optionally kills all the started apps.
		void Terminate( TerminateArgs args ) {}

		// Reboots or shuts down the stations where dirigent agent is running.
		void Shutdown( ShutdownArgs args ) {}

		// updates the dirigent installation/binaries from given URL; if null or empty, allow manual update of binaries
		// This shuts down the dirigent
		void Reinstall( ReinstallArgs args ) {}

		// Reloads the shared config (i.e. all plans). Optionally leaves the already running apps running.
		// On error loding the new plan, rise an OperationalError and leave the currently loaded plans untouched.
		void ReloadSharedConfig( ReloadSharedConfigArgs args ) {}

	}

}
