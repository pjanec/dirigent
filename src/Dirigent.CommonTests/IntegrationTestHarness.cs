using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Dirigent.Tests
{
	public class IntegrationTestHarness : IDisposable
	{
		public Master Master { get; private set; }
		public List<Agent> Agents { get; } = new();
		public MockProcessManager ProcessManager { get; } = new();

		private readonly int _masterPort;
		private readonly int _cliPort;
		public int MasterPort => _masterPort;

		private readonly AppConfig _masterConfig;
		private readonly SharedConfig _sharedConfig;
		private readonly CancellationTokenSource _cts = new();
		private readonly List<Task> _tickTasks = new();
		private readonly MockMasterServer _mockNetwork;

		public Func<AppDef, SharedContext, Dictionary<string, string>?, Launcher> GetMockLauncherFactory()
		{
			return (def, ctx, vars) => new MockLauncher(def, ctx, ProcessManager, vars);
		}

		public IntegrationTestHarness(SharedConfig sharedConfig)
		{
			_sharedConfig = sharedConfig;

			_masterPort = GetAvailablePort(51000);
			_cliPort = GetAvailablePort(51001);

			_masterConfig = new AppConfig
			{
				MasterPort = _masterPort,
				CliPort = _cliPort,
				SharedCfgFileName = "dummy.xml",
				IsMaster = "1",
				MachineId = "master",
				LocalIP = "0.0.0.0",
				MasterIP = "127.0.0.1"
			};

			_mockNetwork = new MockMasterServer();
		}

		public void Start()
		{
            // ensure server binds only to loopback during tests
            Environment.SetEnvironmentVariable("DIRIGENT_SERVER_LOCAL_ONLY", "1");

			string rootPath = Directory.GetCurrentDirectory();
			Master = new Master(_sharedConfig, _masterConfig, rootPath, _mockNetwork);

			foreach (var machineDef in _sharedConfig.Machines)
			{
				var agentConfig = new AppConfig
				{
					MachineId = machineDef.Id,
					MasterIP = "127.0.0.1",
					MasterPort = _masterPort
				};

				var agent = new Agent(agentConfig, machineDef.Id, GetMockLauncherFactory());
				Agents.Add(agent);
			}

			_tickTasks.Add(Task.Run(() => TickLoop(Master.Tick, _masterConfig.MasterTickPeriod, _cts.Token), _cts.Token));
			foreach (var agent in Agents)
			{
				_tickTasks.Add(Task.Run(() => TickLoop(agent.Tick, _masterConfig.TickPeriod, _cts.Token), _cts.Token));
			}
		}

		private static int GetAvailablePort(int preferred)
		{
			try
			{
				var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, preferred);
				l.Start();
				l.Stop();
				return preferred;
			}
			catch
			{
				var l = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
				l.Start();
				int p = ((System.Net.IPEndPoint)l.LocalEndpoint).Port;
				l.Stop();
				return p;
			}
		}

		public async Task TickLoop(Action tickAction, int periodMs, CancellationToken token)
		{
			while (!token.IsCancellationRequested)
			{
				try
				{
					tickAction();
					await Task.Delay(periodMs, token);
				}
				catch (TaskCanceledException) { break; }
				catch (Exception ex)
				{
					Debug.WriteLine($"TickLoop Error: {ex.Message}");
				}
			}
		}

		public async Task WaitUntil(Func<bool> condition, int timeoutMs = 5000, string? message = null)
		{
			var sw = Stopwatch.StartNew();
			while (sw.ElapsedMilliseconds < timeoutMs)
			{
				if (condition()) return;
				await Task.Delay(50);
			}
			throw new TimeoutException(message ?? "WaitUntil condition was not met in time.");
		}

		public CancellationToken GetCancellationToken() => _cts.Token;

		public void Dispose()
		{
			_cts.Cancel();
			try { Task.WhenAll(_tickTasks).Wait(2000); } catch { }

			Master?.Dispose();
			foreach (var agent in Agents) agent.Dispose();

			_cts.Dispose();

			Environment.SetEnvironmentVariable("DIRIGENT_SERVER_LOCAL_ONLY", null);
		}
	}
}


