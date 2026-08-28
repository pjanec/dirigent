using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Dirigent.Tests
{
	[TestClass]
	public class AgentAppControlTests
	{
		[TestMethod]
		public void StartApp_CreatesMockProcess()
		{
			var processManager = new MockProcessManager();
			var ac = new AppConfig { MachineId = "testMachine", MasterIP = "127.0.0.1", MasterPort = 51000 };

			Func<AppDef, SharedContext, Dictionary<string, string>?, Launcher> launcherFactory =
				(def, ctx, vars) => new MockLauncher(def, ctx, processManager, vars);

			using var agent = new Agent(ac, "testMachine", launcherFactory);

			var appDef = new AppDef { Id = new AppIdTuple("testMachine", "app1"), ExeFullPath = "test.exe" };

			// Use reflection to call private ProcessIncomingMessage
			var pim = typeof(Agent).GetMethod("ProcessIncomingMessage", BindingFlags.NonPublic | BindingFlags.Instance);
			Assert.IsNotNull(pim, "ProcessIncomingMessage method not found via reflection.");

			pim!.Invoke(agent, new object[] { new Net.AppDefsMessage(new List<AppDef> { appDef }, false) });

			pim!.Invoke(agent, new object[] { new Net.StartAppMessage("master", appDef.Id, null) });

			agent.Tick();

			Assert.AreEqual(1, processManager.Processes.Count, "Process count should be 1.");
			var startedProcess = processManager.Processes.First().Value;
			Assert.AreEqual("test.exe", System.IO.Path.GetFileName(startedProcess.Exe), "The correct .exe should be started.");
		}
	}
}


