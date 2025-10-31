using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Dirigent.Tests
{
    [TestClass]
    public class DeterministicScenarioTests
    {
        private MockProcessManager _processManager = null!;
        private Master _master = null!;
        private MockMasterServer _mockNetwork = null!;

        private AppDef? _app1Def;
        private AppDef? _app2Def;
        private PlanDef? _planDef;

        private MethodInfo? _processIncomingMessageMethod;

        [TestInitialize]
        public void Setup()
        {
            _processManager = new MockProcessManager();

            _app1Def = new AppDef { Id = new AppIdTuple("slave", "app1"), ExeFullPath = "app1_dummy.exe", AdoptIfAlreadyRunning = true };
            _app2Def = new AppDef { Id = new AppIdTuple("slave", "app2"), ExeFullPath = "app2_dummy.exe", AdoptIfAlreadyRunning = true };
            _planDef = new PlanDef { Name = "p1", AppDefs = new List<AppDef> { _app1Def, _app2Def } };

            var sharedConfig = new SharedConfig();
            sharedConfig.Plans.Add(_planDef);
            sharedConfig.Machines.Add(new MachineDef { Id = "slave" });

            _mockNetwork = new MockMasterServer();
            var masterConfig = new AppConfig
            {
                MasterPort = 51000,
                CliPort = 51001,
                SharedCfgFileName = "dummy.xml",
                IsMaster = "1",
                MachineId = "master",
                LocalIP = "0.0.0.0",
                MasterIP = "127.0.0.1"
            };

            _master = new Master(sharedConfig, masterConfig, Directory.GetCurrentDirectory(), _mockNetwork);

            _processIncomingMessageMethod = typeof(Agent).GetMethod("ProcessIncomingMessage", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(_processIncomingMessageMethod,
                "Critical test infrastructure failure: Could not find private method 'ProcessIncomingMessage' on Agent class via reflection. This is required for deterministic message injection.");
        }

        [TestCleanup]
        public void Cleanup()
        {
            _master?.Dispose();
        }

        public Func<AppDef, SharedContext, Dictionary<string, string>?, Launcher> GetMockLauncherFactory()
        {
            return (def, ctx, vars) => new MockLauncher(def, ctx, _processManager!, vars);
        }

        [TestMethod]
        public void DeterministicFullScenarioTest()
        {
            Agent? slave_v1 = null;
            Agent? slave_v2 = null;

            try
            {
                var agentConfig = new AppConfig { MachineId = "slave", MasterIP = "127.0.0.1", MasterPort = 51000 };
                slave_v1 = new Agent(agentConfig, "slave", GetMockLauncherFactory());

                var ident1 = new Dirigent.Net.ClientIdent("slave", Dirigent.Net.EMsgRecipCateg.Agent);
                _master.ProcessIncomingMessageAndHandleExceptions(ident1);
                _mockNetwork.BufferMessageReceived(ident1);

                TickMaster();
                TickMaster();
                TickMaster();

                var appDefsMsg = _mockNetwork.SentMessages.OfType<Dirigent.Net.AppDefsMessage>().First();
                Assert.AreEqual(2, appDefsMsg.AppDefs!.Count, "Master did not send the correct number of AppDefs.");

                _processIncomingMessageMethod!.Invoke(slave_v1, new object[] { appDefsMsg });
                _mockNetwork.ClearSentMessages();

                _master.StartPlan("test", "p1", null);

                TickMaster();
                TickMaster();

                var startAppMsgs = _mockNetwork.SentMessages.OfType<Dirigent.Net.StartAppMessage>().ToList();
                Assert.AreEqual(2, startAppMsgs.Count, "Master should have generated two StartApp messages.");
                Assert.IsTrue(startAppMsgs.Any(m => m.Id.AppId == "app1"));
                Assert.IsTrue(startAppMsgs.Any(m => m.Id.AppId == "app2"));

                foreach (var msg in startAppMsgs)
                {
                    _processIncomingMessageMethod.Invoke(slave_v1, new object[] { msg });
                }

                slave_v1.Tick();

                Assert.AreEqual(2, _processManager.Processes.Count, "MockProcessManager should have two running processes.");
                var app1Proc = _processManager.Processes.Values.First(p => p.Exe.Contains("app1_dummy.exe"));
                var app2Proc = _processManager.Processes.Values.First(p => p.Exe.Contains("app2_dummy.exe"));
                var app1_PID_v1 = app1Proc.PID;
                var app2_PID_v1 = app2Proc.PID;
                Assert.IsTrue(app1_PID_v1 > 0, "app1 PID should be valid");
                Assert.IsTrue(app2_PID_v1 > 0, "app2 PID should be valid");

                _processManager.KillProcess(app2_PID_v1);
                Assert.IsFalse(app2Proc.IsRunning, "app2 process should be marked as not running.");
                Assert.IsTrue(app1Proc.IsRunning, "app1 process should still be running.");

                slave_v1.Tick();
                var appStateReport = new Dirigent.Net.AppsStateMessage(slave_v1.GetAllAppsState().ToDictionary(kvp => kvp.Key, kvp => kvp.Value), DateTime.UtcNow);

                _master.ProcessIncomingMessageAndHandleExceptions(appStateReport);
                TickMaster();

                var masterApp2State = _master.GetAppState(_app2Def!.Id);
                Assert.IsNotNull(masterApp2State);
                Assert.IsFalse(masterApp2State!.Running, "Master did not process app2 crash report.");

                slave_v1.Dispose();
                slave_v1 = null;

                _mockNetwork.SimulateDisconnect("slave");

                TickMaster();
                var slaveClientState = _master.GetClientState("slave");
                Assert.IsNotNull(slaveClientState);
                Assert.IsFalse(slaveClientState!.Connected, "Master did not detect agent disconnect.");
                _mockNetwork.ClearSentMessages();

                slave_v2 = new Agent(agentConfig, "slave", GetMockLauncherFactory());

                var ident2 = new Dirigent.Net.ClientIdent("slave", Dirigent.Net.EMsgRecipCateg.Agent);
                _master.ProcessIncomingMessageAndHandleExceptions(ident2);
                _mockNetwork.BufferMessageReceived(ident2);

                TickMaster();

                var appDefsMsg_v2 = _mockNetwork.SentMessages.OfType<Dirigent.Net.AppDefsMessage>().LastOrDefault();
                Assert.IsNotNull(appDefsMsg_v2, "Master did not send AppDefsMessage to new agent.");
                _processIncomingMessageMethod.Invoke(slave_v2, new object[] { appDefsMsg_v2! });

                var startAppMsg_app1 = new Dirigent.Net.StartAppMessage("test", _app1Def!.Id, "p1", 0, null);
                _processIncomingMessageMethod.Invoke(slave_v2, new object[] { startAppMsg_app1 });

                slave_v2.Tick();

                var app1State_v2 = slave_v2.GetAppState(_app1Def!.Id);
                var app2State_v2 = slave_v2.GetAppState(_app2Def!.Id);

                Assert.IsNotNull(app1State_v2, "slave_v2 has no state for app1.");
                Assert.IsTrue(app1State_v2!.Running, "slave_v2 did not adopt running process app1.");
                Assert.IsTrue(app1State_v2.PID > 0, "slave_v2 should report a valid PID for app1.");

                Assert.IsNotNull(app2State_v2, "slave_v2 has no state for app2.");
                Assert.IsFalse(app2State_v2!.Running, "slave_v2 should show app2 as not running.");
                Assert.AreEqual(-1, app2State_v2.PID, "slave_v2 should show app2 with no PID.");

                // === ACT 3: Plan Restart and Selective Launch ===

                slave_v2.Tick();
                var appStateReport_v2 = new Dirigent.Net.AppsStateMessage(
                    slave_v2.GetAllAppsState().ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    DateTime.UtcNow
                );

                _master.ProcessIncomingMessageAndHandleExceptions(appStateReport_v2);

                TickMaster();

                var masterApp1State = _master.GetAppState(_app1Def!.Id);
                var masterApp2State_postReport = _master.GetAppState(_app2Def!.Id);
                Assert.IsNotNull(masterApp1State, "Master has no state for app1");
                Assert.IsTrue(masterApp1State!.Running, "Master should know app1 is running after state report.");
                Assert.IsNotNull(masterApp2State_postReport, "Master has no state for app2");
                Assert.IsFalse(masterApp2State_postReport!.Running, "Master should know app2 is stopped after state report.");

                _mockNetwork.ClearSentMessages();

                // Ensure plan restarts deterministically: stop then start with no new vars
                _master.StopPlan("test", "p1");
                TickMaster();

                _master.StartPlan("test", "p1", null);

                // Drive enough ticks for planner to run and messages to be sent
                TickMaster();
                TickMaster();

                var finalStartAppMsgs = _mockNetwork.SentMessages.OfType<Dirigent.Net.StartAppMessage>().ToList();
                Assert.AreEqual(2, finalStartAppMsgs.Count, "Master should send StartApp for ALL apps in the plan.");

                var startApp1Msg = finalStartAppMsgs.FirstOrDefault(m => m.Id.AppId == "app1");
                var startApp2Msg = finalStartAppMsgs.FirstOrDefault(m => m.Id.AppId == "app2");

                Assert.IsNotNull(startApp1Msg, "Master should have sent a StartApp message for app1.");
                Assert.IsNotNull(startApp2Msg, "Master should have sent a StartApp message for app2.");
                Assert.IsNull(startApp1Msg!.Vars, "StartApp(app1) should have null vars.");
                Assert.IsNull(startApp2Msg!.Vars, "StartApp(app2) should have null vars.");

                _processIncomingMessageMethod.Invoke(slave_v2, new object[] { startApp1Msg });
                slave_v2.Tick();

                _processIncomingMessageMethod.Invoke(slave_v2, new object[] { startApp2Msg });
                slave_v2.Tick();

                var runningProcs = _processManager.Processes.Values.Where(p => p.IsRunning).ToList();
                Assert.AreEqual(2, runningProcs.Count, "Both apps should be running in the process manager.");

                var app1Proc_final = runningProcs.FirstOrDefault(p => p.Exe.Contains("app1_dummy.exe"));
                var app2Proc_v2 = runningProcs.FirstOrDefault(p => p.Exe.Contains("app2_dummy.exe"));
                Assert.IsNotNull(app1Proc_final, "app1 was not found running at the end.");
                Assert.IsNotNull(app2Proc_v2, "app2 was not found running at the end.");

                Assert.AreEqual(app1_PID_v1, app1Proc_final.PID, "app1 PID should still be the original (adopted).");
                Assert.AreNotEqual(app2_PID_v1, app2Proc_v2.PID, "app2 PID should be new (relaunched).");
                Assert.IsTrue(app2Proc_v2.PID > 0, "app2 should have a valid new PID.");
            }
            finally
            {
                slave_v1?.Dispose();
                slave_v2?.Dispose();
            }
        }

        private void TickMaster()
        {
            while (_mockNetwork.TryDequeueIncoming(out var msg))
            {
                _master.ProcessIncomingMessageAndHandleExceptions(msg);
            }

            _master.Tick();
        }
    }

    public class MockMasterServer : IMasterServer
    {
        public List<Dirigent.Net.Message> SentMessages { get; } = new List<Dirigent.Net.Message>();
        private readonly ConcurrentQueue<Dirigent.Net.Message> _incoming = new ConcurrentQueue<Dirigent.Net.Message>();
        private readonly ConcurrentDictionary<string, Dirigent.Net.ClientIdent> _clients = new();

        public IEnumerable<Dirigent.Net.ClientIdent> Clients => _clients.Values;
        public bool IsDisposed => false;

        public void Dispose() { }

        public void SendToSingle(Dirigent.Net.Message msg, string clientName) => SentMessages.Add(msg);
        public void SendToAllSubscribed(Dirigent.Net.Message msg, Dirigent.Net.EMsgRecipCateg msgCategoryMask) => SentMessages.Add(msg);
        public void BufferMessageReceived(Dirigent.Net.Message msg)
        {
            _incoming.Enqueue(msg);
            if (msg is Dirigent.Net.ClientIdent ci)
            {
                _clients[ci.Name] = ci;
            }
        }
        public void ClearSentMessages() => SentMessages.Clear();
        public bool TryDequeueIncoming(out Dirigent.Net.Message msg) => _incoming.TryDequeue(out msg!);
        public void Tick(Action<Dirigent.Net.Message>? act = null) { }
        public System.Net.Sockets.Socket? GetClientSocket(string clientName) => null;

        public void SimulateDisconnect(string clientName)
        {
            _clients.TryRemove(clientName, out _);
        }
    }
}


