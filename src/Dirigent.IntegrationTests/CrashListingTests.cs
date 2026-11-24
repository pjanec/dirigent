using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.Crashes;
using Dirigent.Scripts.BuiltIn;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using Dirigent.Web;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// The crash list through the master: the script on each machine (the event log replaced by a list of recorded events), what the
	/// master adds, and the master's own history of exits. Driven through the CLI twin of GET /api/crashes.
	/// </summary>
	[TestClass()]
	public class CrashListingTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 30 );

		/// <summary>An event log that is a list; answers like the real reader would.</summary>
		class FakeSource : ICrashEventSource
		{
			public List<RawEvent> Events = new();
			public DateTime? Oldest;

			public CrashEventResult Read( CrashEventQuery q )
				=> new() { Events = Events.Where( e => e.Log == q.Log && e.TimeUtc >= q.FromUtc && e.TimeUtc <= q.ToUtc
										&& q.Wanted.Any( w => w.provider == e.Provider && w.id == e.Id ) ).ToList() };

			public DateTime? OldestEvent( string log ) => Oldest;
		}

		static RawEvent E1000( DateTime t, string exePath, int pid, string module = "foo.dll" )
			=> new()
			{
				Provider = CrashBuilder.ProviderAppError, Id = 1000, TimeUtc = t,
				Data = new() { Path.GetFileName( exePath ), "1.0.0.0", "65aa0000", module, "1.4.2.0", "65bb0000", "c0000005", "0000000000012345",
								"0x" + pid.ToString( "x" ), "01da0000", exePath, @"C:\x\" + module, "11111111-2222-3333-4444-555555555555", "", "" },
			};

		static RawEvent E1001( DateTime t, string dumpPath )
			=> new()
			{
				Provider = CrashBuilder.ProviderWer, Id = 1001, TimeUtc = t,
				Data = new() { "Fault bucket 1, type 4", "APPCRASH", "", "0", "x.exe", "", "", "", "", "", "", "", "", "", dumpPath, @"C:\WER\one", "", "0",
								"11111111-2222-3333-4444-555555555555", "98", "", "" },
			};

		FakeSource _m1Log = new();
		FakeSource _m2Log = new();

		[TestInitialize()]
		public void SetUp()
		{
			Diagnostics.ClearLog();
			_m1Log = new FakeSource { Oldest = DateTime.UtcNow.AddDays( -20 ) };
			_m2Log = new FakeSource { Oldest = DateTime.UtcNow.AddDays( -20 ) };

			QueryCrashes.EnvironmentFactory = machine => new CrashEnvironment
			{
				Source = machine == "m1" ? _m1Log : machine == "m2" ? _m2Log : new FakeSource(),
				ReadMachineInfo = () => new CrashMachineInfo { os = "Test OS", ramTotalMB = 1024 },
				ReadVersions = path => new CrashVersions { exeFileVersion = "9.9", files = { new() { name = "a.dll" }, new() { name = "foo.dll", version = "1.4.2.0" } } },
			};
		}

		[TestCleanup()]
		public void TearDown() => QueryCrashes.EnvironmentFactory = _ => CrashEnvironment.Real();

		static string SystemDriveRoot => Path.GetPathRoot( Path.GetTempPath() )!;

		static Scenario World()
			=> Scenario.TwoMachines()
				.Share( "m1", "C", SystemDriveRoot )
				.App( "m1.render", a => a.LongRunning().Attribute( "Component", "Render" ) )
				.App( "m1.quitter", a => a.ExitsAfter( 0.5, exitCode: 7 ).Attribute( "Component", "Quit" ) )
				.App( "m1.victim", a => a.LongRunning() )
				.App( "m2.other", a => a.LongRunning().Attribute( "Component", "Other" ) );

		static async Task<CrashListing> Ask( TestBed.TestBed bed, string args = "" )
		{
			var lines = await bed.SendCliCommandAsync( "GetCrashes " + args, Timeout );
			Assert.AreEqual( 2, lines.Count, string.Join( " | ", lines ) );
			Assert.AreEqual( "END", lines[1] );
			StringAssert.StartsWith( lines[0], "CRASHES:", lines[0] );
			return JsonSerializer.Deserialize<CrashListing>( lines[0].Substring( "CRASHES:".Length ) )!;
		}

		[TestMethod()]
		public async Task TheScriptReadsEachMachineAndOneThatIsNotConnectedIsSaid()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World() } );
			bed.StopAgent( "m2" );
			await bed.WaitUntilAsync( async () => !( await bed.Operator.GetAllClientsStateAsync() ).First( c => c.Key == "m2" ).Value.Connected,
										Timeout, "the master notices that m2 is gone" );

			var dump = Path.Combine( bed.TempRoot, "dumps", "TestApp.1234.dmp" );
			Directory.CreateDirectory( Path.GetDirectoryName( dump )! );
			File.WriteAllText( dump, "not a real dump" );

			var t = DateTime.UtcNow.AddMinutes( -30 );
			_m1Log.Events.Add( E1000( t, bed.TestAppPath, 1234 ) );
			_m1Log.Events.Add( E1001( t.AddSeconds( 4 ), dump ) );
			_m1Log.Events.Add( E1000( t.AddMinutes( 1 ), @"C:\Somebody\Else.exe", 99 ) ); // not ours
			_m1Log.Events.Add( new RawEvent { Log = "System", Provider = CrashBuilder.ProviderDisplay, Id = 4101, TimeUtc = t.AddMinutes( 2 ), Data = { "gpudrv" } } );

			var list = await Ask( bed );

			CollectionAssert.AreEqual( new[] { "m1", "m2" }, list.machines.Select( m => m.machine ).ToArray() );
			Assert.AreEqual( "ok", list.machines[0].status );
			Assert.IsNotNull( list.machines[0].coverage.oldestEvent );
			Assert.IsFalse( list.machines[0].cut );
			Assert.AreEqual( "not connected", list.machines[1].status );

			Assert.AreEqual( 2, list.crashes.Count, "our crash and the display reset; not the program of somebody else" );
			Assert.AreEqual( CrashKind.GpuReset, list.crashes[0].kind, "newest first" );

			var crash = list.crashes[1];
			Assert.AreEqual( CrashKind.Crash, crash.kind );
			Assert.AreEqual( "m1", crash.machine );
			Assert.IsNull( crash.app, "the three apps of m1 share the test program: nothing tells them apart" );
			Assert.AreEqual( 3, crash.apps.Count, "all the apps of m1 that run the exe - " + string.Join( ",", crash.apps ) );
			Assert.IsNull( crash.component, "the candidates differ" );
			Assert.AreEqual( 1234, crash.processId );
			Assert.AreEqual( dump, crash.dump!.path );
			Assert.IsTrue( crash.dump.exists );
			Assert.AreEqual( 15L, crash.dump.size );
			Assert.AreEqual( @"\\127.0.0.1\C\" + dump.Substring( SystemDriveRoot.Length ), crash.dump.unc, "through the share of m1, added by the master" );
			Assert.AreEqual( 15L, list.totals.dumpBytes );
			Assert.AreEqual( 2, list.totals.crashes );
			Assert.IsNull( crash.versions, "no details were asked for" );
			Assert.IsNull( crash.machineInfo );
		}

		[TestMethod()]
		public async Task DetailsAddTheVersionsOfTheExeAndTheFactsOfTheMachine()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World() } );
			_m1Log.Events.Add( E1000( DateTime.UtcNow.AddMinutes( -5 ), bed.TestAppPath, 4321 ) );

			var crash = ( await Ask( bed, "kind=crash details=1" ) ).crashes.Single();

			Assert.AreEqual( "9.9", crash.versions!.exeFileVersion );
			Assert.AreEqual( "foo.dll", crash.versions.files[0].name, "the faulting module first" );
			Assert.IsTrue( crash.versions.files.All( f => f.modifiedAfterCrash == false ), "unchanged since the crash is false, not absent" );
			Assert.AreEqual( "Test OS", crash.machineInfo!.os );
		}

		[TestMethod()]
		public async Task TheProcessDirigentStartedIsToldByItsPidAndGivesItsAppPlanAndUptime()
		{
			var scenario = Scenario.OneMachine()
				.App( "m1.render", a => a.LongRunning().Attribute( "Component", "Render" ) )
				.App( "m1.other", a => a.LongRunning().Attribute( "Component", "Other" ) )
				.Plan( "p1", "m1.render" );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );
			var app = bed.App( "m1", "render" );
			await bed.Operator.StartAppAsync( app, "p1" );

			await bed.WaitUntilAsync( async () => ( await bed.Operator.GetAppStateAsync( app ) )?.Running ?? false, Timeout, "the app runs" );
			var state = ( await bed.Operator.GetAppStateAsync( app ) )!;
			await Task.Delay( 1200 ); // some uptime

			_m1Log.Events.Add( E1000( DateTime.UtcNow, bed.TestAppPath, state.PID ) );

			var crash = ( await Ask( bed, "component=Render" ) ).crashes.SingleOrDefault();

			Assert.IsNotNull( crash, "the pid told which of the two apps sharing the exe it was, so the component filter finds it" );
			Assert.AreEqual( "m1.render", crash.app );
			Assert.AreEqual( "Render", crash.component );
			Assert.AreEqual( "p1", crash.plan );
			Assert.IsNotNull( crash.startedAt );
			Assert.IsTrue( crash.uptimeSeconds >= 1.0 && crash.uptimeSeconds < 25, $"uptime {crash.uptimeSeconds}" );
		}

		[TestMethod()]
		public async Task AnAppThatExitsByItselfIsInTheHistoryAndOneDirigentKilledIsNot()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World() } );
			var quitter = bed.App( "m1", "quitter" );
			var victim = bed.App( "m1", "victim" );

			await bed.Operator.StartAppAsync( victim );
			await bed.Operator.StartAppAsync( quitter );

			await bed.WaitUntilAsync( async () => ( await bed.Operator.GetAppStateAsync( victim ) )?.Running ?? false, Timeout, "the victim runs" );
			await bed.Operator.KillAppAsync( victim );
			await bed.WaitUntilAsync( async () =>
			{
				var s = await bed.Operator.GetAppStateAsync( victim );
				return s is not null && !s.Running && s.Killed;
			}, Timeout, "the victim is killed" );

			await bed.WaitUntilAsync( () => bed.Master.ExitHistory.Of( quitter ).Count == 1, Timeout, "the master notes the exit of the quitter" );

			Assert.AreEqual( 0, bed.Master.ExitHistory.Of( victim ).Count, "a kill is not an unexpected exit" );

			var exit = bed.Master.ExitHistory.Of( quitter ).Single();
			Assert.AreEqual( 7, exit.exitCode );
			Assert.IsNotNull( exit.startedAt );

			// as the states route shows it
			var api = new AppApiController( bed.Master );
			var states = ( await api.GetAllAppsState() ).ToDictionary( s => s.id );
			Assert.AreEqual( 1, states["m1.quitter"].lastExits!.Count );
			Assert.AreEqual( 7, states["m1.quitter"].lastExits![0].exitCode );
			Assert.AreEqual( 0, states["m1.victim"].lastExits!.Count );
			Assert.AreEqual( 1, ( await api.GetAppState( "m1.quitter" ) ).lastExits!.Count );

			// and in the list, as a kind of its own - no event explains it
			var list = await Ask( bed, "kind=exit" );
			var entry = list.crashes.Single();
			Assert.AreEqual( CrashKind.Exit, entry.kind );
			Assert.AreEqual( "m1.quitter", entry.app );
			Assert.AreEqual( "Quit", entry.component );
			Assert.AreEqual( 7, entry.exitCode );
			Assert.AreEqual( exit.pid, entry.processId );
			Assert.IsTrue( entry.uptimeSeconds is > 0 and < 25, $"uptime {entry.uptimeSeconds}" );

			// an event of the same process is the crash, and the exit does not appear next to it
			_m1Log.Events.Add( E1000( exit.time.AddSeconds( 2 ), bed.TestAppPath, exit.pid!.Value ) );
			var merged = await Ask( bed, "app=m1.quitter" );
			var one = merged.crashes.Single();
			Assert.AreNotEqual( CrashKind.Exit, one.kind );
			Assert.AreEqual( "m1.quitter", one.app, "the history told the app" );
		}

		[TestMethod()]
		public async Task AMachineThatDoesNotAnswerInTimeIsSaidAndTheOthersAreReturned()
		{
			// m2 is connected but takes longer than the timeout to read its log
			var release = new System.Threading.ManualResetEventSlim();
			QueryCrashes.EnvironmentFactory = machine =>
			{
				if( machine == "m2" ) release.Wait( TimeSpan.FromSeconds( 20 ) );
				return new CrashEnvironment { Source = machine == "m1" ? _m1Log : new FakeSource() };
			};

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World() } );
			_m1Log.Events.Add( E1000( DateTime.UtcNow.AddMinutes( -5 ), bed.TestAppPath, 4321 ) );

			var started = DateTime.UtcNow;
			var list = await Ask( bed, "kind=crash timeout=2" );
			release.Set();

			Assert.IsTrue( DateTime.UtcNow - started < TimeSpan.FromSeconds( 15 ), "it did not wait for the machine that does not answer" );
			Assert.AreEqual( "ok", list.machines.Single( m => m.machine == "m1" ).status );
			Assert.AreEqual( "no answer within 2 s", list.machines.Single( m => m.machine == "m2" ).status );
			Assert.AreEqual( 1, list.crashes.Count, "what m1 said is there" );
		}

		[TestMethod()]
		public async Task AnApplicationWrittenWithAnotherCaseThanItsMachineStillGetsItsMachineAsked()
		{
			// the machine is "M1"; its application is written "m1.render": the script goes to the client as it is spelled, "M1"
			var scenario = Scenario.Empty().Machines( "M1" )
				.RawXml( "<App AppIdTuple='m1.render' ExeFullPath='{testapp}' CmdLineArgs='--run-forever'/>" );
			QueryCrashes.EnvironmentFactory = machine => new CrashEnvironment { Source = _m1Log };

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario } );

			var list = await Ask( bed, "timeout=10" );

			CollectionAssert.AreEqual( new[] { "M1" }, list.machines.Select( m => m.machine ).ToArray(), "one machine, in the spelling of its definition" );
			Assert.AreEqual( "ok", list.machines[0].status );
		}

		[TestMethod()]
		public async Task GroupBySignatureGivesTheBugsWithEveryIdAndTheUnreliableCrashesAlone()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World() } );
			var t = DateTime.UtcNow.AddMinutes( -30 );

			// the same bug on both machines, and a crash whose module is unknown - which stays alone
			var onM1 = E1000( t, bed.TestAppPath, 101 );
			var onM2 = E1000( t.AddMinutes( 3 ), bed.TestAppPath, 202 );
			var unreliable = E1000( t.AddMinutes( 6 ), bed.TestAppPath, 303, module: "unknown" );
			_m1Log.Events.AddRange( new[] { onM1, unreliable } );
			_m2Log.Events.Add( onM2 );

			var lines = await bed.SendCliCommandAsync( "GetCrashes kind=crash groupBy=signature", Timeout );
			Assert.AreEqual( "END", lines[1], string.Join( " | ", lines ) );
			var doc = JsonDocument.Parse( lines[0].Substring( "CRASHES:".Length ) ).RootElement;

			Assert.IsFalse( doc.TryGetProperty( "crashes", out _ ), "the grouped answer has groups and ungrouped instead" );
			var group = doc.GetProperty( "groups" ).EnumerateArray().Single();
			Assert.AreEqual( 2, group.GetProperty( "count" ).GetInt32() );
			Assert.AreEqual( "crash", group.GetProperty( "kind" ).GetString() );
			Assert.AreEqual( "foo.dll", group.GetProperty( "faultingModule" ).GetString() );
			CollectionAssert.AreEqual( new[] { "m1", "m2" }, group.GetProperty( "machines" ).EnumerateArray().Select( x => x.GetString() ).ToArray() );
			var ids = group.GetProperty( "ids" ).EnumerateArray().Select( x => x.GetString()! ).ToList();
			Assert.AreEqual( 2, ids.Count );
			StringAssert.StartsWith( ids[0], "m2/", "newest first" );

			var alone = doc.GetProperty( "ungrouped" ).EnumerateArray().Single();
			Assert.AreEqual( 303, alone.GetProperty( "processId" ).GetInt32(), "the full entry" );
			Assert.AreEqual( 3, doc.GetProperty( "totals" ).GetProperty( "crashes" ).GetInt32() );
			Assert.AreEqual( 2, doc.GetProperty( "machines" ).GetArrayLength() );

			// each crash of the group can still be asked for alone
			var one = await Ask( bed, "id=" + ids[1] );
			Assert.AreEqual( 1, one.crashes.Count );
			Assert.AreEqual( ids[1], one.crashes[0].id );

			// without groupBy nothing changes
			Assert.AreEqual( 3, ( await Ask( bed, "kind=crash" ) ).crashes.Count );
		}

		[TestMethod()]
		public async Task AnUnknownGroupByIsAnError()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World() } );

			var lines = await bed.SendCliCommandAsync( "GetCrashes groupBy=app", Timeout );

			StringAssert.Contains( string.Join( " | ", lines ), "groupBy" );
			Assert.IsFalse( lines.Any( l => l.StartsWith( "CRASHES:" ) ) );
		}

		[TestMethod()]
		public async Task AnUntilBeforeSinceIsAnError()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World() } );

			var lines = await bed.SendCliCommandAsync( "GetCrashes since=2026-11-21T10:00:00Z until=2026-11-21T09:00:00Z", Timeout );

			StringAssert.Contains( string.Join( " | ", lines ), "earlier than" );
			Assert.IsFalse( lines.Any( l => l.StartsWith( "CRASHES:" ) ) );
		}
	}
}
