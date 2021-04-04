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
        public EShutdownMode Mode;
    }

    [ProtoBuf.ProtoContract]
    [DataContract]
    public struct TerminateArgs
    {
        public bool KillApps;  // kill all local apps before terminating
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
        public EDownloadMode DownloadMode;
        public string Url;
    }


    [ProtoBuf.ProtoContract]
    [DataContract]
    public struct ReloadSharedConfigArgs
    {
        public bool KillApps;  // kill all local apps before terminating
    }

    /// <summary>
    /// </summary>
    public interface IDirigentControl
    {
        /// <summary>
        /// Get the status of an application (no matter whether local or remote)
		/// If used on a LocalOperation instance, returns reference to a live writable 
		/// state - you can modify the app state.
		/// If used on non-owning instance, you should not modify the state (it
		/// is likely to be overwritten soon anyway).
        /// </summary>
        /// <param name="appIdTuple"></param>
        /// <returns></returns>
        AppState GetAppState( AppIdTuple appIdTuple );

        /// <summary>
        /// Get the status of a plan
		/// Taken from local storage; the plan state is synchronized through plan manip command (Start/Stop/Kill...)
        /// </summary>
		PlanState GetPlanState(string planName);

        /// <summary>
        /// Get the status of all applications (no matter whether locl or remote)
        /// </summary>
        /// <returns></returns>
        Dictionary<AppIdTuple, AppState> GetAllAppsState();

        /// <summary>
        /// Set the status of a remote application (received from another agent through master for example)
        /// </summary>
        /// <param name="appIdTuple"></param>
        /// <returns></returns>
        void SetRemoteAppState( AppIdTuple appIdTuple, AppState state );

        /// <summary>
        /// Set the status of a plan (updated after connecting an agent to an already running master)
        /// </summary>
        void SetPlanState( string planName, PlanState state );

        // works for local ops only, not over the net - selects current plan for GUI
        void SelectPlan( string planName );

		/// <summary>
		/// Returns the currently loaded launch plan (or null if none loaded yet)
		/// Works in local scope only (always returns a locally selected plan)
		/// </summary>
		/// <returns></returns>
		ILaunchPlan GetCurrentPlan();

        /// <summary>
        /// Returns the available launch plans.
        /// </summary>
        /// <returns></returns>
        IEnumerable<ILaunchPlan> GetPlanRepo();

        /// <summary>
        /// Sets a new plan repository to be returned by GetPlanRepo().
        /// Does not affect the current plan.
        /// </summary>
        /// <param name="planRepo"></param>
        void SetPlanRepo( IEnumerable<ILaunchPlan> planRepo );

        // launch the applications from the plan
        void StartPlan(string planName);

        // stop launching next planned applications from the plan
        void StopPlan(string planName);

        // stop all apps from the plan
        void KillPlan(string planName);

        // kill everything from the plan and start againho planu
        void RestartPlan(string planName);

        // run specific app
        void LaunchApp(AppIdTuple appIdTuple);

        // kill and then start given app (must be part of some plan)
        void RestartApp(AppIdTuple appIdTuple);

        // kill specified app
        void KillApp(AppIdTuple appIdTuple);

        // disable (do not run as part of plan)
        void SetAppEnabled(string planName, AppIdTuple appIdTuple, bool enabled);

		// set the value of environment variables to be inherited by processes started from Dirigent
		// format of string: VAR1=VALUE1::VAR2=VALUE2
		void SetVars( string vars );

        // stops/kills all apps, plans, restarters, everything
        void KillAll( KillAllArgs args );

        // Terminates the dirigent on all stations; optionally kills all the started apps.
        void Terminate( TerminateArgs args );

        // Reboots or shuts down the stations where dirigent agent is running.
        void Shutdown( ShutdownArgs args );

        // updates the dirigent installation/binaries from given URL; if null or empty, allow manual update of binaries
        // This shuts down the dirigent
        void Reinstall( ReinstallArgs args );

        // Reloads the shared config (i.e. all plans). Optionally leaves the already running apps running.
        // On error loding the new plan, rise an OperationalError and leave the currently loaded plans untouched.
        void ReloadSharedConfig( ReloadSharedConfigArgs args );

    }

}
