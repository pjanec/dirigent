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
    public class LaunchSequencerTest
    {
        Dictionary<string, AppDef> ads = PlanRepo.ads;
        
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void testSeparationIntervals()
        {
            var appDefs = new List<AppDef>()
            {
                ads["a"],
                ads["b"],
                ads["c"],
            };

            var ls = new LaunchSequencer();
            Assert.AreEqual( true, ls.IsEmpty(), "empty after creation"  );

            ls.AddApps( appDefs );

            Assert.AreEqual( false, ls.IsEmpty(), "non-empty after adding some apps" );
            
            // first app should be returned immediately
            double t = 0.0;
            AppDef ad;
            ad = ls.GetNext( t );
            Assert.AreEqual( appDefs[0], ad, "first app should be returned immediately" );
            Assert.AreEqual( false, ls.IsEmpty(), "non-empty after first app returned"  );
            
            ad = ls.GetNext( t );
            Assert.AreEqual( null, ad, "second app should wait" );

            t += appDefs[0].SeparationInterval;

            ad = ls.GetNext( t );
            Assert.AreEqual( appDefs[1], ad, "second app after the first app's separ. interval" );
            Assert.AreEqual( false, ls.IsEmpty(), "non-empty after second app returned"  );

            ad = ls.GetNext( t );
            Assert.AreEqual( null, ad, "third app should wait" );

            t += appDefs[1].SeparationInterval;

            ad = ls.GetNext( t );
            Assert.AreEqual( appDefs[2], ad, "third app after the second app's separ. interval" );
            Assert.AreEqual( true, ls.IsEmpty(), "empty after last app returned" );


            // add some next app
            ls.AddApps( new List<AppDef>() { ads["a"] } );
            Assert.AreEqual( false, ls.IsEmpty(), "non-empty after refilled"  );
            
            t += appDefs[2].SeparationInterval;

            ad = ls.GetNext( t );
            Assert.AreEqual( ads["a"], ad, "refill app after the last app's separ. interval" );
            Assert.AreEqual( true, ls.IsEmpty(), "empty after last app returned" );

        }

    }
}
