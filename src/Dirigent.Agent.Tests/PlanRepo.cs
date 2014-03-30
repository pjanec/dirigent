using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Common;

namespace Dirigent.Agent.Tests
{
    public class PlanRepo
    {
        public static Dictionary<string, AppDef> ads = new Dictionary<string, AppDef>()
        {
            { "a", new AppDef() { AppIdTuple = new AppIdTuple("m1", "a"), StartupOrder = -1, SeparationInterval = 1.0, Dependencies=new List<string>() {"b"} } },
            { "b", new AppDef() { AppIdTuple = new AppIdTuple("m1", "b"), StartupOrder = -1, SeparationInterval = 2.0, } },
            { "c", new AppDef() { AppIdTuple = new AppIdTuple("m1", "c"), StartupOrder = -1, SeparationInterval = 1.0, Dependencies=new List<string>() {"b"} } },
            { "d", new AppDef() { AppIdTuple = new AppIdTuple("m1", "d"), StartupOrder = -1, SeparationInterval = 1.0, Dependencies=new List<string>() {"a"} } },
        };

        public static Dictionary<string, ILaunchPlan> plans = new Dictionary<string, ILaunchPlan>()
        {
            { "p1", new LaunchPlan("p1", new List<AppDef>() { ads["a"], ads["b"], ads["c"], ads["d"] } ) }
        };

    }

    public class AppStateRepo
    {
        public Dictionary<AppIdTuple, AppState> appsState;
        
        public void init( List<AppDef> appDefs )
        {
            appsState = new Dictionary<AppIdTuple, AppState>();
            foreach( var ad in appDefs )
            {
                appsState[ad.AppIdTuple] = new AppState();
            }
        }

        public void makeLaunched( AppIdTuple id )
        {
            appsState[id].PlanApplied = true;
            appsState[id].Started = true;
            appsState[id].Running = true;
        }

        public void makeInitialized( AppIdTuple id )
        {
            makeLaunched(id);
            appsState[id].Initialized = true;
        }
    }

}
