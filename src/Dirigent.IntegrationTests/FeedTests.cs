using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.Net;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// The state feed: the master sends the states of apps, plans, clients, machines and scripts only to the GUIs that look at them, only
	/// what has changed, and keeps the apps of an agent honest about whether the agent is there at all.
	/// </summary>
	[TestClass()]
	public class FeedTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 30 );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		static Scenario World() => Scenario.TwoMachinesWithIdlers().PlanWithEverything( "all" );

		/// <summary>
		/// A GUI client of its own, counting what the master sends it. Single-threaded: whoever uses it ticks it.
		/// </summary>
		sealed class Viewer : IDisposable
		{
			public readonly Net.Client Client;
			public readonly TrafficStats Traffic = new();
			public readonly ReflectedStateRepo Repo;
			public readonly List<Net.Message> Received = new();

			public Viewer( TestBed.TestBed bed, string name )
			{
				var ident = new ClientIdent( name, EMsgRecipCateg.Gui );
				Client = new Net.Client( ident, "127.0.0.1", bed.MasterPort, autoConn: true, traffic: Traffic, security: bed.Security );
				Repo = new ReflectedStateRepo( Client, string.Empty, bed.TempRoot );
				Client.MessageReceived += m => Received.Add( m );
			}

			public void Tick() => Client.Tick();

			public long Count( string messageType )
				=> Traffic.Snapshot().received.Where( t => t.type == messageType ).Sum( t => t.messages );

			public void Dispose()
			{
				Repo.Dispose();
				Client.Dispose();
			}
		}

		/// <summary>Ticks the viewer until the condition holds.</summary>
		static Task Until( TestBed.TestBed bed, Viewer v, Func<bool> condition, string because )
			=> bed.WaitUntilAsync( () => { v.Tick(); return condition(); }, Timeout, because );

		/// <summary>Ticks the viewer for a while, whatever happens.</summary>
		static async Task Settle( Viewer v, TimeSpan time )
		{
			var end = DateTime.UtcNow + time;
			while( DateTime.UtcNow < end )
			{
				v.Tick();
				await Task.Delay( 25 );
			}
		}

		static readonly string[] FeedTypes = { "AppsStateMessage", "PlansStateMessage", "ClientStateMessage", "MachineStateMessage", "ScriptStateMessage" };

		[TestMethod()]
		public async Task AGuiThatDoesNotWantTheFeedGetsNoneOfItAndTheFullPictureWhenItDoes()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World() } );
			using var v = new Viewer( bed, "viewer_gui_1" );

			await Until( bed, v, () => v.Repo.GetAllAppsState().Count() >= 2 && v.Repo.GetAllClientsState().Count() >= 2, "the viewer is shown the states when it connects" );

			// the window is minimised
			v.Client.FeedWanted = false;
			await Settle( v, TimeSpan.FromSeconds( 1.5 ) ); // what was already on its way arrives
			v.Traffic.Reset();

			// things happen: an app starts, which changes its state, and the agents go on with their heartbeats and machine states
			var idler = bed.App( "m1", "idler" );
			await bed.Operator.StartAppAsync( idler );
			await bed.WaitUntilAsync( async () => ( await bed.Operator.GetAppStateAsync( idler ) )?.Running == true, Timeout, "the app is running" );
			await Settle( v, TimeSpan.FromSeconds( 4.5 ) ); // longer than the master's refresh, and than the agents' machine state period

			foreach( var type in FeedTypes )
				Assert.AreEqual( 0, v.Count( type ), $"a GUI that does not want the feed was sent {type}" );

			// the window is shown again
			v.Received.Clear();
			v.Client.FeedWanted = true;
			await Until( bed, v, () => v.Received.OfType<AppsStateMessage>().Any(), "the feed comes back" );

			var first = v.Received.First( m => m is ClientStateMessage or AppsStateMessage or PlansStateMessage );
			Assert.IsInstanceOfType( first, typeof( ClientStateMessage ), "the full picture starts with the clients" );

			var apps = v.Received.OfType<AppsStateMessage>().First();
			Assert.AreEqual( 2, apps.AppsState!.Count, "the first apps message holds all the apps, not just the changes" );
			Assert.IsTrue( apps.AppsState[idler].Running, "and the app that started while the GUI was not looking is running in it" );
			Assert.IsTrue( v.Received.OfType<PlansStateMessage>().First().Full, "plans too come whole" );
		}

		[TestMethod()]
		public async Task ChangesArriveAsDeltasAndLeaveTheGuiWithTheMastersState()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World() } );
			using var v = new Viewer( bed, "viewer_gui_2" );
			await Until( bed, v, () => v.Repo.GetAllAppsState().Count() >= 2 && v.Repo.GetPlanState( "all" ) is not null, "the viewer has the states" );

			var m1 = bed.App( "m1", "idler" );
			var m2 = bed.App( "m2", "idler" );

			async Task Step( string what, Func<Task> act )
			{
				await act();
				await Until( bed, v, () => Same( v, MasterApps( bed ).GetAwaiter().GetResult() ), $"the viewer's apps equal the master's after: {what}" );
			}

			// a sequence of changes, each followed by the wait for the GUI to catch up
			await Step( "start m1", () => bed.Operator.StartAppAsync( m1 ) );
			await Step( "start m2", () => bed.Operator.StartAppAsync( m2 ) );
			await Step( "kill m1", () => bed.Operator.KillAppAsync( m1 ) );
			await Step( "start the plan", () => bed.Operator.StartPlanAsync( "all" ) );
			await bed.WaitUntilAsync( () => { v.Tick(); return v.Repo.GetPlanState( "all" )?.Running == true; }, Timeout, "the plan is running for the viewer" );
			await Step( "kill the plan", () => bed.Operator.KillPlanAsync( "all" ) );
			await bed.WaitUntilAsync( () => { v.Tick(); return v.Repo.GetPlanState( "all" )?.Running == false; }, Timeout, "the plan has stopped for the viewer" );

			var finalMaster = await MasterApps( bed );
			Assert.IsTrue( Same( v, finalMaster ), "the viewer's apps equal the master's at the end" );

			// they were deltas: the messages that told of the changes held some of the apps, not all of them
			Assert.IsTrue( v.Received.OfType<AppsStateMessage>().Any( m => m.AppsState!.Count < 2 ), "changes are sent as changes" );
			Assert.IsTrue( v.Received.OfType<PlansStateMessage>().Any( m => !m.Full ), "plans are sent as changes too" );
		}

		static async Task<Dictionary<AppIdTuple, AppState>> MasterApps( TestBed.TestBed bed )
		{
			Dictionary<AppIdTuple, AppState>? result = null;
			var op = bed.Master.AddSynchronousOp( () => result = bed.Master.AppsState.ToDictionary( k => k.Key, v => v.Value.Clone() ) );
			await op.WaitAsync();
			return result!;
		}

		static bool Same( Viewer v, Dictionary<AppIdTuple, AppState> master )
		{
			foreach( var (id, state) in master )
			{
				var mine = v.Repo.GetAppState( id );
				if( mine is null || state.DiffersForFeed( mine ) ) return false;
			}
			return true;
		}

		[TestMethod()]
		public async Task AnAppOfAGoneAgentTurnsOfflineAndOneOfAQuietAgentDoesNot()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World() } );
			using var v = new Viewer( bed, "viewer_gui_3" );

			var m1 = bed.App( "m1", "idler" );
			var m2 = bed.App( "m2", "idler" );

			async Task<bool> Offline( AppIdTuple id )
			{
				bool offline = true;
				await bed.Master.AddSynchronousOp( () => offline = bed.Master.GetAppState( id )!.IsOffline ).WaitAsync();
				return offline;
			}

			await bed.WaitUntilAsync( async () => !await Offline( m1 ) && !await Offline( m2 ), Timeout, "both agents have reported their apps" );
			await Until( bed, v, () => v.Repo.GetAppState( m2 ) is { IsOffline: false }, "the viewer sees m2's app online" );

			// the trap: nothing about the apps changes - the agents only say they are alive, and send all the apps every ten seconds -
			// and the apps must not go offline, here or in the viewer, which no longer hears about them at all
			var until = DateTime.UtcNow + TimeSpan.FromSeconds( 11 );
			while( DateTime.UtcNow < until )
			{
				v.Tick();
				Assert.IsFalse( await Offline( m1 ), "a quiet but connected agent's app is not offline (master)" );
				Assert.IsFalse( await Offline( m2 ), "a quiet but connected agent's app is not offline (master)" );
				Assert.IsFalse( v.Repo.GetAppState( m2 )!.IsOffline, "nor in the viewer" );
				await Task.Delay( 200 );
			}

			// the agent goes: its apps are offline within the time an agent may be silent, at the master (which is what REST reports) and in the viewer
			bed.StopAgent( "m2" );
			var gone = System.Diagnostics.Stopwatch.StartNew();
			await bed.WaitUntilAsync( async () => await Offline( m2 ), TimeSpan.FromSeconds( 6 ), "the apps of the gone agent are offline at the master" );
			Assert.IsTrue( gone.Elapsed < TimeSpan.FromSeconds( 4.5 ), $"it took {gone.Elapsed.TotalSeconds:0.0} s" );
			Assert.IsFalse( await Offline( m1 ), "the other agent's apps are not" );
			await Until( bed, v, () => v.Repo.GetAppState( m2 )!.IsOffline, "the change reaches the viewer" );
			Assert.IsFalse( v.Repo.GetAppState( m1 )!.IsOffline );
			StringAssert.Contains( Tools.GetAppStateText( v.Repo.GetAppState( m2 )!, null, null ), "Offline" );

			// and when it is back, they are online again
			bed.StartAgent( "m2" );
			await bed.WaitUntilAsync( async () => !await Offline( m2 ), Timeout, "the apps are online again" );
			await Until( bed, v, () => v.Repo.GetAppState( m2 )!.IsOffline == false, "and so in the viewer" );
			StringAssert.DoesNotMatch( Tools.GetAppStateText( v.Repo.GetAppState( m2 )!, null, null ), new System.Text.RegularExpressions.Regex( "Offline" ) );
		}

		[TestMethod()]
		public async Task AScriptsResultGoesToItsRequestorAndTheOthersSeeTheStateWithoutIt()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World() } );
			using var watcher = new Viewer( bed, "viewer_gui_4" );   // looks at the feed, asked for nothing
			using var other = new Viewer( bed, "viewer_gui_5" );     // asks for a script itself, with its window minimised

			await Until( bed, watcher, () => watcher.Repo.GetAllClientsState().Count() >= 2, "the watcher is connected" );
			other.Client.FeedWanted = false;

			foreach( var host in new[] { string.Empty, bed.MachineIds["m1"] } )
			{
				var where = host == string.Empty ? "on the master" : "on an agent";
				watcher.Received.Clear();
				var args = new Scripts.BuiltIn.ResolveVfsPath.TArgs() { VfsNode = new FolderDef() { Path = "C:\\", MachineId = bed.MachineIds["m1"] }, ForceUNC = false };

				// by the operator
				var instance = Guid.Empty;
				var (inst, completion) = await bed.Operator.StartScriptAsync<Scripts.BuiltIn.ResolveVfsPath.TArgs, Scripts.BuiltIn.ResolveVfsPath.TResult>(
					Scripts.BuiltIn.ResolveVfsPath._Name, args, hostId: host );
				instance = inst;
				await completion.WaitAsync( Timeout );

				var requestorStates = bed.Operator.ScriptStates.Where( s => s.Instance == instance ).ToList();
				Assert.IsTrue( requestorStates.Any( s => s.State.Status == EScriptStatus.Finished && !string.IsNullOrEmpty( s.State.Data ) ), $"the requestor got the result {where}" );

				await Until( bed, watcher, () => watcher.Received.OfType<ScriptStateMessage>().Any( m => m.Instance == instance && m.State.Status == EScriptStatus.Finished ), $"the watcher is told that the script finished {where}" );
				foreach( var m in watcher.Received.OfType<ScriptStateMessage>().Where( m => m.Instance == instance ) )
					Assert.IsNull( m.State.Data, $"the watcher was sent the data of a script it did not ask for {where}" );

				// and by a GUI whose window is minimised: what is addressed to it arrives regardless
				other.Received.Clear();
				var task = other.Repo.ScriptReg.RunScriptAsync<Scripts.BuiltIn.ResolveVfsPath.TArgs, Scripts.BuiltIn.ResolveVfsPath.TResult>(
					host, Scripts.BuiltIn.ResolveVfsPath._Name, null, args, "test", out var otherInstance );
				await bed.WaitUntilAsync( () => { other.Tick(); return task.IsCompleted; }, Timeout, $"the minimised requestor gets its result {where}" );
				Assert.IsNotNull( ( await task )?.VfsNode );
				Assert.IsTrue( other.Received.OfType<ScriptStateMessage>().Any( m => m.Instance == otherInstance && !string.IsNullOrEmpty( m.State.Data ) ) );
			}
		}
	}
}
