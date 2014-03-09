using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace Dirigent.Common
{

    public interface IDirigentControl
    {
        // chci zjistit stav konkretni aplikace
        AppState GetAppState( AppIdTuple appIdTuple );

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
    }

}
