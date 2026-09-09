using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System.Diagnostics;

namespace Dirigent.SystemTests
{
	/// <summary>
	/// What happens to the applications when their agent dies, and when it comes back.
	/// </summary>
	/// <remarks>
	/// Only a real process can show this: the agent has to actually be killed, its applications have
	/// to actually outlive it, and the status file on disk is what a restarted agent reads to find
	/// them again. Tier 1 has no processes to kill.
	/// </remarks>
	[TestClass()]
	public class RecoveryTests
	{
		[TestMethod()]
		public void AnAgentThatIsKilledAdoptsItsApplicationsWhenItComesBack()
		{
			using var world = SystemWorld.Start( "LoggingWorld" );

			world.Cli( "StartApp m2.recorder" );
			world.WaitAppState( "m2.recorder", "R" );

			// APP:<idTuple>:<flags>:<pid>:... - the pid is what proves it was adopted rather than
			// started again
			var before = world.Cli( "GetAppState m2.recorder" ).Split( ':' )[3];

			// the status file is what the recovery reads; it must exist while the agent runs
			Assert.IsTrue( Directory.EnumerateFiles( world.Files.AgentStatusFolder ).Any(),
				"the agent wrote a status file" );

			// kill the agent hosting m2 the hard way, leaving its application running
			var agent = world.Agents.Single( a => a.Machine == "m2" );
			agent.Process.Kill();
			agent.Process.WaitForExit( 10000 );

			world.WaitUntil( "the master notices m2 is gone", TimeSpan.FromSeconds( 40 ), () =>
					!world.CliList( "GetAllClientsState" ).Any( l => l.StartsWith( "CLIENT:m2:1:" ) ) );

			// the application is still running: Dirigent does not take applications down with an agent
			Assert.IsTrue( world.ApplicationProcesses().Count > 0
						|| SystemWorld.ProcessesMentioning( "Dirigent.TestApp.exe", world.Root ).Count > 0,
				"the application outlived its agent" );

			// bring the agent back and let it adopt what it finds
			var restarted = Process.Start( new ProcessStartInfo(
					SystemWorld.Tool( "Dirigent.Agent.Console", "Dirigent.Agent.exe" ) )
			{
				WorkingDirectory = world.Root,
				UseShellExecute = true,
				WindowStyle = ProcessWindowStyle.Minimized,
				ArgumentList =
				{
					"--machineId", "m2",
					"--mode", "daemon",
					"--isMaster", "0",
					"--masterIp", "127.0.0.1",
					"--masterPort", world.MasterPort.ToString(),
					"--sharedConfigFile", world.Files.SharedConfig,
					"--localConfigFile", world.Files.LocalConfig,
					"--agentStatusFolder", world.Files.AgentStatusFolder,
					"--downloadFolder", world.Files.DownloadFolder,
					"--logFile", Path.Combine( world.Files.LogFolder, "m2-restarted.log" ),
					"--rootForRelativePaths", world.Root,
				}
			} )!;

			// so that the teardown kills it and finds its children
			world.Adopt( "m2", restarted );

			world.WaitAppState( "m2.recorder", "R", TimeSpan.FromSeconds( 40 ) );

			var after = world.Cli( "GetAppState m2.recorder" ).Split( ':' )[3];

			Assert.AreEqual( before, after,
				"the adopted application is the same process, not a fresh one" );
		}
	}
}
