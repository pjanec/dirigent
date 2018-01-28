using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace Dirigent.Common
{

    /// <summary>
    /// </summary>
    public interface IDirigentControl
    {
        /// <summary>
        /// Get the status of an application (no matter whether locl or remote)
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


    }

}
