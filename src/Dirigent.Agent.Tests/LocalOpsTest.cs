using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;

using Dirigent.Common;
using Dirigent.Agent.Core;

namespace Dirigent.Agent.Tests
{
    class FakeLauncher : ILauncher
    {
        bool running = false;
		bool dying = false;

        AppDef appDef;
        FakeLaunchFactory flf;

        public FakeLauncher( AppDef appDef, FakeLaunchFactory flf, string planName )
        {
            this.appDef = appDef;
            this.flf = flf;
        }

		public void Dispose()
		{
		}
        
        public void  Launch()
        {
 	        running = true;
            
            // remember
            flf.addLaunchedApp( appDef );
        }

        public void  Kill()
        {
 	        running = false;
			dying = true;
        }

		public bool AdoptAlreadyRunning()
		{
			return false;
		}

		public bool  Running
        {
            get
            {
				if (dying )
				{
					running = false;
					dying = false;
				}

                return running;
            }
        }

        public bool  Dying
        {
            get
            {
                return dying;
            }
        }

        public int ExitCode
        {
            get
            {
                return 0;
            }
        }

        public int ProcessId
        {
            get
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// Remembers all the apps launched
    /// </summary>
    class FakeLaunchFactory : ILauncherFactory
    {
        public List<AppDef> appsLaunched = new List<AppDef>();
        
        public ILauncher createLauncher(AppDef appDef, string rootForRelativePaths, string planName, string masterIP, Dictionary<string, string> internalVars )
        {
 	        return new FakeLauncher( appDef, this, planName );
        }

        public void addLaunchedApp( AppDef appDef )
        {
            appsLaunched.Add( appDef );
        }
    }
    

    [TestFixture]
    public class LocalOpsTest
    {
        AppStateRepo asr = new AppStateRepo();
        Dictionary<string, AppDef> ads = PlanRepo.ads;

        FakeLaunchFactory lf = new FakeLaunchFactory();

        IAppInitializedDetectorFactory appInitializedDetectorFactory = new AppInitializedDetectorFactory();

        public string getOrder(List<AppDef> appDefs)
        {
            var appIds = from a in appDefs select a.AppIdTuple.ToString();
            return string.Join(",", appIds.ToArray());
        }

        /// <summary>
        /// Returns a string with comma separated appIdTuples of apps whose state matches given predicate;
        /// the apps go in alphabetical order ("m10.b", "m2.a"...)
        /// </summary>
        public string getAppsWithMatchingState(LocalOperations lo, Predicate<AppState> predicate)
        {
            var appIds =
                from p in lo.GetPlanRepo()
			    from a in p.getAppDefs() 
                where predicate( lo.GetAppState(a.AppIdTuple) )
                orderby a.AppIdTuple.ToString()
                select a.AppIdTuple.ToString();
            return string.Join(",", appIds.ToArray());
        }

        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void testLaunchSequence()
        {
            var lo = new LocalOperations("m1", lf, appInitializedDetectorFactory, null, null, 0, false );
            
            lo.SelectPlan( "p1" );
            
            // start the plan
            lo.StartPlan("p1");

            var t = 0.0;

            Assert.AreEqual( 0, lf.appsLaunched.Count, "nothing started without tick" );

            lo.tick( t );

            Assert.AreEqual( "m1.b", getOrder(lf.appsLaunched), "'b' started as first app" );
            
            t += 1.0;
            lo.tick( t );

            Assert.AreEqual( "m1.b", getOrder(lf.appsLaunched), "'a' waiting, not yet starting (separ. interval of b is 2.0)" );

            t += 1.0;
            lo.tick( t );

            Assert.AreEqual( "m1.b,m1.a", getOrder(lf.appsLaunched), "'a' started" );

            t += 1.0;
            lo.tick( t );

            Assert.AreEqual( "m1.b,m1.a,m1.c", getOrder(lf.appsLaunched), "'c' started" );

            t += 1.0;
            lo.tick( t );

            Assert.AreEqual( "m1.b,m1.a,m1.c,m1.d", getOrder(lf.appsLaunched), "'d' started as last app" );

            Assert.AreEqual( "m1.a,m1.b,m1.c,m1.d", getAppsWithMatchingState(lo, st => st.Running ), "all aps running" );

        }

        [Test]
        public void testRunApp()
        {
            var lo = new LocalOperations("m1", lf, appInitializedDetectorFactory, null, null, 0, false );
            
            lo.SelectPlan( "p1" );
            
            AppState st;
            
            st = lo.GetAppState( ads["a"].AppIdTuple );
            Assert.AreEqual( st.Started, false, "not yet launched" );
            Assert.AreEqual( st.Running, false, "not yet running" );
            Assert.AreEqual( st.Killed, false, "not yet killed");
            Assert.AreEqual(st.Initialized, false, "not yet initialized");

            lo.LaunchApp( ads["a"].AppIdTuple );
            lo.tick( 10.0 );

            st = lo.GetAppState( ads["a"].AppIdTuple );
            Assert.AreEqual( st.Started, true, "launched" );
            Assert.AreEqual( st.Running, true, "running" );
            Assert.AreEqual(st.Killed, false, "not yet killed");
            Assert.AreEqual(st.Initialized, true, "initialized");

            lo.KillApp( ads["a"].AppIdTuple );
            lo.tick( 10.0 );

            st = lo.GetAppState( ads["a"].AppIdTuple );
            Assert.AreEqual( st.Started, true, "started after kill" );
            Assert.AreEqual(st.Killed, true, "killed after kill");
            Assert.AreEqual(st.Running, false, "not running after kill");
            Assert.AreEqual( st.Initialized, false, "not initialized after kill" );
        }

        public void testStopPlan()
        {
            var lo = new LocalOperations("m1", lf, appInitializedDetectorFactory, null, null, 0, false );
            
            var planName =  "p1";
            lo.SelectPlan( planName );
            lo.StartPlan( planName );
            for(int i=0; i < 10; i++ ) lo.tick(i); // give enought ticks to start all 
            Assert.AreEqual( "m1.a,m1.b,m1.c,m1.d", getAppsWithMatchingState(lo, st => st.Running ), "all aps running after Start()" );

            
            lo.KillPlan(planName);
            lo.tick(20.0);
            Assert.AreEqual( "", getAppsWithMatchingState(lo, st => st.Running ), "no app running after Stop()" );
        }
    }
}