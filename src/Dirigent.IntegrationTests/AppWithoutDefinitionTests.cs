using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// An application the system still remembers but the configuration no longer defines.
	/// </summary>
	/// <remarks>
	/// Reported from a live system: right-clicking a row reading *"Not running (Offline for 5999 …)"*
	/// threw a `NullReferenceException` out of the apps tab. The application was `Main_IOS.BScene`,
	/// which the shared configuration did not define at all - the machine had `BSceneEditor` and
	/// `SimIG`, and `BScene` was a leftover the master still held a state for.
	///
	/// Which is a state the GUI has to cope with rather than prevent, because the apps grid is filled
	/// from the app **states**, not from the definitions - so anything the master remembers gets a
	/// row, definition or not. Seeing the leftover is useful; the operator wants to know something
	/// unknown is lingering. `Tools.GetAppStateText` had always taken an `AppDef?` and coped; only
	/// the context menu dereferenced it.
	///
	/// These tests pin the *precondition* - that an app really can have a state and no definition -
	/// so the null checks in `MainAppsTab.MouseClick` cannot be tidied away as unreachable. The menu
	/// itself is WinForms and has no automated cover; see the manual note at the end.
	/// </remarks>
	[TestClass()]
	public class AppWithoutDefinitionTests
	{
		// the master sleeps three seconds inside a reload
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 40 );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		[TestMethod()]
		public async Task AnAppDroppedFromTheConfigCanKeepItsStateTest()
		{
			// exactly the shape of the report: two applications, one of which the configuration stops
			// naming while the system runs
			var before = Scenario.OneMachine()
				.App( "m1.BSceneEditor", a => a.LongRunning() )
				.App( "m1.BScene", a => a.LongRunning() );

			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = before } );

			var ghost = bed.App( "m1", "BScene" );

			await bed.Operator.StartAppAsync( ghost );
			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetAppStateAsync( ghost ) )?.Running ?? false,
				Timeout, "the application runs before it is dropped from the config" );

			// now it is gone from the configuration, the other one staying
			await bed.ReloadSharedConfigAsync(
				Scenario.OneMachine().App( "m1.BSceneEditor", a => a.LongRunning() ) );

			await bed.WaitUntilAsync(
				async () => ( await bed.Operator.GetAllAppsDefAsync() ).All( x => x.Key.AppId != "BScene" ),
				Timeout, "the definition goes away" );

			// The reported combination, reproduced: the state outlives the definition. The state is
			// what the grid draws a row from and the definition is what its context menu was built
			// from, so from here a right-click had a null to dereference.
			var def = ( await bed.Operator.GetAllAppsDefAsync() )
					.Where( x => x.Key == ghost )
					.Select( x => x.Value )
					.FirstOrDefault();

			Assert.IsNull( def, "the definition is gone, which is the premise of the whole case" );

			var state = await bed.Operator.GetAppStateAsync( ghost );
			Assert.IsNotNull( state,
				"the master still holds a state for it - this is what puts the row in the grid, and "
				+ "what made the crash reachable" );

			// and it is in the collection the grid is actually filled from
			var states = await bed.Operator.GetAllAppsStateAsync();
			Assert.IsTrue( states.Any( x => x.Key == ghost ),
				"the grid is filled from GetAllAppsState, so this is the row that appears" );

			// the other application is untouched, so this is not simply everything falling over
			Assert.IsTrue(
				( await bed.Operator.GetAllAppsDefAsync() ).Any( x => x.Key.AppId == "BSceneEditor" ),
				"the application that stayed in the config still has its definition" );
		}

		[TestMethod()]
		public async Task TheStatusTextCopesWithAMissingDefinitionTest()
		{
			// The display path has always taken an AppDef? - this is what the grid puts in the status
			// column for such a row, and it is where the reported "Not running (Offline for ...)"
			// came from. If this ever threw instead, the row could not even be drawn.
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions()
			{
				Scenario = Scenario.OneMachine().App( "m1.idler", a => a.LongRunning() )
			} );

			var app = bed.App( "m1", "idler" );

			var state = await bed.Operator.GetAppStateAsync( app );
			Assert.IsNotNull( state );

			// what the grid does, with the definition deliberately absent
			var text = Tools.GetAppStateText( state!, null, null );

			Assert.IsFalse( string.IsNullOrWhiteSpace( text ),
				"a row whose app has no definition still needs something in its status column" );
		}

		/// <summary>
		/// The state of an app with no definition at all, as the master holds it.
		/// </summary>
		/// <remarks>
		/// Bypasses the config entirely: the grid is filled from `GetAllAppsState`, so an entry there
		/// with no matching definition is all it takes to produce the row that crashed. Building the
		/// pair directly says what the GUI must tolerate without depending on how the master decides
		/// to expire a state.
		/// </remarks>
		[TestMethod()]
		public void EverythingTheGridNeedsWorksWithoutADefinitionTest()
		{
			var state = new AppState()
			{
				Started = false,
				Running = false,
				PlanName = "SomePlan",   // it remembers a plan it was once part of
				Initialized = false,
			};

			// the status column
			var text = Tools.GetAppStateText( state, null, null );
			Assert.IsFalse( string.IsNullOrWhiteSpace( text ) );
			StringAssert.Contains( text, "Not running",
				"which is what the reported row said, next to how long it had been offline" );

			// and with a plan state, since a leftover keeps the name of the plan it belonged to
			var planned = Tools.GetAppStateText( state, new PlanState() { Running = true }, null );
			Assert.IsFalse( string.IsNullOrWhiteSpace( planned ) );
		}
	}
}
