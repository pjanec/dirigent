using System;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent.Tests
{
	public class MockProcessManager
	{
		public readonly Dictionary<int, MockProcess> Processes = new();

		public MockProcess StartProcess(string exe, string args)
		{
			var process = new MockProcess(exe, args);
			Processes[process.PID] = process;
			return process;
		}

		public MockProcess? FindProcessByExeName(string exePath)
		{
			var exeName = System.IO.Path.GetFileName(exePath);
			return Processes.Values.FirstOrDefault(p =>
				p.IsRunning &&
				System.IO.Path.GetFileName(p.Exe).Equals(exeName, StringComparison.OrdinalIgnoreCase)
			);
		}

		public void KillProcess(int pid)
		{
			if (Processes.TryGetValue(pid, out var process))
			{
				process.Kill();
			}
		}
	}
}


