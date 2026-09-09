using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;

namespace Dirigent.SystemTests
{
	/// <summary>
	/// That the hosting model works when it is real processes: a master, an agent per machine, and
	/// applications starting on the machine they belong to.
	/// </summary>
	/// <remarks>
	/// Tier 1 runs all of this in one process, faster and in more depth. What it cannot show is that
	/// the shipped executables start at all, find their configuration and connect to each other -
	/// which is what fails when a deployment is wrong rather than when the code is.
	/// </remarks>
	[TestClass()]
	public class HostingTests
	{
		[TestMethod()]
		public void TheWorldComesUpWithItsMasterAgentsAndApplications()
		{
			using var world = SystemWorld.Start( "LoggingWorld" );

			var clients = world.CliList( "GetAllClientsState" );
			foreach( var machine in new[] { "m1", "m2" } )
			{
				Assert.IsTrue( clients.Any( l => l.StartsWith( $"CLIENT:{machine}:1:" ) ),
					$"{machine} is connected: {string.Join( " | ", clients )}" );
			}

			var apps = world.CliList( "GetAllAppsState" );
			foreach( var app in new[] { "m1.camera", "m1.tracker", "m2.recorder" } )
			{
				Assert.IsTrue( apps.Any( l => l.StartsWith( $"APP:{app}:" ) ),
					$"{app} reached the master: {string.Join( " | ", apps )}" );
			}
		}

		[TestMethod()]
		public void AnApplicationStartsAndStopsOnTheMachineItBelongsTo()
		{
			using var world = SystemWorld.Start( "LoggingWorld" );

			world.Cli( "StartApp m2.recorder" );
			world.WaitAppState( "m2.recorder", "R" );

			// the other machine's applications were not touched. The flags are field 2 of
			// APP:<idTuple>:<flags>:... - matching the whole line would also hit the "r" in "camera"
			var running = world.CliList( "GetAllAppsState" )
					.Where( l => l.StartsWith( "APP:m1." ) )
					.Where( l => l.Split( ':' ).Length > 2 && l.Split( ':' )[2].Contains( 'R' ) )
					.ToList();

			Assert.AreEqual( 0, running.Count,
				$"no m1 application started: {string.Join( " | ", running )}" );

			world.Cli( "KillApp m2.recorder" );

			world.WaitUntil( "m2.recorder stops running", SystemWorld.AppTimeout,
					() => !world.AppFlags( "m2.recorder" ).Contains( 'R' ) );
		}
	}
}
