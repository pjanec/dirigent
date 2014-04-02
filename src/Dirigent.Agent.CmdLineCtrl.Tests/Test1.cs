using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NUnit.Framework;
using Moq;

using Dirigent.Common;
using Dirigent.Agent.Core;
using Dirigent.Agent.CmdLineCtrl;

namespace Dirigent.Agent.CmdLineCtrl.Tests
{
    [TestFixture]
    public class Test1
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void test1()
        {
            var ctrl = new Mock<IDirigentControl>();

            var cmd = new Commands.StartPlan(ctrl.Object);
            
            Assert.AreEqual(cmd.Name, "StartPlan");
        }

        [Test]
        [ExpectedException(typeof(UnknownCommandException))]
        public void testUnknownCommand()
        {
            var ctrlMock = new Mock<IDirigentControl>();

            var cmdRepo = new CommandRepository();
            cmdRepo.ParseAndExecute(new List<string>() { "Unknown!!!", "plan1" });
        }

        [Test]
        public void testSelectPlan()
        {
            var ctrlMock = new Mock<IDirigentControl>();
            var appIdTuple = new AppIdTuple("m1.a");
            var appDef = new AppDef() { AppIdTuple = appIdTuple };
            var plan = new LaunchPlan("plan1", new List<AppDef>() { appDef } );
            var planRepo = new List<ILaunchPlan>() { plan };
            ctrlMock.Setup(f => f.GetPlanRepo()).Returns(planRepo);
            ctrlMock.Setup(f => f.SelectPlan(plan)).Verifiable();

            var cmdRepo = new CommandRepository();
            cmdRepo.Register( new Commands.SelectPlan(ctrlMock.Object) );
            cmdRepo.ParseAndExecute(new List<string>() { "SelectPlan", "plan1" });

            ctrlMock.Verify();

            //Assert.AreEqual(");
        }

        [Test]
        public void testKillApp()
        {
            var ctrlMock = new Mock<IDirigentControl>();
            var appIdTuple = new AppIdTuple("m1.a");
            ctrlMock.Setup(f => f.KillApp(appIdTuple)).Verifiable();

            var cmdRepo = new CommandRepository();
            cmdRepo.Register(new Commands.KillApp(ctrlMock.Object));
            cmdRepo.ParseAndExecute(new List<string>() { "KillApp", "m1.a" });

            ctrlMock.Verify();

            //Assert.AreEqual(");
        }

        [Test]
        [ExpectedException(typeof(ArgumentSyntaxErrorException))]
        public void testKillApp_invalidAppId()
        {
            var ctrlMock = new Mock<IDirigentControl>();
            var appIdTuple = new AppIdTuple("justmachine.");
            ctrlMock.Setup(f => f.KillApp(appIdTuple));

            var cmdRepo = new CommandRepository();
            cmdRepo.Register(new Commands.KillApp(ctrlMock.Object));
            cmdRepo.ParseAndExecute(new List<string>() { "KillApp", "justmachine." });

            //Assert.AreEqual(");
        }

    }
}
