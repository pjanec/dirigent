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
	/// Wake-on-LAN as the master sees it: MACs learned from the agents' own reports, and the
	/// WakeUp command.
	/// </summary>
	/// <remarks>
	/// The magic packets really go out on this machine's network, addressed to a locally administered
	/// MAC that no card owns - nothing wakes up. What is asserted is the master's decision, not the
	/// packet arriving.
	/// </remarks>
	[TestClass()]
	public class WakeUpTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 30 );

		const string ConfiguredMac = "02-00-00-00-00-01";

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		[TestMethod()]
		public async Task MacsReportedByTheAgentAreCachedOnDisk()
		{
			if( Tools.LocalNics.Count == 0 )
				Assert.Inconclusive( "this machine has no network adapter with a MAC address to report" );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = Scenario.OneMachine() } );

			var file = Path.Combine( bed.AgentStatusFolder, MachineMacCache.FileName );
			await bed.WaitUntilAsync( () => Task.FromResult( File.Exists( file ) ), Timeout, "the master writes the MAC cache" );

			// what a master restarted now would know
			var cache = new MachineMacCache( bed.AgentStatusFolder );
			CollectionAssert.AreEqual( Tools.LocalNics, cache.Get( bed.Machine( "m1" ) ) );
		}

		[TestMethod()]
		public async Task WakeUpOfAMachineWithAConfiguredMacIsAcknowledged()
		{
			// a machine that is declared but has never connected: the only MAC the master can have
			// is the configured one, so an ACK proves that it was used
			var scenario = Scenario.OneMachine()
				.RawXml( $"<Machine Name=\"ghost\" MAC=\"{ConfiguredMac}\" />" );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );
			using var cli = new CliSession( bed.CliPort );

			Assert.AreEqual( "ACK", cli.Request( "WakeUp ghost" ) );
		}

		[TestMethod()]
		public async Task WakeUpOfAMachineWithNoKnownMacIsAnError()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = Scenario.OneMachine() } );

			var lines = await bed.SendCliCommandAsync( "WakeUp neverHeardOf", Timeout );

			Assert.AreEqual( 1, lines.Count, string.Join( " | ", lines ) );
			StringAssert.StartsWith( lines[0], "ERROR", lines[0] );
			StringAssert.Contains( lines[0], "No MAC address known for machine 'neverHeardOf'" );
		}
	}
}
