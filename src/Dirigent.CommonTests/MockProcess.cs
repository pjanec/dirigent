using System;

namespace Dirigent.Tests
{
	public class MockProcess
	{
		public int PID { get; }
		public string Exe { get; }
		public string Args { get; }
		public bool IsRunning { get; set; } = true;
		public DateTime StartTime { get; } = DateTime.UtcNow;

		public MockProcess(string exe, string args)
		{
			PID = new Random().Next(10000, 50000);
			Exe = exe;
			Args = args;
		}

		public void Kill() => IsRunning = false;
	}
}


