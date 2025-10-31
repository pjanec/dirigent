using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace Dirigent.Tests
{
	[TestClass]
	public class MockProcessManagerTests
	{
		[TestMethod]
		public void StartProcess_AddsProcessToDictionaryAndFindsIt()
		{
			var manager = new MockProcessManager();

			var process = manager.StartProcess("test.exe", "-a");

			Assert.AreEqual(1, manager.Processes.Count, "Process count should be 1.");
			Assert.IsTrue(manager.Processes.ContainsKey(process.PID), "Process dictionary should contain new PID.");
			Assert.IsTrue(manager.Processes[process.PID].IsRunning, "Process should be running.");

			var found = manager.FindProcessByExeName("test.exe");
			Assert.IsNotNull(found, "FindProcessByExeName should find the process.");
			Assert.AreEqual(process.PID, found!.PID, "Found process PID should match.");
		}

		[TestMethod]
		public void KillProcess_SetsIsRunningToFalse()
		{
			var manager = new MockProcessManager();
			var process = manager.StartProcess("test.exe", "-a");
			Assert.IsTrue(process.IsRunning, "Process should be running initially.");

			manager.KillProcess(process.PID);

			Assert.IsFalse(process.IsRunning, "Process IsRunning should be false after kill.");

			var found = manager.FindProcessByExeName("test.exe");
			Assert.IsNull(found, "FindProcessByExeName should not find a killed process.");
		}
	}
}


