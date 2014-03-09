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
    public class LaunchWavePlannerTest
    {
        Dictionary<string, AppDef> ads = PlanRepo.ads;

        public string getOrder(List<AppWave> waves)
        {
            return
                string.Join("|",
                    ( from w in waves
                      select string.Join(",",
                                (from a in w.apps
                                 select a.AppIdTuple.ToString()
                                ).ToArray())
                    ).ToArray() );
        }

        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void check_empty()
        {

            var apps = new List<AppDef>()
            {
            };

            var waves = LaunchWavePlanner.build(apps);

            Assert.AreEqual("", getOrder(waves));
        }

        [Test]
        public void check_nodeps_goes_first()
        {

            var appDefs = new List<AppDef>()
            {
                ads["b"] // this one has no deps
            };

            var waves = LaunchWavePlanner.build(appDefs);

            Assert.AreEqual("m1.b", getOrder(waves));
        }

        [Test]
        [ExpectedException(typeof(UnknownDependencyException))]
        public void check_unknown_depency()
        {

            var appDefs = new List<AppDef>()
            {
                ads["a"] // this one depends on "b" which is not present in the app list
            };

            var waves = LaunchWavePlanner.build(appDefs);

            Assert.AreEqual("m1.a", getOrder(waves));
        }

        [Test]
        public void check_B_AC_D()
        {

            var appDefs = new List<AppDef>()
            {
                ads["a"],
                ads["b"],
                ads["c"],
                ads["d"],
            };

            var waves = LaunchWavePlanner.build(appDefs);

            Assert.AreEqual("m1.b|m1.a,m1.c|m1.d", getOrder(waves));
        }


        [Test]
        [ExpectedException(typeof(CircularDependencyException))]
        public void check_circular()
        {

            var apps = new List<AppDef>()
            {
                new AppDef() { AppIdTuple = new AppIdTuple("m1", "a"), StartupOrder = -1, Dependencies=new List<string>() {"c"} },
                new AppDef() { AppIdTuple = new AppIdTuple("m1", "b"), StartupOrder = -1, Dependencies=new List<string>() {"d"} },
                new AppDef() { AppIdTuple = new AppIdTuple("m1", "c"), StartupOrder = -1, Dependencies=new List<string>() {"b"} },
                new AppDef() { AppIdTuple = new AppIdTuple("m1", "d"), StartupOrder = -1, Dependencies=new List<string>() {"a"} },
            };

            var waves = LaunchWavePlanner.build(apps);
        }

        [Test]
        [ExpectedException(typeof(CircularDependencyException))]
        public void check_circular2()
        {

            var apps = new List<AppDef>()
            {
                new AppDef() { AppIdTuple = new AppIdTuple("m1", "a"), StartupOrder = -1, Dependencies=new List<string>() {"a"} },
            };

            var waves = LaunchWavePlanner.build(apps);
        }
    }
}