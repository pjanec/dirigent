using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace Dirigent.Common
{

    public interface IDirigentControl
    {
        /// <summary>
        /// Get the status of an application (no matter whether locl or remote)
        /// </summary>
        /// <param name="appIdTuple"></param>
        /// <returns></returns>
        AppState GetAppState( AppIdTuple appIdTuple );

        /// <summary>
        /// Set the status of a remote application (received from another agent through master for example)
        /// </summary>
        /// <param name="appIdTuple"></param>
        /// <returns></returns>
        void SetRemoteAppState( AppIdTuple appIdTuple, AppState state );

        // chci nacist novy plan (a tim zabit aplikace z predchoziho)
        void LoadPlan( ILaunchPlan plan );

        // chci znat aktualni plan
        ILaunchPlan GetPlan();

        // chci spustit aplikace dle aktualniho planu
        void StartPlan();

        // chci zabit aplikace z aktualniho planu
        void StopPlan();

        // chci znovuspustit vse z aktualniho planu
        void RestartPlan();

        // chci spustit konkretni aplikaci z aktualniho planu
        void RunApp(AppIdTuple appIdTuple);

        // chci restartovat konkretni aplikaci z aktualniho planu
        void RestartApp(AppIdTuple appIdTuple);

        // chci zabit konkretni aplikaci z aktualniho planu
        void KillApp(AppIdTuple appIdTuple);

        //// chci operativne vytvorit svuj novy plan na zaklade existujiciho
        //ILaunchPlan clonePlan( ILaunchPlan existingPlan );

        ///// <summary>
        ///// A configuration received from another agent. The reciving agent is supposed to update its own
        ///// configuration information if it differs. The event is fired by the master machine where
        ///// the configuration is considered most up-to-date.
        ///// </summary>
        ///// 
        //public delegate void SharedConfigReceived(SharedConfig config);

        //void setSharedConfigReceivedDelegate(SharedConfigReceived deleg);


    }

}
