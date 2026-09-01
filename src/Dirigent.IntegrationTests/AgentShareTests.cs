using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// What an agent knows about the machines of the system, and where it gets it from.
	/// </summary>
	/// <remarks>
	/// Only the master reads `SharedConfig.xml`, so `&lt;Machine&gt;` and its `&lt;Share&gt;` elements
	/// exist nowhere else until the master hands them out. An agent needs them all the same: a node
	/// belonging to another machine is resolved by *that* machine's agent - `FileRegistry.ResolveAsync`
	/// sends a `ResolveVfsPath` script there - and turning its local path into a UNC one the other
	/// machines can reach needs the share table.
	///
	/// The regression these pin: the machine definitions used to be sent to GUI clients only, so an
	/// agent's `MakeUNC` failed with "Machine X not found" even though the share had been declared all
	/// along. A download requested from a GUI that does not sit on the master's machine then lost
	/// every other machine's files, complaining that no share covered the download folder.
	///
	/// The failure is on the **resolution** side, which is what these tests exercise, by asking an
	/// agent to resolve something directly. The transfer that follows cannot be covered here: every
	/// machine of a tier-1 bed is this one, and a declared share is not a real Windows share, so an
	/// actual `\\host\share\...` access needs the two real machines of tier 3.
	/// </remarks>
	[TestClass()]
	public class AgentShareTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 30 );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		/// <summary>The root of the drive the run's temporary files are on, e.g. "C:\".</summary>
		static string SystemDriveRoot => Path.GetPathRoot( Path.GetTempPath() )!;

		/// <summary>
		/// Two machines, each declaring a share of the whole system drive - which is what covers a
		/// download folder, wherever the run's temporary files happen to be.
		/// </summary>
		static Scenario SharedWorld()
			=> Scenario.TwoMachines()
				.Share( "m1", "C", SystemDriveRoot )
				.Share( "m2", "C", SystemDriveRoot );

		static Task<TestBed.TestBed> StartBed()
			=> TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = SharedWorld() } );

		/// <summary>
		/// Resolves a node on the given machine's agent, the way the master asks a remote machine to
		/// resolve what belongs to it.
		/// </summary>
		/// <remarks>
		/// Sent to the agent explicitly, because in a tier-1 bed the master would do it itself: every
		/// machine answers on loopback, so `IsLocalMachine` is true for all of them and the remote
		/// dispatch never happens. That is precisely why the harness did not catch this.
		/// </remarks>
		static async Task<VfsNodeDef?> ResolveOnAgentAsync( TestBed.TestBed bed, string machineName,
				VfsNodeDef node )
		{
			var result = await bed.Operator.RunScriptAsync<
					Scripts.BuiltIn.ResolveVfsPath.TArgs, Scripts.BuiltIn.ResolveVfsPath.TResult>(
				Scripts.BuiltIn.ResolveVfsPath._Name,
				new Scripts.BuiltIn.ResolveVfsPath.TArgs() { VfsNode = node, ForceUNC = true },
				hostId: bed.MachineIds[machineName],
				timeout: Timeout );

			return result?.VfsNode;
		}

		[TestMethod()]
		public async Task AnAgentCanTurnItsOwnDownloadFolderIntoAUncPath()
		{
			// The exact call that failed: DownloadZipped asks the requestor's machine for its download
			// folder as a UNC path, so that the other machines have somewhere to upload their parts.
			using var bed = await StartBed();

			var resolved = await ResolveOnAgentAsync( bed, "m1",
				new FolderDef() { Path = "%DOWNLOADS%", MachineId = bed.MachineIds["m1"] } );

			Assert.IsNotNull( resolved, "the agent resolved nothing" );
			StringAssert.StartsWith( resolved!.Path ?? "", @"\\127.0.0.1\C\",
				$"the agent has to know its machine's shares to build this; got '{resolved.Path}'" );
			StringAssert.Contains( resolved.Path ?? "", "downloads",
				$"and it is still the download folder; got '{resolved.Path}'" );
		}

		[TestMethod()]
		public async Task EveryAgentGetsTheTableNotOnlyTheOneTheMasterRunsOn()
		{
			// The master happens to share a machine with one agent, which is the one that never had
			// the problem - what broke was every other machine.
			using var bed = await StartBed();

			foreach( var machine in new[] { "m1", "m2" } )
			{
				var resolved = await ResolveOnAgentAsync( bed, machine,
					new FolderDef()
					{
						Path = Path.Combine( SystemDriveRoot, "Logs" ),
						MachineId = bed.MachineIds[machine],
					} );

				Assert.IsNotNull( resolved, $"{machine} resolved nothing" );
				StringAssert.StartsWith( resolved!.Path ?? "", @"\\127.0.0.1\C\Logs",
					$"{machine} could not build a UNC path; got '{resolved.Path}'" );
			}
		}

		[TestMethod()]
		public async Task AnAgentPrefersTheMostSpecificShareOfTheTableItWasGiven()
		{
			// Proves the agent got the table rather than a lucky single entry: with two shares
			// covering the path it has to make the same choice the master would - the more specific
			// one, whose permissions were presumably set up for exactly that folder.
			using var bed = await StartBed();

			var resolved = await ResolveOnAgentAsync( bed, "m1",
				new FolderDef()
				{
					Path = Path.Combine( SystemDriveRoot, "Logs", "app" ),
					MachineId = bed.MachineIds["m1"],
				} );

			Assert.IsNotNull( resolved );
			StringAssert.StartsWith( resolved!.Path ?? "", @"\\127.0.0.1\C\Logs\app",
				$"one share only, so far; got '{resolved.Path}'" );

			// now declare a share dedicated to that folder
			await bed.ReloadSharedConfigAsync(
				SharedWorld().Share( "m1", "Logs", Path.Combine( SystemDriveRoot, "Logs" ) ) );

			await bed.WaitUntilAsync(
				async () =>
				{
					var again = await ResolveOnAgentAsync( bed, "m1",
						new FolderDef()
						{
							Path = Path.Combine( SystemDriveRoot, "Logs", "app" ),
							MachineId = bed.MachineIds["m1"],
						} );

					return ( again?.Path ?? "" ).StartsWith( @"\\127.0.0.1\Logs\app", StringComparison.OrdinalIgnoreCase );
				},
				Timeout,
				"the agent picks up the share the reload added, and prefers it - so the table reaches "
				+ "agents on a reload too, without anybody restarting anything" );
		}
	}
}
