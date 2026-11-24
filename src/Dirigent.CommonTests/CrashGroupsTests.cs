using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.Crashes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent.Tests
{
	/// <summary>Grouping the crashes of one bug - and leaving alone every crash whose identity is not reliable.</summary>
	[TestClass()]
	public class CrashGroupsTests
	{
		static readonly DateTime T0 = new( 2026, 11, 20, 12, 0, 0, DateTimeKind.Utc );

		static CrashEntry Native( string id, int minutes = 0, string machine = "m1", string exe = @"C:\Apps\Viewer.exe", string? module = "foo.dll", string? code = "0xc0000005", string? offset = "0x0000000000012345", string? app = "m1.viewer" )
			=> new() { id = id, kind = CrashKind.Crash, machine = machine, app = app, apps = app is null ? new() : new() { app }, exe = exe, faultingModule = module, exceptionCode = code, faultingOffset = offset, exeVersion = "1.0", time = T0.AddMinutes( minutes ) };

		const string Text = "System.InvalidOperationException: broken\nat A.B.Run(String x, Int32 y) in D:\\src\\B.cs:line 12\nat C.D()";

		static CrashEntry Managed( string id, int minutes = 0, string? text = Text, string exe = @"C:\Apps\Viewer.exe" )
			=> new() { id = id, kind = CrashKind.Managed, machine = "m1", exe = exe, exceptionText = text, time = T0.AddMinutes( minutes ) };

		// ---- the keys ----------------------------------------------------------------------------------

		[TestMethod()]
		public void ANativeCrashIsKeyedByExeModuleCodeAndTheOffsetInTheModule()
		{
			var key = CrashGroups.KeyOf( Native( "a" ) )!;

			Assert.AreEqual( "crash|viewer.exe|foo.dll|0xc0000005|0x12345", key.Text );
			Assert.AreEqual( key.Text, CrashGroups.KeyOf( Native( "b", exe: @"D:\Else\VIEWER.EXE", module: "FOO.dll", code: "c0000005", offset: "12345" ) )!.Text, "case and zeros do not make two bugs" );
			Assert.AreNotEqual( key.Text, CrashGroups.KeyOf( Native( "c", offset: "0x12346" ) )!.Text );
		}

		[TestMethod()]
		public void AManagedCrashIsKeyedByExeExceptionTypeAndTheTopFrameWithoutArgumentsFileAndLine()
		{
			var key = CrashGroups.KeyOf( Managed( "a" ) )!;

			Assert.AreEqual( "managed|viewer.exe|System.InvalidOperationException|A.B.Run", key.Text );
			Assert.AreEqual( "System.InvalidOperationException", key.ExceptionType );
			Assert.AreEqual( "A.B.Run", key.TopFrame );

			// the message and the line number are not part of the bug
			Assert.AreEqual( key.Text, CrashGroups.KeyOf( Managed( "b", text: "System.InvalidOperationException: another message\nat A.B.Run(Boolean z) in X.cs:line 99" ) )!.Text );
			Assert.AreNotEqual( key.Text, CrashGroups.KeyOf( Managed( "c", text: "System.InvalidOperationException: broken\nat A.B.Other()" ) )!.Text );
		}

		[TestMethod()]
		public void AHangIsKeyedByItsExe()
		{
			var hang = new CrashEntry { id = "h", kind = CrashKind.Hang, exe = @"C:\Apps\Viewer.exe" };
			Assert.AreEqual( "hang|viewer.exe", CrashGroups.KeyOf( hang )!.Text );
		}

		[TestMethod()]
		public void ExitsAndMachineLevelEventsAreNeverGrouped()
		{
			foreach( var kind in new[] { CrashKind.Exit, CrashKind.Reboot, CrashKind.BugCheck, CrashKind.GpuReset } )
				Assert.IsNull( CrashGroups.KeyOf( new CrashEntry { id = "x", kind = kind, exe = @"C:\Apps\Viewer.exe", faultingModule = "foo.dll", exceptionCode = "0x1", faultingOffset = "0x5" } ), kind );
		}

		[TestMethod()]
		public void ACrashWithAMissingOrPlaceholderPartOfItsIdentityIsLeftAlone()
		{
			Assert.IsNull( CrashGroups.KeyOf( Native( "a", module: null ) ), "no module" );
			Assert.IsNull( CrashGroups.KeyOf( Native( "a", module: "" ) ), "empty module" );
			Assert.IsNull( CrashGroups.KeyOf( Native( "a", module: "unknown" ) ), "unknown module" );
			Assert.IsNull( CrashGroups.KeyOf( Native( "a", module: "StackHash_1a2b" ) ), "a WER placeholder" );
			Assert.IsNull( CrashGroups.KeyOf( Native( "a", offset: null ) ), "no offset" );
			Assert.IsNull( CrashGroups.KeyOf( Native( "a", offset: "0x0000000000000000" ) ), "offset 0" );
			Assert.IsNull( CrashGroups.KeyOf( Native( "a", offset: "0" ) ), "offset 0" );
			Assert.IsNull( CrashGroups.KeyOf( Native( "a", code: null ) ), "no exception code" );
			Assert.IsNull( CrashGroups.KeyOf( Native( "a", code: "" ) ), "empty exception code" );
			Assert.IsNull( CrashGroups.KeyOf( Native( "a", exe: "" ) ), "no exe" );
			Assert.IsNotNull( CrashGroups.KeyOf( Native( "a" ) ), "and the whole one is grouped" );

			Assert.IsNull( CrashGroups.KeyOf( Managed( "m", text: null ) ), "no 1026 event" );
			Assert.IsNull( CrashGroups.KeyOf( Managed( "m", text: "System.Exception: no frames here" ) ), "no top frame" );
			Assert.IsNull( CrashGroups.KeyOf( Managed( "m", text: "at A.B()" ) ), "no exception type" );
			Assert.IsNull( CrashGroups.KeyOf( Managed( "m", exe: "" ) ), "no exe" );
		}

		// ---- the groups --------------------------------------------------------------------------------

		[TestMethod()]
		public void TheSameBugOnTwoMachinesIsOneGroupWithTheIdOfEveryCrashNewestFirst()
		{
			var entries = new[]
			{
				Native( "m1/a", 0, "m1", app: "m1.viewer" ),
				Native( "m2/b", 10, "m2", app: "m2.viewer" ),
				Native( "m1/c", 5, "m1", app: "m1.viewer" ),
			};

			var (groups, ungrouped) = CrashGroups.Group( entries );

			Assert.AreEqual( 0, ungrouped.Count );
			var g = groups.Single();
			Assert.AreEqual( 3, g.count );
			CollectionAssert.AreEqual( new[] { "m2/b", "m1/c", "m1/a" }, g.ids, "every crash, newest first" );
			CollectionAssert.AreEqual( new[] { "m1", "m2" }, g.machines );
			CollectionAssert.AreEqual( new[] { "m1.viewer", "m2.viewer" }, g.apps );
			CollectionAssert.AreEqual( new[] { "1.0" }, g.exeVersions );
			Assert.AreEqual( T0, g.first );
			Assert.AreEqual( T0.AddMinutes( 10 ), g.last );
			Assert.AreEqual( "foo.dll", g.faultingModule );
			Assert.AreEqual( "0xc0000005", g.exceptionCode );
			Assert.AreEqual( "0x12345", g.faultingOffset );
			Assert.AreEqual( "viewer.exe", g.exe.ToLowerInvariant() );
		}

		[TestMethod()]
		public void DifferentOffsetsAreTwoGroupsAndUnreliableCrashesStaySingleInFull()
		{
			var entries = new[]
			{
				Native( "a", 0 ), Native( "b", 1, offset: "0x999" ),
				Native( "stack", 2, module: "StackHash_77" ), Native( "unknown", 3, module: "unknown" ),
				Managed( "noevent", 4, text: null ), Managed( "withevent", 5 ),
				new CrashEntry { id = "exit", kind = CrashKind.Exit, time = T0.AddMinutes( 6 ) },
				new CrashEntry { id = "boot", kind = CrashKind.Reboot, time = T0.AddMinutes( 7 ) },
			};

			var (groups, ungrouped) = CrashGroups.Group( entries );

			Assert.AreEqual( 3, groups.Count, "two offsets, one managed" );
			CollectionAssert.AreEqual( new[] { "boot", "exit", "noevent", "unknown", "stack" }, ungrouped.Select( e => e.id ).ToArray(), "alone, in full, newest first" );
		}

		[TestMethod()]
		public void TheGroupsAreByCountThenByTheLastCrashBothDescending()
		{
			var entries = new[]
			{
				Native( "a1", 0, offset: "0x1" ), Native( "a2", 1, offset: "0x1" ),                         // two, last at 1
				Native( "b1", 20, offset: "0x2" ), Native( "b2", 30, offset: "0x2" ),                       // two, last at 30
				Native( "c1", 50, offset: "0x3" ),                                                           // one, but the newest
				Native( "d1", 5, offset: "0x4" ), Native( "d2", 6, offset: "0x4" ), Native( "d3", 7, offset: "0x4" ), // three
			};

			var groups = CrashGroups.Group( entries ).groups;

			CollectionAssert.AreEqual( new[] { "0x4", "0x2", "0x1", "0x3" }, groups.Select( g => g.faultingOffset ).ToArray() );
		}

		[TestMethod()]
		public void TheFiltersApplyBeforeTheGrouping()
		{
			var entries = new[]
			{
				Native( "old", -60 * 24 * 3, app: "m1.viewer" ),
				Native( "new1", 0, app: "m1.viewer" ), Native( "new2", 5, app: "m1.viewer" ),
				Native( "other", 6, app: "m1.render" ),
			};
			entries[3].component = "Render";
			entries[0].component = entries[1].component = entries[2].component = "Viewer";

			var filter = new CrashFilter { components = { "Viewer" }, since = T0.AddHours( -1 ) }.Resolve( T0.AddDays( 1 ) );
			var filtered = entries.Where( e => filter.Matches( e ) ).ToList();
			var groups = CrashGroups.Group( CrashFilter.NewestPerApp( filtered, 1 ) ).groups;

			Assert.AreEqual( 1, groups.Count );
			CollectionAssert.AreEqual( new[] { "new2" }, groups[0].ids, "the old one, the other component and the older of the app are gone before the grouping" );
		}

		[TestMethod()]
		public void OnlySignatureIsAKnownGrouping()
		{
			Assert.IsFalse( CrashGroups.IsGrouping( null ) );
			Assert.IsFalse( CrashGroups.IsGrouping( "" ) );
			Assert.IsTrue( CrashGroups.IsGrouping( "Signature" ) );
			Assert.ThrowsException<ArgumentException>( () => CrashGroups.IsGrouping( "app" ) );
		}
	}
}
