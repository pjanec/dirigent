using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NUnit.Framework;

using Dirigent.Common;
using Dirigent.Agent.Core;

namespace Dirigent.Agent.Tests
{
    [TestFixture]
    class AppStateMonitorTest
    {
        //[Test]
        //public void check_compilable()
        //{
        //    var appDefs = new List<AppDef>()
        //    {
        //        new AppDef() { AppId = "a", StartupOrder = -1, MachineId = "m1" },
        //    };

        //    AppStateMonitor asm = new AppStateMonitor(appDefs, "m1");
            
        //    // rict ze aplikace byla spustena
        //    asm.setAppLaunched("a", 12345);
            
        //    // vratit stav aplikace podle appId
        //    AppState apps = asm.getAppState( "a" );

        //    // aktualizovat stav aplikaci
        //    asm.evaluate( "a" );
        //}

        [Test]
        [ExpectedException(typeof(UnknownAppIdException))]
        public void check_empty()
        {
            var appDefs = new List<AppDef>()
            {
                new AppDef() { AppId = "a", StartupOrder = -1, MachineId = "m1" },
            };

            AppStateMonitor asm = new AppStateMonitor(appDefs, "m1");
            
            // vratit stav aplikace podle appId - musi vyhodit vyjimku
            AppState apps = asm.getAppState( "b" );
        }

        [Test]
        public void check_created_initial_state()
        {
            var appDefs = new List<AppDef>()
            {
                new AppDef() { AppId = "a", StartupOrder = -1, MachineId = "m1" },
            };

            AppStateMonitor asm = new AppStateMonitor(appDefs, "m1");
            
            // vratit stav aplikace podle appId
            AppState aps = asm.getAppState( "a" );

            Assert.AreEqual( aps.Initialized, false );
            Assert.AreEqual( aps.Running, false );
            Assert.AreEqual( aps.WasLaunched, false );
        }

        [Test]
        public void check_set_launched()
        {
            var appDefs = new List<AppDef>()
            {
                new AppDef() { AppId = "a", StartupOrder = -1, MachineId = "m1" },
            };

            AppStateMonitor asm = new AppStateMonitor(appDefs, "m1");
            
            // nastavit ze byla spustena (ale protoze neprobehlo evaluate, nevime, zda bezi)
            asm.setAppLaunched( "a", 12345 );

            // vratit stav aplikace podle appId
            AppState aps = asm.getAppState( "a" );

            Assert.AreEqual( aps.Initialized, false );
            Assert.AreEqual( aps.Running, false );
            Assert.AreEqual( aps.WasLaunched, true );
        }
    }
}
