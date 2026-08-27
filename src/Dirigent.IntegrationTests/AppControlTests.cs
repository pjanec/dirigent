using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.TestBed;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// The first tier-1 tests: a master and two agents in this process, real TCP between them,
	/// driven by an operator that is a GUI in everything but pixels.
	/// </summary>
	[TestClass()]
	public class AppControlTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 20 );

		/// <summary>
		/// Two machines, one idle application on each. {m1}, {m2} and {testapp} are substituted
		/// by the test bed with the ids and paths that this run actually uses.
		/// </summary>
		const string Config = @"<Shared>
	<Machine Name=""{m1}"" IP=""127.0.0.1""/>
	<Machine Name=""{m2}"" IP=""127.0.0.1""/>

	<App AppIdTuple=""{m1}.idler""
		 ExeFullPath=""{testapp}""
		 CmdLineArgs=""--run-forever""
		 StartupDir=""{temp}""/>

	<App AppIdTuple=""{m2}.idler""
		 ExeFullPath=""{testapp}""
		 CmdLineArgs=""--run-forever""
		 StartupDir=""{temp}""/>
</Shared>";

		static Task<TestBed.TestBed> StartBed()
			=> TestBed.TestBed.StartAsync( new TestBedOptions() { SharedConfigXml = Config } );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		[TestMethod()]
		public async Task DefinitionsAndClientsReachTheOperator()
		{
			using var bed = await StartBed();

			// StartAsync already waited for the agents to connect; make it an assertion too,
			// because this is the path everything else depends on
			var clients = await bed.Operator.GetAllClientsStateAsync();
			var connected = clients.Where( x => x.Value.Connected ).Select( x => x.Key ).ToList();
			CollectionAssert.IsSubsetOf(
				new[] { bed.Machine( "m1" ), bed.Machine( "m2" ) },
				connected,
				$"both agents should be connected, saw: {string.Join( ", ", connected )}" );

			// the shared config was read by the master and distributed to the operator
			var appDefs = await bed.Operator.GetAllAppsDefAsync();
			CollectionAssert.AreEquivalent(
				new[] { bed.App( "m1", "idler" ), bed.App( "m2", "idler" ) },
				appDefs.Select( x => x.Key ).ToList() );

			var machines = await bed.Operator.GetAllMachinesDefAsync();
			CollectionAssert.AreEquivalent(
				new[] { bed.Machine( "m1" ), bed.Machine( "m2" ) },
				machines.Select( x => x.Id ).ToList() );
		}

		[TestMethod()]
		public async Task AppStartsAndReportsRunning()
		{
			using var bed = await StartBed();
			var app = bed.App( "m1", "idler" );

			await bed.Operator.StartAppAsync( app );

			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetAppStateAsync( app ) )?.Running ?? false,
				Timeout, $"{app} reports running" );

			var state = await bed.Operator.GetAppStateAsync( app );
			Assert.IsNotNull( state );
			Assert.IsTrue( state!.Started, "the app should be marked started" );
			Assert.IsTrue( state.PID > 0, $"a running app should report a pid, got {state.PID}" );
		}

		[TestMethod()]
		public async Task AppIsKilledOnRequest()
		{
			using var bed = await StartBed();
			var app = bed.App( "m1", "idler" );

			await bed.Operator.StartAppAsync( app );
			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetAppStateAsync( app ) )?.Running ?? false,
				Timeout, $"{app} reports running before we kill it" );

			await bed.Operator.KillAppAsync( app );

			await bed.WaitUntilAsync(
				async () =>
				{
					var st = await bed.Operator.GetAppStateAsync( app );
					return st is not null && !st.Running;
				},
				Timeout, $"{app} stops reporting running" );

			var state = await bed.Operator.GetAppStateAsync( app );
			Assert.IsTrue( state!.Killed, "the app should be marked killed" );
		}

		[TestMethod()]
		public async Task AppsAreRoutedToTheRightMachine()
		{
			using var bed = await StartBed();
			var onM2 = bed.App( "m2", "idler" );
			var onM1 = bed.App( "m1", "idler" );

			await bed.Operator.StartAppAsync( onM2 );

			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetAppStateAsync( onM2 ) )?.Running ?? false,
				Timeout, $"{onM2} reports running" );

			// starting one machine's app must not start the other machine's
			var otherState = await bed.Operator.GetAppStateAsync( onM1 );
			Assert.IsFalse( otherState?.Running ?? false, $"{onM1} should not be running" );
		}
	}
}
