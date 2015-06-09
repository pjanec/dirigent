using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;

using NUnit.Framework;

using Dirigent.Common;
using Dirigent.Agent.Core;

namespace Dirigent.Agent.Tests
{
    [TestFixture]
    public class InitDetectorTest
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void testTimeOutDetector()
        {
            AppDef appDef = new AppDef();
            AppState appState = new AppState();

            var initialTicks = DateTime.UtcNow.Ticks;
            IAppInitializedDetector d = new TimeOutInitDetector(appDef, appState, 0, XElement.Parse("<timeout>0.1</timeout>"));

            Assert.AreEqual(false, d.IsInitialized, "not initialized immediately");
            Thread.Sleep(100);
            Assert.AreEqual(true, d.IsInitialized, "initialized after time out");
        }

        [Test]
        [ExpectedException(typeof(InvalidAppInitDetectorArguments))]
        public void testTimeOutDetectorFailsForInvalidParams()
        {
            AppDef appDef = new AppDef();
            AppState appState = new AppState();
            var d = new TimeOutInitDetector(appDef, appState, 0, XElement.Parse("<timeout>abcd-not-a-double</timeout>"));
        }
    }
}
