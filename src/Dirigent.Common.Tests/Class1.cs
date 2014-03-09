using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
 
using Dirigent.Common;

namespace DirigentCommons.Test
{
    [TestFixture]
    public class ActorInfoRuleTest
    {

        [SetUp]
        public void Setup()
        {
        }

        //[Test]
        //public void Compilability001()
        //{
        //    Dirigent.Common.Configuration config;
        //    IDirigentControl control;

        //    // chci konfiguraci nacist z XLM
        //    //config = new XMLConfigLoader.Load( "DirigentConfig.xml" );
            
        //    // chci dirigenta nastartovat
        //    //  z konfigurace vycte vse potrebne
        //    //    - jake je jeho machineId
        //    //    - na jakem portu ma bezet
        //    //    - zda ma byt master
        //    //control = new Dirigent( config );

        //    config = control.Config;

        //    // chci zjistit stav konkretni aplikace
        //    AppState appState = control.getAppState( new AppIdTuple("host1", "app1") );

        //    // chci zjistit stav vsech aplikaci
        //    IEnumerable<AppState> appStates = control.getAllAppsState();

        //    // chci zjistit vsechny dostupne plany
        //    var allPlanDefs = config.Plans.Values;

        //    // chci nacist novy plan (a tim zabit aplikace z predchoziho)
        //    ILaunchPlan plan = config.Plans["plan1"];
        //    control.loadPlan( plan );

        //    // chci spustit aplikace dle aktualniho planu
        //    control.startPlan();

        //    // chci zabit aplikace z aktualniho planu
        //    control.stopPlan();

        //    // chci znovuspustit vse z aktualniho planu
        //    control.restartPlan();

        //    // chci spustit konkretni aplikaci z aktualniho planu
        //    control.runApp( new AppIdTuple("host1", "app1") );

        //    // chci restartovat konkretni aplikaci z aktualniho planu
        //    control.restartApp( new AppIdTuple("host1", "app1") );

        //    // chci zabit konkretni aplikaci z aktualniho planu
        //    control.killApp( new AppIdTuple("host1", "app1") );

        //    // chci operativne vytvorit svuj novy plan na zaklade existujiciho
        //    //  vyjmutim nekterych aplikaci
        //    //ILaunchPlan newPlan = existingPlan.Clone();


        //}
    }
}
