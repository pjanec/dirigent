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
    public class AppWaveListBuilderTest
    {
        List<AppDef> appsEmpty;
        List<AppDef> appsA;
        List<AppDef> appsAB;

        public string getOrder(List<AppWave> waves)
        {
            string res = "";
            foreach( var w in waves )
            {
                foreach (var a in w.apps)
                {
                    res += a.AppId + ",";
                }
                res = res.TrimEnd(',');
                res += "|";
            }
            res = res.TrimEnd('|');
            return res;
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

            var waves = AppWaveListBuilder.build(apps);

            Assert.AreEqual("", getOrder(waves));
        }

        [Test]
        public void check_A()
        {

            var apps = new List<AppDef>()
            {
                new AppDef() { AppId = "a", StartupOrder = -1, MachineId = "m1" },
            };

            var waves = AppWaveListBuilder.build(apps);

            Assert.AreEqual("a", getOrder(waves));
        }

        [Test]
        [ExpectedException(typeof(UnknownDependencyException))]
        public void check_unknown_depency()
        {

            var apps = new List<AppDef>()
            {
                new AppDef() { AppId = "a", StartupOrder = -1, MachineId = "m1", Dependencies=new List<string>() {"b"} },
            };

            var waves = AppWaveListBuilder.build(apps);

            Assert.AreEqual("a", getOrder(waves));
        }

        [Test]
        public void check_B_AC_D()
        {

            var apps = new List<AppDef>()
            {
                new AppDef() { AppId = "a", StartupOrder = -1, MachineId = "m1", Dependencies=new List<string>() {"b"} },
                new AppDef() { AppId = "b", StartupOrder = -1, MachineId = "m1" },
                new AppDef() { AppId = "c", StartupOrder = -1, MachineId = "m1", Dependencies=new List<string>() {"b"} },
                new AppDef() { AppId = "d", StartupOrder = -1, MachineId = "m1", Dependencies=new List<string>() {"a"} },
            };

            var waves = AppWaveListBuilder.build(apps);

            Assert.AreEqual("b|a,c|d", getOrder(waves));
        }


        [Test]
        [ExpectedException(typeof(CircularDependencyException))]
        public void check_circular()
        {

            var apps = new List<AppDef>()
            {
                new AppDef() { AppId = "a", StartupOrder = -1, MachineId = "m1", Dependencies=new List<string>() {"c"} },
                new AppDef() { AppId = "b", StartupOrder = -1, MachineId = "m1", Dependencies=new List<string>() {"d"} },
                new AppDef() { AppId = "c", StartupOrder = -1, MachineId = "m1", Dependencies=new List<string>() {"b"} },
                new AppDef() { AppId = "d", StartupOrder = -1, MachineId = "m1", Dependencies=new List<string>() {"a"} },
            };

            var waves = AppWaveListBuilder.build(apps);
        }

        [Test]
        [ExpectedException(typeof(CircularDependencyException))]
        public void check_circular2()
        {

            var apps = new List<AppDef>()
            {
                new AppDef() { AppId = "a", StartupOrder = -1, MachineId = "m1", Dependencies=new List<string>() {"a"} },
            };

            var waves = AppWaveListBuilder.build(apps);
        }
    }
}