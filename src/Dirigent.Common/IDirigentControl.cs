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
        /// what machine are we running on
        /// </summary>
        string LocalMachineId { get; }

        Dirigent.Common.Configuration Config { get; }

        // chci zjistit stav konkretni aplikace
        AppState getAppState( string machineId, string appId );

        // chci zjistit stav vsech aplikaci
        IEnumerable<AppState> getAllAppsState();

        // chci nacist novy plan (a tim zabit aplikace z predchoziho)
        void loadPlan( ILaunchPlan plan );

        // chci znat aktualni plan
        ILaunchPlan getPlan();

        // chci spustit aplikace dle aktualniho planu
        void startPlan();

        // chci zabit aplikace z aktualniho planu
        void stopPlan();

        // chci znovuspustit vse z aktualniho planu
        void restartPlan();

        // chci spustit konkretni aplikaci z aktualniho planu
        void runApp( string machineId, string appId );

        // chci restartovat konkretni aplikaci z aktualniho planu
        void restartApp( string machineId, string appId );

        // chci zabit konkretni aplikaci z aktualniho planu
        void killApp( string machineId, string appId );

        // chci operativne vytvorit svuj novy plan na zaklade existujiciho
        ILaunchPlan clonePlan( ILaunchPlan existingPlan );
    }

}
