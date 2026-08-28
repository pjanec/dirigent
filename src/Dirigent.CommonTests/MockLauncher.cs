using System;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent.Tests
{
	public class MockLauncher : Launcher
	{
		private readonly MockProcessManager _processManager;
		private MockProcess? _mockProcess;
		private readonly AppDef _appDefForMock;

		public MockLauncher(AppDef appDef, SharedContext sharedContext, MockProcessManager processManager, Dictionary<string, string>? extraVars)
			: base(appDef, sharedContext, extraVars)
		{
			_processManager = processManager;
			_appDefForMock = appDef;
		}

		public override bool Launch()
		{
			// try to adopt an already running process first (matching base Launcher behavior)
			if (_appDefForMock.AdoptIfAlreadyRunning)
			{
				if (AdoptAlreadyRunningByName())
				{
					return true;  // Adoption succeeded, don't create new process
				}
			}

			// No adoption possible, create new process
			var exePath = _appDefForMock.ExeFullPath;
			var cmd = _appDefForMock.CmdLineArgs;
			_mockProcess = _processManager.StartProcess(exePath, cmd);
			_mockProcess.IsRunning = true;
			return true;
		}

		public override void Kill(Net.KillAppFlags flags = 0)
		{
			if (_mockProcess != null)
			{
				_processManager.KillProcess(_mockProcess.PID);
				_mockProcess = null;
			}
		}

		public override bool AdoptAlreadyRunningByName()
		{
			var appPath = _appDefForMock.ExeFullPath;
			var found = _processManager.FindProcessByExeName(appPath);
			if (found != null)
			{
				_mockProcess = found;
				_mockProcess.IsRunning = true;
				return true;
			}
			return false;
		}

		public override void AdoptByPID(int PID)
		{
			if (_processManager.Processes.TryGetValue(PID, out var found))
			{
				_mockProcess = found;
				_mockProcess.IsRunning = true;
			}
			else
			{
				// no-op if not found
			}
		}

		public override bool Running => _mockProcess?.IsRunning ?? false;

		public override int PID => _mockProcess?.PID ?? -1;

		public override Process_? Process => null;
	}
}


