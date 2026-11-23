using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.Crashes;
using Dirigent.Web;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent.Tests
{
	/// <summary>
	/// The crash list: events to entries, the filters, the exit history, and what the master adds. Everything here is pure -
	/// the recorded events are neutral test data, the log and the disk are replaced.
	/// </summary>
	[TestClass()]
	public class CrashTests
	{
		// ---- the events -------------------------------------------------------------------------------

		static readonly DateTime T0 = new( 2026, 11, 20, 12, 3, 11, DateTimeKind.Utc );

		const string Xml1000 = @"<Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'>
  <System><Provider Name='Application Error'/><EventID Qualifiers='0'>1000</EventID>
    <TimeCreated SystemTime='2026-11-20T12:03:11.123456700Z'/><Channel>Application</Channel><Computer>m1</Computer></System>
  <EventData>
    <Data>RenderHost.exe</Data><Data>1.2.3.4</Data><Data>65aa0000</Data><Data>foo.dll</Data><Data>1.4.2.0</Data><Data>65bb0000</Data>
    <Data>c0000005</Data><Data>0000000000012345</Data><Data>0x4d2</Data><Data>01da0000</Data>
    <Data>C:\Apps\Render\RenderHost.exe</Data><Data>C:\Apps\Render\foo.dll</Data>
    <Data>11111111-2222-3333-4444-555555555555</Data><Data></Data><Data></Data>
  </EventData></Event>";

		static RawEvent Ev( string provider, int id, DateTime t, params string[] data )
			=> new() { Provider = provider, Id = id, TimeUtc = t, Data = data.ToList() };

		static RawEvent E1000( DateTime t, string exe = "RenderHost.exe", string path = @"C:\Apps\Render\RenderHost.exe", int pid = 1234, string reportId = "11111111-2222-3333-4444-555555555555", string code = "c0000005", string offset = "0000000000012345" )
			=> Ev( CrashBuilder.ProviderAppError, 1000, t, exe, "1.2.3.4", "65aa0000", "foo.dll", "1.4.2.0", "65bb0000", code, offset,
					"0x" + pid.ToString( "x" ), "01da0000", path, @"C:\Apps\Render\foo.dll", reportId, "", "" );

		static RawEvent E1026( DateTime t, string exe = "RenderHost.exe" )
			=> Ev( CrashBuilder.ProviderDotNet, 1026, t,
				$"Application: {exe}\nFramework Version: v4.0.30319\nDescription: The process was terminated due to an unhandled exception.\nException Info: System.InvalidOperationException: broken\n   at A.B()\n   at C.D()\n   at E.F()\n   at G.H()\n   at I.J()\n   at K.L()" );

		static RawEvent E1001( DateTime t, string reportId = "11111111-2222-3333-4444-555555555555", string attached = "", string folder = @"C:\ProgramData\Microsoft\Windows\WER\ReportArchive\AppCrash_RenderHost.exe_abc" )
			=> Ev( CrashBuilder.ProviderWer, 1001, t, "Fault bucket 123, type 4", "APPCRASH", "Not available", "0",
					"RenderHost.exe", "1.2.3.4", "65aa0000", "foo.dll", "1.4.2.0", "65bb0000", "c0000005", "0000000000012345", "", "",
					attached, folder, "", "0", reportId, "98", "", "" );

		static CrashContext Ctx( params CrashApp[] apps )
		{
			var ctx = new CrashContext { Machine = "m1" };
			ctx.Table.Apps.AddRange( apps );
			return ctx;
		}

		static CrashApp Render( string app = "m1.render", string? component = "Render", string? startDir = null )
			=> new() { App = app, Component = component, ExePath = @"C:\Apps\Render\RenderHost.exe", ExeName = "RenderHost.exe", StartDir = startDir };

		// ---- parsing -----------------------------------------------------------------------------------

		[TestMethod()]
		public void TheXmlOfAnEventIsParsedToItsProviderIdTimeAndData()
		{
			var ev = CrashEventXml.Parse( Xml1000 );

			Assert.AreEqual( "Application Error", ev.Provider );
			Assert.AreEqual( 1000, ev.Id );
			Assert.AreEqual( new DateTime( 2026, 11, 20, 12, 3, 11, DateTimeKind.Utc ).AddTicks( 1234567 ), ev.TimeUtc );
			Assert.AreEqual( "RenderHost.exe", ev.D( 0 ) );
			Assert.AreEqual( @"C:\Apps\Render\RenderHost.exe", ev.D( 10 ) );

			var entry = CrashBuilder.Build( new[] { ev }, Ctx( Render() ) ).Single();
			Assert.AreEqual( 1234, entry.processId, "the pid is hexadecimal in the event" );
			Assert.AreEqual( "0xc0000005", entry.exceptionCode );
			Assert.AreEqual( "0x0000000000012345", entry.faultingOffset );
			Assert.AreEqual( "foo.dll", entry.faultingModule );
			Assert.AreEqual( "1.4.2.0", entry.faultingModuleVersion );
			Assert.AreEqual( "1.2.3.4", entry.exeVersion );
		}

		[TestMethod()]
		public void TheQueryHasAnXPathOfProvidersIdsAndTheWindow()
		{
			var q = new CrashEventQuery { FromUtc = T0, ToUtc = T0.AddHours( 1 ), Wanted = new() { ("Application Error", 1000), ("Display", 4101) } };
			var xpath = CrashEventXml.BuildXPath( q );

			StringAssert.Contains( xpath, "(Provider[@Name='Application Error'] and EventID=1000) or (Provider[@Name='Display'] and EventID=4101)" );
			StringAssert.Contains( xpath, "@SystemTime>='2026-11-20T12:03:11.000Z'" );
			StringAssert.Contains( xpath, "@SystemTime<='2026-11-20T13:03:11.000Z'" );
		}

		// ---- correlation -------------------------------------------------------------------------------

		[TestMethod()]
		public void The1000TheReportAndTheDotNetEventOfOneCrashAreOneEntry()
		{
			var events = new[]
			{
				E1026( T0.AddSeconds( -1 ) ),
				E1000( T0 ),
				E1001( T0.AddSeconds( 20 ), attached: "\\\\?\\C:\\Dumps\\RenderHost.exe.1234.dmp\r\n\\\\?\\C:\\WER\\Report.wer" ),
			};
			var ctx = Ctx( Render() );
			ctx.FileSize = p => p == @"C:\Dumps\RenderHost.exe.1234.dmp" ? 5000 : null;

			var entry = CrashBuilder.Build( events, ctx ).Single();

			Assert.AreEqual( CrashKind.Managed, entry.kind );
			Assert.AreEqual( "m1.render", entry.app );
			Assert.AreEqual( "Render", entry.component );
			Assert.AreEqual( T0, entry.time, "the time of the 1000" );
			Assert.AreEqual( "m1/2026-11-20T12:03:11Z/1234", entry.id );
			StringAssert.StartsWith( entry.exceptionText, "System.InvalidOperationException: broken" );
			Assert.AreEqual( CrashBuilder.MaxExceptionLines, entry.exceptionText!.Split( '\n' ).Length, "cut to a few lines" );
			Assert.AreEqual( @"C:\WER\Report.wer", entry.werReport );
			Assert.AreEqual( @"C:\Dumps\RenderHost.exe.1234.dmp", entry.dump!.path );
			Assert.IsTrue( entry.dump.exists );
			Assert.AreEqual( 5000L, entry.dump.size );
			Assert.AreEqual( @"C:\Apps\Render\RenderHost.exe", entry.exe );
		}

		[TestMethod()]
		public void ANativeCrashIsAEntryOfKindCrashAndItsReportIsFoundByTheReportId()
		{
			var events = new[]
			{
				E1000( T0 ),
				E1001( T0.AddSeconds( 3 ), reportId: "99999999-0000-0000-0000-000000000000" ), // of another crash
				E1001( T0.AddMinutes( 4 ) ),
			};

			var entry = CrashBuilder.Build( events, Ctx( Render() ) ).Single();

			Assert.AreEqual( CrashKind.Crash, entry.kind );
			Assert.IsNull( entry.exceptionText );
			Assert.AreEqual( @"C:\ProgramData\Microsoft\Windows\WER\ReportArchive\AppCrash_RenderHost.exe_abc\Report.wer", entry.werReport );
		}

		[TestMethod()]
		public void TheReportOfAnotherCrashOfTheSameExeIsNotTakenWhenItsIdIsKnown()
		{
			var events = new[]
			{
				E1000( T0, pid: 1, reportId: "aaaaaaaa-0000-0000-0000-000000000001" ),
				E1000( T0.AddMinutes( 1 ), pid: 2, reportId: "aaaaaaaa-0000-0000-0000-000000000002" ),
				E1001( T0.AddMinutes( 1 ).AddSeconds( 5 ), reportId: "aaaaaaaa-0000-0000-0000-000000000002", folder: @"C:\WER\two" ),
				E1001( T0.AddSeconds( 5 ), reportId: "aaaaaaaa-0000-0000-0000-000000000001", folder: @"C:\WER\one" ),
			};

			var entries = CrashBuilder.Build( events, Ctx( Render() ) );

			Assert.AreEqual( @"C:\WER\one\Report.wer", entries.Single( e => e.processId == 1 ).werReport );
			Assert.AreEqual( @"C:\WER\two\Report.wer", entries.Single( e => e.processId == 2 ).werReport );
		}

		[TestMethod()]
		public void ADotNetEventWithoutThe1000IsAManagedEntryOfItsOwn()
		{
			var entry = CrashBuilder.Build( new[] { E1026( T0 ) }, Ctx( Render() ) ).Single();

			Assert.AreEqual( CrashKind.Managed, entry.kind );
			Assert.AreEqual( "m1.render", entry.app, "found by the file name alone" );
			Assert.IsNull( entry.processId );
			Assert.AreEqual( "m1/2026-11-20T12:03:11Z/0", entry.id );
			Assert.AreEqual( "RenderHost.exe||System.InvalidOperationException|", entry.signature );
		}

		[TestMethod()]
		public void TwoCrashesOfOneExeAtOnceAreToldApartByTheKindOfCrashTheirDotNetTextDescribes()
		{
			var clr = E1000( T0, pid: 1, code: "e0434352" );
			var native = E1000( T0.AddMilliseconds( 20 ), pid: 2, code: "c0000005" );
			var managedText = E1026( T0.AddMilliseconds( 40 ) ); // InvalidOperationException
			var fatalText = Ev( CrashBuilder.ProviderDotNet, 1026, T0.AddMilliseconds( 10 ),
				"Application: RenderHost.exe\nDescription: The process was terminated due to an unhandled exception.\nException Info: System.AccessViolationException: Attempted to read or write protected memory." );

			var entries = CrashBuilder.Build( new[] { clr, native, managedText, fatalText }, Ctx( Render() ) );

			StringAssert.Contains( entries.Single( e => e.processId == 1 ).exceptionText!, "InvalidOperationException" );
			StringAssert.Contains( entries.Single( e => e.processId == 2 ).exceptionText!, "AccessViolationException" );
		}

		[TestMethod()]
		public void The1026OfAnotherExeIsNotJoinedToThe1000()
		{
			var other = new CrashApp { App = "m1.other", ExePath = @"C:\Apps\Other.exe", ExeName = "Other.exe" };
			var entries = CrashBuilder.Build( new[] { E1000( T0 ), E1026( T0, exe: "Other.exe" ) }, Ctx( Render(), other ) );

			Assert.AreEqual( 2, entries.Count );
			Assert.AreEqual( CrashKind.Crash, entries.Single( e => e.app == "m1.render" ).kind );
			Assert.AreEqual( CrashKind.Managed, entries.Single( e => e.app == "m1.other" ).kind );
		}

		[TestMethod()]
		public void TheSignatureIsExeModuleCodeAndTheOffsetWithoutLeadingZeros()
		{
			var entry = CrashBuilder.Build( new[] { E1000( T0 ) }, Ctx( Render() ) ).Single();
			Assert.AreEqual( "RenderHost.exe|foo.dll|0xc0000005|0x12345", entry.signature );

			var again = CrashBuilder.Build( new[] { E1000( T0.AddHours( 5 ), pid: 77 ) }, Ctx( Render() ) ).Single();
			Assert.AreEqual( entry.signature, again.signature, "the same bug, the same signature" );
		}

		[TestMethod()]
		public void AHangIsListedAndMatchedWithItsReport()
		{
			var hang = Ev( CrashBuilder.ProviderHang, 1002, T0, "RenderHost.exe", "1.2.3.4", "0x10", "01da", "01db", @"C:\Apps\Render\RenderHost.exe", "cccccccc-0000-0000-0000-000000000000" );
			var wer = E1001( T0.AddSeconds( 30 ), reportId: "cccccccc-0000-0000-0000-000000000000", folder: @"C:\WER\hang" );
			wer.Data[1] = "APPHANG";

			var entry = CrashBuilder.Build( new[] { hang, wer }, Ctx( Render() ) ).Single();

			Assert.AreEqual( CrashKind.Hang, entry.kind );
			Assert.AreEqual( 16, entry.processId );
			Assert.AreEqual( @"C:\WER\hang\Report.wer", entry.werReport );
		}

		[TestMethod()]
		public void WithoutAReportTheDumpIsTheFileOfLocalDumpsAndItsAbsenceIsSaid()
		{
			var ctx = Ctx( Render() );
			ctx.LocalDumpFolders = exe => new[] { @"D:\CrashDumps" };

			ctx.FileSize = p => p == @"D:\CrashDumps\RenderHost.exe.1234.dmp" ? 42 : null;
			var found = CrashBuilder.Build( new[] { E1000( T0 ) }, ctx ).Single();
			Assert.AreEqual( @"D:\CrashDumps\RenderHost.exe.1234.dmp", found.dump!.path );
			Assert.IsTrue( found.dump.exists );
			Assert.AreEqual( 42L, found.dump.size );

			ctx.FileSize = _ => null;
			var missing = CrashBuilder.Build( new[] { E1000( T0 ) }, ctx ).Single();
			Assert.IsFalse( missing.dump!.exists );
			Assert.AreEqual( @"D:\CrashDumps\RenderHost.exe.1234.dmp", missing.dump.path );

			ctx.LocalDumpFolders = _ => Array.Empty<string>();
			Assert.IsNull( CrashBuilder.Build( new[] { E1000( T0 ) }, ctx ).Single().dump, "LocalDumps is not set: no dump to speak of" );
		}

		// ---- machine-level events ----------------------------------------------------------------------

		[TestMethod()]
		public void RebootBlueScreenAndDisplayResetAreKeptWithoutAnApp()
		{
			var power = Ev( CrashBuilder.ProviderKernelPower, 41, T0.AddHours( 1 ) );
			power.Named["BugcheckCode"] = "0";
			var elog = Ev( CrashBuilder.ProviderEventLog, 6008, T0.AddHours( 1 ).AddSeconds( 2 ), "12:00:00", "20.11.2026" );
			var bugcheck = Ev( CrashBuilder.ProviderBugCheckShort, 1001, T0.AddHours( 3 ), "0x0000009f (0x3, 0x1, 0x2, 0x4)", @"C:\Windows\MEMORY.DMP", "dddddddd-0000-0000-0000-000000000000" );
			var gpu = Ev( CrashBuilder.ProviderDisplay, 4101, T0.AddHours( 5 ), "gpudrv" );

			var ctx = Ctx(); // no apps at all: machine-level events are always kept
			ctx.FileSize = p => p == @"C:\Windows\MEMORY.DMP" ? 1000000 : null;

			var entries = CrashBuilder.Build( new[] { power, elog, bugcheck, gpu }, ctx );

			Assert.AreEqual( 3, entries.Count, "41 and 6008 of one reboot are one entry" );
			Assert.IsTrue( entries.All( e => e.app is null && e.apps.Count == 0 && e.component is null ) );
			CollectionAssert.AreEqual( new[] { CrashKind.GpuReset, CrashKind.BugCheck, CrashKind.Reboot }, entries.Select( e => e.kind ).ToArray(), "newest first" );

			var bc = entries.Single( e => e.kind == CrashKind.BugCheck );
			Assert.AreEqual( "0x0000009f", bc.exceptionCode );
			Assert.IsTrue( bc.dump!.exists );
			Assert.AreEqual( 1000000L, bc.dump.size );
			StringAssert.Contains( entries.Single( e => e.kind == CrashKind.GpuReset ).exceptionText!, "gpudrv" );
		}

		// ---- whose crashes -----------------------------------------------------------------------------

		[TestMethod()]
		public void OnlyTheProgramsOfTheSiteAreListed()
		{
			var ctx = Ctx( Render() );
			var events = new[]
			{
				E1000( T0 ),
				E1000( T0.AddMinutes( 1 ), exe: "Browser.exe", path: @"C:\Program Files\Browser\Browser.exe", pid: 9 ),
				E1000( T0.AddMinutes( 2 ), exe: "RenderHost.exe", path: @"D:\Elsewhere\RenderHost.exe", pid: 10 ), // the same name, not our file
			};

			var entries = CrashBuilder.Build( events, ctx );

			Assert.AreEqual( 1, entries.Count );
			Assert.AreEqual( 1234, entries[0].processId );
		}

		[TestMethod()]
		public void TheExePathIsComparedWithoutRegardToCase()
		{
			var entries = CrashBuilder.Build( new[] { E1000( T0, path: @"c:\apps\RENDER\renderhost.EXE" ) }, Ctx( Render() ) );
			Assert.AreEqual( 1, entries.Count );
		}

		[TestMethod()]
		public void WhenTheEventHasOnlyTheNameTheNameDecides()
		{
			var table = new CrashAppTable();
			table.Apps.Add( Render() );

			Assert.AreEqual( "m1.render", table.Find( null, "renderhost.EXE" ).App );
			Assert.IsFalse( table.Find( null, "other.exe" ).IsOurs );
			Assert.IsFalse( table.Find( @"D:\x\RenderHost.exe" ).IsOurs, "with a path the path decides" );
		}

		[TestMethod()]
		public void DirigentsOwnProgramsAreCountedAsTheComponentDirigent()
		{
			var ctx = Ctx( Render() );
			ctx.Table.OwnExes.Add( @"C:\Dirigent\Dirigent.Agent.Console.exe" );

			var entry = CrashBuilder.Build( new[] { E1000( T0, exe: "Dirigent.Agent.Console.exe", path: @"C:\Dirigent\Dirigent.Agent.Console.exe" ) }, ctx ).Single();

			Assert.IsNull( entry.app );
			Assert.AreEqual( 0, entry.apps.Count );
			Assert.AreEqual( "Dirigent", entry.component );
		}

		[TestMethod()]
		public void AnExeSharedByAppsIsNeverGuessed()
		{
			var table = new CrashAppTable();
			table.Apps.Add( Render( "m1.a", "Render" ) );
			table.Apps.Add( Render( "m1.b", "Control" ) );

			var owner = table.Find( @"C:\Apps\Render\RenderHost.exe" );

			Assert.IsNull( owner.App );
			CollectionAssert.AreEquivalent( new[] { "m1.a", "m1.b" }, owner.Apps );
			Assert.IsNull( owner.Component, "the candidates differ" );
		}

		[TestMethod()]
		public void SharedExeOfTheSameComponentGivesThatComponent()
		{
			var table = new CrashAppTable();
			table.Apps.Add( Render( "m1.a", "Render" ) );
			table.Apps.Add( Render( "m1.b", "render" ) );

			Assert.IsNull( table.Find( @"C:\Apps\Render\RenderHost.exe" ).App );
			Assert.AreEqual( "Render", table.Find( @"C:\Apps\Render\RenderHost.exe" ).Component );
		}

		[TestMethod()]
		public void TheStartFolderOrTheCommandLineTellsSharedAppsApartWhenTheEventHasThem()
		{
			var table = new CrashAppTable();
			table.Apps.Add( Render( "m1.a", "Render", startDir: @"C:\Work\A" ) );
			table.Apps.Add( Render( "m1.b", "Control", startDir: @"C:\Work\B" ) );
			table.Apps[1].CmdLine = "--x";

			Assert.AreEqual( "m1.b", table.Find( @"C:\Apps\Render\RenderHost.exe", startDir: @"c:\work\b\" ).App );
			Assert.AreEqual( "m1.b", table.Find( @"C:\Apps\Render\RenderHost.exe", cmdLine: "--x" ).App );
			Assert.IsNull( table.Find( @"C:\Apps\Render\RenderHost.exe", startDir: @"C:\Nowhere" ).App, "an unknown folder tells nothing" );
		}

		// ---- the filters -------------------------------------------------------------------------------

		static CrashEntry Entry( string id, string machine = "m1", string? app = "m1.a", string? component = "Render", string kind = CrashKind.Crash, int minutes = 0 )
			=> new()
			{
				id = id, machine = machine, app = app, apps = app is null ? new() : new() { app }, component = component, kind = kind,
				time = T0.AddMinutes( minutes ),
			};

		static List<string> Ids( CrashFilter f, params CrashEntry[] entries ) => entries.Where( e => f.Matches( e ) ).Select( e => e.id ).ToList();

		[TestMethod()]
		public void TheWindowIsNormalizedLikeThatOfADownloadAndUntilBeforeSinceIsRefused()
		{
			var local = new CrashFilter { since = new DateTime( 2026, 11, 20, 14, 0, 0, DateTimeKind.Local ) }.Resolve( T0 );
			Assert.AreEqual( DateTimeKind.Utc, local.since!.Value.Kind );
			Assert.AreEqual( new DateTime( 2026, 11, 20, 14, 0, 0, DateTimeKind.Local ).ToUniversalTime(), local.since );

			var plain = new CrashFilter { since = new DateTime( 2026, 11, 20, 14, 0, 0, DateTimeKind.Unspecified ) }.Resolve( T0 );
			Assert.AreEqual( new DateTime( 2026, 11, 20, 14, 0, 0, DateTimeKind.Utc ), plain.since, "without an offset, UTC" );

			Assert.ThrowsException<ArgumentException>( () => new CrashFilter { since = T0, until = T0.AddSeconds( -1 ) }.Resolve( T0 ) );
		}

		[TestMethod()]
		public void WithoutAWindowTheLastSevenDaysAreListed()
		{
			var now = T0.AddDays( 30 );
			var f = new CrashFilter().Resolve( now );

			Assert.AreEqual( now.AddDays( -7 ), f.since );
			Assert.IsNull( f.until );

			var untilOnly = new CrashFilter { until = T0 }.Resolve( now );
			Assert.AreEqual( T0.AddDays( -7 ), untilOnly.since, "the week before 'until'" );

			Assert.AreEqual( 1, Ids( f, Entry( "old", minutes: 0 ), WithTime( Entry( "new" ), now.AddDays( -1 ) ) ).Count );
		}

		static CrashEntry WithTime( CrashEntry e, DateTime t ) { e.time = t; return e; }

		[TestMethod()]
		public void TheTimeFiltersCutOnBothSides()
		{
			var f = new CrashFilter { since = T0.AddMinutes( 1 ), until = T0.AddMinutes( 3 ) };
			CollectionAssert.AreEqual( new[] { "b", "c", "d" }, Ids( f, Entry( "a", minutes: 0 ), Entry( "b", minutes: 1 ), Entry( "c", minutes: 2 ), Entry( "d", minutes: 3 ), Entry( "e", minutes: 4 ) ) );
		}

		[TestMethod()]
		public void ComponentsAppsMachinesKindsAndIdsEachMatchAndWithinOneFilterTheValuesAreAlternatives()
		{
			var a = Entry( "a", app: "m1.render", component: "Render" );
			var b = Entry( "b", machine: "m2", app: "m2.control", component: "Control", kind: CrashKind.Hang );
			var c = Entry( "c", app: "m1.other", component: null, kind: CrashKind.Managed );

			CollectionAssert.AreEqual( new[] { "a", "b" }, Ids( new CrashFilter { components = { "Render", "control" } }, a, b, c ), "components: or, any case" );
			CollectionAssert.AreEqual( new[] { "a", "c" }, Ids( new CrashFilter { machines = { "m1" } }, a, b, c ) );
			CollectionAssert.AreEqual( new[] { "b", "c" }, Ids( new CrashFilter { kinds = { "hang", "managed" } }, a, b, c ) );
			CollectionAssert.AreEqual( new[] { "c" }, Ids( new CrashFilter { ids = { "c", "zzz" } }, a, b, c ) );
			CollectionAssert.AreEqual( new[] { "a", "c" }, Ids( new CrashFilter { apps = { "m1.render", "*.other" } }, a, b, c ), "app ids with wildcards" );
			CollectionAssert.AreEqual( new[] { "b" }, Ids( new CrashFilter { apps = { "m?.contr*" } }, a, b, c ) );
			CollectionAssert.AreEqual( new[] { "a" }, Ids( new CrashFilter { apps = { "render" } }, a, b, c ), "the app part alone" );
		}

		[TestMethod()]
		public void DifferentFiltersAndAndAnEntryWithoutAComponentIsDroppedOnlyByTheComponentsFilter()
		{
			var a = Entry( "a", app: "m1.render", component: "Render" );
			var b = Entry( "b", app: "m1.render", component: "Render", kind: CrashKind.Hang );
			var c = Entry( "c", app: "m1.other", component: null );

			CollectionAssert.AreEqual( new[] { "a" }, Ids( new CrashFilter { components = { "Render" }, kinds = { "crash" } }, a, b, c ) );
			CollectionAssert.AreEqual( new[] { "a", "b" }, Ids( new CrashFilter { components = { "Render" } }, a, b, c ), "c has no component" );
			CollectionAssert.AreEqual( new[] { "a", "c" }, Ids( new CrashFilter { kinds = { "crash" } }, a, b, c ), "listed by everything else" );
			Assert.AreEqual( 0, Ids( new CrashFilter { components = { "Render" }, machines = { "m9" } }, a, b, c ).Count );
		}

		[TestMethod()]
		public void AnAmbiguousEntryIsJudgedByItsCandidatesAndLeftToTheMasterOnTheAgent()
		{
			var shared = Entry( "s", app: null, component: null );
			shared.apps = new() { "m1.a", "m1.b" };

			Assert.IsTrue( new CrashFilter { apps = { "m1.b" } }.Matches( shared ) );
			Assert.IsFalse( new CrashFilter { apps = { "m1.z" } }.Matches( shared ) );
			Assert.IsFalse( new CrashFilter { components = { "Render" } }.Matches( shared ) );
			Assert.IsTrue( new CrashFilter { components = { "Render" } }.Matches( shared, deferAmbiguous: true ), "the master may still tell the app by the pid" );
		}

		[TestMethod()]
		public void NewestPerAppKeepsTheNewestOfEachAppAndAllTheMachineLevelOnes()
		{
			var list = new[]
			{
				Entry( "a1", app: "m1.a", minutes: 1 ), Entry( "a2", app: "m1.a", minutes: 2 ), Entry( "a3", app: "m1.a", minutes: 3 ),
				Entry( "b1", app: "m1.b", minutes: 1 ),
				Entry( "r1", app: null, component: null, kind: CrashKind.Reboot, minutes: 4 ), Entry( "r2", app: null, component: null, kind: CrashKind.Reboot, minutes: 5 ),
			};

			var two = CrashFilter.NewestPerApp( list, 2 );
			CollectionAssert.AreEqual( new[] { "r2", "r1", "a3", "a2", "b1" }, two.Select( e => e.id ).ToArray(), "newest first" );

			CollectionAssert.AreEqual( new[] { "r2", "r1", "a3", "b1" }, CrashFilter.NewestPerApp( list, 1 ).Select( e => e.id ).ToArray() );
			Assert.AreEqual( 6, CrashFilter.NewestPerApp( list, null ).Count );
		}

		[TestMethod()]
		public void TheFilterIsReadFromQueryParametersRepeatedOrCommaSeparated()
		{
			var q = new System.Collections.Specialized.NameValueCollection
			{
				{ "component", "A,B" }, { "component", "C" }, { "app", "m1.*" }, { "kind", "crash" }, { "machines", "m1, m2" },
				{ "since", "2026-11-20T14:00:00+02:00" }, { "until", "2026-11-21T00:00:00Z" }, { "newestPerApp", "2" },
			};

			var f = CrashFilterParser.Parse( q );

			CollectionAssert.AreEqual( new[] { "A", "B", "C" }, f.components );
			CollectionAssert.AreEqual( new[] { "m1.*" }, f.apps );
			CollectionAssert.AreEqual( new[] { "m1", "m2" }, f.machines );
			Assert.AreEqual( new DateTime( 2026, 11, 20, 12, 0, 0, DateTimeKind.Utc ), f.since );
			Assert.AreEqual( new DateTime( 2026, 11, 21, 0, 0, 0, DateTimeKind.Utc ), f.until );
			Assert.AreEqual( 2, f.newestPerApp );

			Assert.ThrowsException<ArgumentException>( () => CrashFilterParser.Parse( new System.Collections.Specialized.NameValueCollection { { "since", "yesterday-ish" } } ) );
			Assert.ThrowsException<ArgumentException>( () => CrashFilterParser.Parse( new System.Collections.Specialized.NameValueCollection { { "newestPerApp", "x" } } ) );
		}

		// ---- the exit history --------------------------------------------------------------------------

		static AppState Running( int pid = 5, DateTime? startedAt = null, string? plan = "p1" )
			=> new() { Started = true, Running = true, PID = pid, StartedAt = startedAt ?? T0.AddHours( -1 ), PlanName = plan };

		static AppState Ended( AppState before, int exitCode = 3, bool killed = false, bool dying = false, bool restarting = false )
		{
			var s = before.Clone();
			s.Running = false;
			s.Killed = killed;
			s.Dying = dying;
			s.Restarting = restarting;
			s.ExitCode = exitCode;
			s.EndedAt = T0;
			return s;
		}

		[TestMethod()]
		public void AnExitWithoutDirigentKillingItIsRecordedWithWhatWasKnown()
		{
			var h = new ExitHistory();
			var id = new AppIdTuple( "m1.a" );
			var before = Running( pid: 77 );

			var exit = h.Observe( id, before, Ended( before, exitCode: 9, restarting: true ), T0.AddSeconds( 2 ) );

			Assert.IsNotNull( exit );
			Assert.AreEqual( T0, exit!.time, "the end the agent saw, not the time the master heard of it" );
			Assert.AreEqual( 9, exit.exitCode );
			Assert.AreEqual( 77, exit.pid );
			Assert.AreEqual( T0.AddHours( -1 ), exit.startedAt );
			Assert.AreEqual( "p1", exit.plan );
			Assert.IsTrue( exit.willRestart );
			Assert.AreEqual( 1, h.Of( id ).Count );
		}

		[TestMethod()]
		public void AnExitDirigentCausedIsNotUnexpectedAndNeitherIsAStateThatChangedNothing()
		{
			var h = new ExitHistory();
			var id = new AppIdTuple( "m1.a" );
			var before = Running();

			Assert.IsNull( h.Observe( id, before, Ended( before, killed: true ), T0 ), "killed" );

			var wasKilled = Running(); wasKilled.Killed = true;
			var restarted = Ended( wasKilled ); restarted.Killed = false; // the flag is cleared by a new start, not by the end
			Assert.IsNull( h.Observe( id, wasKilled, restarted, T0 ), "the kill had been asked for before" );

			var dying = Running(); dying.Dying = true;
			Assert.IsNull( h.Observe( id, dying, Ended( dying ), T0 ), "it was being killed" );

			Assert.IsNull( h.Observe( id, before, before.Clone(), T0 ), "still running" );
			Assert.IsNull( h.Observe( id, Ended( before ), Ended( before ), T0 ), "was not running before" );
			Assert.IsNull( h.Observe( id, null, Ended( before ), T0 ), "no earlier state" );
			Assert.AreEqual( 0, h.Of( id ).Count );
		}

		[TestMethod()]
		public void OnlyTheLastTenExitsOfAnAppAreKeptNewestFirst()
		{
			var h = new ExitHistory();
			var id = new AppIdTuple( "m1.a" );

			for( int i = 1; i <= 13; i++ )
			{
				var before = Running( pid: i );
				var after = Ended( before, exitCode: i );
				after.EndedAt = T0.AddMinutes( i );
				h.Observe( id, before, after, T0 );
			}

			var list = h.Of( id );
			Assert.AreEqual( ExitHistory.PerApp, list.Count );
			Assert.AreEqual( 13, list[0].exitCode );
			Assert.AreEqual( 4, list[^1].exitCode );
			Assert.AreEqual( 0, h.Of( new AppIdTuple( "m1.b" ) ).Count );
		}

		// ---- what the master adds ----------------------------------------------------------------------

		static CrashMerger Merger()
		{
			var m = new CrashMerger();
			m.ComponentOf["m1.render"] = "Render";
			m.ComponentOf["m1.control"] = "Control";
			return m;
		}

		[TestMethod()]
		public void TheStartTimePlanAndUptimeComeFromTheExitOfTheSamePid()
		{
			var m = Merger();
			m.Exits["m1.render"] = new() { new AppExit { time = T0.AddSeconds( 1 ), pid = 1234, startedAt = T0.AddSeconds( -90 ), plan = "p9" } };

			var e = Entry( "x", app: "m1.render" );
			e.processId = 1234;
			m.Enrich( e );

			Assert.AreEqual( T0.AddSeconds( -90 ), e.startedAt );
			Assert.AreEqual( 90.0, e.uptimeSeconds );
			Assert.AreEqual( "p9", e.plan );

			var other = Entry( "y", app: "m1.render" );
			other.processId = 999; // not a process we know
			m.Enrich( other );
			Assert.IsNull( other.startedAt );
			Assert.IsNull( other.plan );
		}

		[TestMethod()]
		public void TheCurrentStateServesWhenTheProcessStillRunsAndOnlyForTheSamePid()
		{
			var m = Merger();
			m.Running["m1.render"] = new RunningProcess { Pid = 50, StartedAt = T0.AddHours( -2 ), Plan = "live" };

			var same = Entry( "x", app: "m1.render" );
			same.processId = 50;
			m.Enrich( same );
			Assert.AreEqual( 7200.0, same.uptimeSeconds );
			Assert.AreEqual( "live", same.plan );

			var reused = Entry( "y", app: "m1.render" );
			reused.processId = 51;
			m.Enrich( reused );
			Assert.IsNull( reused.uptimeSeconds );

			var before = Entry( "z", app: "m1.render" );
			before.processId = 50;
			before.time = T0.AddHours( -5 ); // the pid is the same, but the process had not been started yet
			m.Enrich( before );
			Assert.IsNull( before.uptimeSeconds );
		}

		[TestMethod()]
		public void ThePidSettlesWhichOfTheSharedAppsItWas()
		{
			var m = Merger();
			m.Exits["m1.control"] = new() { new AppExit { time = T0, pid = 1234, startedAt = T0.AddSeconds( -5 ) } };

			var e = Entry( "x", app: null, component: null );
			e.apps = new() { "m1.render", "m1.control" };
			e.processId = 1234;
			m.Enrich( e );

			Assert.AreEqual( "m1.control", e.app );
			Assert.AreEqual( "Control", e.component );
			Assert.AreEqual( 5.0, e.uptimeSeconds );
			Assert.IsTrue( new CrashFilter { components = { "Control" } }.Matches( e ) );
		}

		[TestMethod()]
		public void AnExitNoEventExplainsIsListedAsKindExitAndOneAnEventExplainsIsNot()
		{
			var m = Merger();
			m.Exits["m1.render"] = new()
			{
				new AppExit { time = T0.AddMinutes( 30 ), pid = 11, exitCode = 255, startedAt = T0.AddMinutes( 29 ), plan = "p" },
				new AppExit { time = T0.AddSeconds( 4 ), pid = 1234, exitCode = -1073741819 },
			};

			var event1 = Entry( "ev", app: "m1.render" );
			event1.processId = 1234;

			var exits = m.ExitEntries( new[] { event1 }, _ => true ).ToList();

			var exit = exits.Single();
			Assert.AreEqual( CrashKind.Exit, exit.kind );
			Assert.AreEqual( "m1.render", exit.app );
			Assert.AreEqual( "Render", exit.component );
			Assert.AreEqual( 255, exit.exitCode );
			Assert.AreEqual( 60.0, exit.uptimeSeconds );
			Assert.AreEqual( "m1/2026-11-20T12:33:11Z/11", exit.id );
			Assert.AreEqual( "m1", exit.machine );

			Assert.AreEqual( 0, m.ExitEntries( new[] { event1 }, machine => machine == "m2" ).Count(), "only the machines asked for" );

			// the same pid but far in time is not the same crash (this exit knows no start; the other one has lived 30 minutes)
			event1.time = T0.AddHours( 3 );
			Assert.AreEqual( 2, m.ExitEntries( new[] { event1 }, _ => true ).Count() );

			// a crashed process may live on while Windows reports on it: its exit comes minutes after the event, and is still that crash
			m.Exits["m1.render"] = new() { new AppExit { time = T0.AddMinutes( 12 ), pid = 1234, startedAt = T0.AddSeconds( -40 ), exitCode = 1 } };
			event1.time = T0;
			Assert.AreEqual( 0, m.ExitEntries( new[] { event1 }, _ => true ).Count(), "the event is within the life of the process" );

			// and an event of an earlier process that had the pid is not
			event1.time = T0.AddMinutes( -30 );
			Assert.AreEqual( 1, m.ExitEntries( new[] { event1 }, _ => true ).Count() );
		}

		[TestMethod()]
		public void TheVersionsOfTheFaultingModuleAreListedFirst()
		{
			var v = new CrashVersions { exeFileVersion = "1", files = { new() { name = "a.dll" }, new() { name = "foo.dll" }, new() { name = "z.dll" } } };

			var first = CrashQuery.FaultingFirst( v, "FOO.dll" );

			CollectionAssert.AreEqual( new[] { "foo.dll", "a.dll", "z.dll" }, first.files.Select( f => f.name ).ToArray() );
			Assert.AreEqual( "a.dll", v.files[0].name, "the original is left alone" );
		}

		// ---- the real log ------------------------------------------------------------------------------

		[TestMethod()]
		public void TheRealEventLogOfThisMachineCanBeReadAndTheEventsAreOfTheKindsAsked()
		{
			if( !OperatingSystem.IsWindows() ) Assert.Inconclusive( "Windows only" );

			var source = new WindowsCrashEventSource();
			var result = source.Read( new CrashEventQuery
			{
				Log = "Application", FromUtc = DateTime.UtcNow.AddDays( -30 ), ToUtc = DateTime.UtcNow.AddMinutes( 5 ),
				Wanted = CrashBuilder.ApplicationWanted, MaxEvents = 200,
			} );

			Assert.IsTrue( result.Events.All( e => CrashBuilder.ApplicationWanted.Contains( ( e.Provider, e.Id ) ) ) );
			Assert.IsTrue( result.Events.All( e => e.TimeUtc > DateTime.UtcNow.AddDays( -31 ) ) );

			// whatever it holds, it builds without failing
			CrashBuilder.Build( result.Events, new CrashContext { Machine = "m1" } );

			Assert.IsNotNull( source.OldestEvent( "Application" ), "an Application log is never empty" );
		}
	}
}
