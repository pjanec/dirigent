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
    public class ExitCodeInitDetectorTest
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void testDetectorSingle()
        {
            AppDef appDef = new AppDef();
            AppState appState = new AppState();

            appState.Started = true;
            appState.Running = true;
            IAppInitializedDetector d = new ExitCodeInitDetector(appDef, appState, 0, XElement.Parse("<timeout>1</timeout>"));

            Assert.AreEqual(false, d.IsInitialized, "not initialized immediately");
            appState.Running = false;
            appState.ExitCode = 0;
            Assert.AreEqual(false, d.IsInitialized, "not initialized if wrong exit code");
            appState.ExitCode = 1;
            Assert.AreEqual(true, d.IsInitialized, "initialized if correct exit code");
        }

        [Test]
        public void testDetectorMultiSingle()
        {
            AppDef appDef = new AppDef();
            AppState appState = new AppState();

            appState.Started = true;
            appState.Running = true;
            IAppInitializedDetector d = new ExitCodeInitDetector(appDef, appState, 0, XElement.Parse("<timeout>1,4</timeout>"));

            Assert.AreEqual(false, d.IsInitialized, "not initialized immediately");
            appState.Running = false;
            appState.ExitCode = 0;
            Assert.AreEqual(false, d.IsInitialized, "not initialized if wrong exit code");
            appState.ExitCode = 1;
            Assert.AreEqual(true, d.IsInitialized, "initialized if correct exit code");
            appState.ExitCode = 4;
            Assert.AreEqual(true, d.IsInitialized, "initialized if correct exit code");
        }

        [Test]
        public void testDetectorRange()
        {
            AppDef appDef = new AppDef();
            AppState appState = new AppState();

            appState.Started = true;
            appState.Running = true;
            IAppInitializedDetector d = new ExitCodeInitDetector(appDef, appState, 0, XElement.Parse("<timeout>5-6</timeout>"));

            Assert.AreEqual(false, d.IsInitialized, "not initialized immediately");
            appState.Running = false;
            appState.ExitCode = 4;
            Assert.AreEqual(false, d.IsInitialized, "not initialized if wrong exit code");
            appState.ExitCode = 5;
            Assert.AreEqual(true, d.IsInitialized, "initialized if correct exit code");
            appState.ExitCode = 6;
            Assert.AreEqual(true, d.IsInitialized, "initialized if correct exit code");
            appState.ExitCode = 7;
            Assert.AreEqual(false, d.IsInitialized, "not initialized if wrong exit code");
        }


        [Test]
        [ExpectedException(typeof(InvalidAppInitDetectorArguments))]
        public void testDetectorFailsOnInvalidParams()
        {
            AppDef appDef = new AppDef();
            AppState appState = new AppState();
            var d = new ExitCodeInitDetector(appDef, appState, 0, XElement.Parse("<timeout>abcd-not-a-double</timeout>"));
        }
    }
}
