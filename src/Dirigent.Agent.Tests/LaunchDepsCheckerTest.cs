using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using NUnit.Framework;

using Dirigent.Common;
using Dirigent.Agent.Core;

namespace Dirigent.Agent.Tests
{
    [TestFixture]
    public class LaunchDepsCheckerTest
    {
        AppStateRepo asr = new AppStateRepo();
        Dictionary<string, AppDef> ads = PlanRepo.ads;

        
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void testTimeOutDetector()
        {
            var appDefs = new List<AppDef>()
            {
                ads["a"],
                ads["b"],
                ads["c"],
                ads["d"],
            };

            asr.init( appDefs );

            var waves = LaunchWavePlanner.build( appDefs );
            var ls = new LaunchDepsChecker("m1", asr.appsState, waves );
            
            
            List<AppDef> atl;

            // first goes "b" as it does not depends on anything else
            atl = new List<AppDef>( ls.getAppsToLaunch() );
            Assert.AreEqual( new List<AppDef>() { ads["b"] }, atl );

            // now we initialize "b" which in turn yields "a" and "c"
            asr.makeInitialized( ads["b"].AppIdTuple );

            atl = new List<AppDef>( ls.getAppsToLaunch() );
            Assert.AreEqual( new List<AppDef>() { ads["a"], ads["c"] }, atl );
            
            // we launch "a" and "c" which yields nothing (no other app to launch)
            asr.makeLaunched( ads["a"].AppIdTuple );
            asr.makeLaunched( ads["c"].AppIdTuple );

            atl = new List<AppDef>( ls.getAppsToLaunch() );
            Assert.AreEqual( new List<AppDef>(), atl );

            // now we initize "a" which in turn yields "d"
            asr.makeInitialized( ads["a"].AppIdTuple );

            atl = new List<AppDef>( ls.getAppsToLaunch() );
            Assert.AreEqual( new List<AppDef>() { ads["d"] }, atl );

        }

    }
}
