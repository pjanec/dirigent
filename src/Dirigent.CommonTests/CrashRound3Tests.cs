using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.Crashes;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Dirigent.Tests
{
	/// <summary>Truncated report folders, and the versions of the file against those the event could say.</summary>
	[TestClass()]
	public class CrashRound3Tests
	{
		static readonly DateTime T0 = new( 2026, 11, 20, 12, 3, 11, DateTimeKind.Utc );

		static string Report( DateTime eventTime, string? appPath, string? appName, string? nsName = null )
			=> "Version=1\r\nEventType=APPCRASH\r\nEventTime=" + eventTime.ToFileTimeUtc() + "\r\n"
				+ ( nsName is null ? "" : "NsAppName=" + nsName + "\r\n" ) + ( appName is null ? "" : "AppName=" + appName + "\r\n" ) + ( appPath is null ? "" : "AppPath=" + appPath + "\r\n" );

		static string Folder( string root, string name, string text, DateTime written )
		{
			var dir = Path.Combine( root, name );
			Directory.CreateDirectory( dir );
			File.WriteAllText( Path.Combine( dir, "Report.wer" ), text, Encoding.Unicode );
			Directory.SetLastWriteTimeUtc( dir, written );
			return Path.Combine( dir, "Report.wer" );
		}

		// ---- the report folders ------------------------------------------------------------------------

		[TestMethod()]
		public void AReportInAFolderWithATruncatedNameIsFoundByWhatTheReportSays()
		{
			var root = Path.Combine( Path.GetTempPath(), "WerTest_" + Guid.NewGuid().ToString( "N" ) );
			try
			{
				// "ConsoleApp.exe" as Windows names the folders: cut
				var truncated = Folder( root, "AppCrash_ConsoleAp_0123456789abcdef", Report( T0, @"C:\Apps\ConsoleApp.exe", "ConsoleApp.exe" ), T0 );
				var dotE = Folder( root, "AppCrash_ConsoleApp.e_fedcba9876543210", Report( T0.AddMinutes( 30 ), @"C:\Apps\ConsoleApp.exe", "ConsoleApp.exe" ), T0.AddMinutes( 30 ) );
				Folder( root, "AppCrash_Other.exe_1", Report( T0, @"C:\Apps\Other.exe", "Other.exe" ), T0 );
				Folder( root, "AppCrash_ConsoleAp_old", Report( T0.AddDays( -5 ), @"C:\Apps\ConsoleApp.exe", "ConsoleApp.exe" ), T0.AddDays( -5 ) ); // too old to be read
				Folder( root, "NonCritical_x", Report( T0, @"C:\Apps\ConsoleApp.exe", "ConsoleApp.exe" ), T0 );   // not a crash or hang folder

				Assert.AreEqual( truncated, WerReports.Find( "ConsoleApp.exe", @"C:\Apps\ConsoleApp.exe", T0, new[] { root } ) );
				Assert.AreEqual( dotE, WerReports.Find( "ConsoleApp.exe", @"C:\Apps\ConsoleApp.exe", T0.AddMinutes( 30 ), new[] { root } ) );
				Assert.IsNull( WerReports.Find( "ConsoleApp.exe", @"C:\Apps\ConsoleApp.exe", T0.AddDays( 3 ), new[] { root } ), "no folder within a day of that time" );
			}
			finally { try { Directory.Delete( root, true ); } catch { } }
		}

		[TestMethod()]
		public void AFolderTouchedDaysAfterTheCrashIsStillFoundByItsCreationAndEventTime()
		{
			var root = Path.Combine( Path.GetTempPath(), "WerTest_" + Guid.NewGuid().ToString( "N" ) );
			try
			{
				// Windows re-processed the queued report four days later: the folder was written then, but made and described at the crash
				var report = Folder( root, "AppCrash_ConsoleAp_queued", Report( T0, @"C:\Apps\ConsoleApp.exe", "ConsoleApp.exe" ), T0.AddDays( 4 ) );
				Directory.SetCreationTimeUtc( Path.GetDirectoryName( report )!, T0 );
				File.SetCreationTimeUtc( report, T0 );

				Assert.AreEqual( report, WerReports.Find( "ConsoleApp.exe", @"C:\Apps\ConsoleApp.exe", T0, new[] { root } ) );
			}
			finally { try { Directory.Delete( root, true ); } catch { } }
		}

		[TestMethod()]
		public void TheNameInTheReportIsTriedInOrderAndTheReportsAreReadOncePerLookup()
		{
			var root = Path.Combine( Path.GetTempPath(), "WerTest_" + Guid.NewGuid().ToString( "N" ) );
			try
			{
				var packaged = Folder( root, "AppCrash_Pack_1", Report( T0, null, "Something", "Packaged.exe" ), T0 );
				var cache = new WerReports.Cache();

				Assert.AreEqual( packaged, WerReports.Find( "Packaged.exe", null, T0, new[] { root }, cache ), "by NsAppName when there is no path" );
				Assert.AreEqual( 1, cache.Reports.Count );

				File.Delete( packaged ); // gone from the disk: the cache is what the lookup knows
				Assert.AreEqual( packaged, WerReports.Find( "Packaged.exe", null, T0, new[] { root }, cache ), "read once" );
			}
			finally { try { Directory.Delete( root, true ); } catch { } }
		}

		// ---- two crashes, two reports ------------------------------------------------------------------

		static WerReportInfo Rep( string name, DateTime t, string? sessionGuid = null )
			=> WerReports.Parse( Report( t, @"C:\Apps\ConsoleApp.exe", "ConsoleApp.exe" ) + ( sessionGuid is null ? "" : "AppSessionGuid=" + sessionGuid + "\r\n" ), name );

		static CrashEntry Crash( string id, DateTime t, int? pid = null )
			=> new() { id = id, kind = CrashKind.Crash, time = t, processId = pid, exe = @"C:\Apps\ConsoleApp.exe" };

		static Dictionary<string, string?> Assigned( DateTime t1, DateTime t2, params WerReportInfo[] reports )
		{
			var ctx = new CrashContext { Machine = "m1" };
			ctx.Table.Apps.Add( new CrashApp { App = "m1.c", ExePath = @"C:\Apps\ConsoleApp.exe", ExeName = "ConsoleApp.exe" } );
			ctx.WerReports = ( from, to ) => reports;

			var events = new[] { 7744, 9616 }.Zip( new[] { t1, t2 } ).Select( x => new RawEvent
			{
				Provider = CrashBuilder.ProviderAppError, Id = 1000, TimeUtc = x.Second,
				Data = new() { "ConsoleApp.exe", "1.0", "", "foo.dll", "1.0", "", "c0000005", "0000000000012345", "0x" + x.First.ToString( "x" ), "", @"C:\Apps\ConsoleApp.exe", "", "", "", "" },
			} );

			return CrashBuilder.Build( events, ctx ).ToDictionary( e => e.processId!.Value.ToString(), e => e.werReport );
		}

		[TestMethod()]
		public void TwoCrashesFiveSecondsApartEachGetTheirOwnReportInEitherOrder()
		{
			var a = Rep( "report-a", T0.AddSeconds( 1 ) );   // 1 s after the first crash
			var b = Rep( "report-b", T0.AddSeconds( 6 ) );   // 1 s after the second

			foreach( var order in new[] { new[] { a, b }, new[] { b, a } } )
			{
				var got = Assigned( T0, T0.AddSeconds( 5 ), order );
				Assert.AreEqual( "report-a", got["7744"] );
				Assert.AreEqual( "report-b", got["9616"] );
			}
		}

		[TestMethod()]
		public void AReportTakenByANearerCrashIsNotGivenAgainAndTheProcessIdOverridesNearness()
		{
			// both reports fit both crashes in time; one report only is nearer to the second crash than the first
			var only = Rep( "only", T0.AddSeconds( 4 ) );
			var got = Assigned( T0, T0.AddSeconds( 5 ), only );
			Assert.IsNull( got["7744"], "the nearer crash has it" );
			Assert.AreEqual( "only", got["9616"] );

			// the report names its process: it goes to that crash, though the other is nearer
			var named = Rep( "named", T0.AddSeconds( 4 ), "00001e40-0002-001d-8e98-a271dfe8dc01" ); // 0x1e40 = 7744
			var other = Rep( "other", T0.AddSeconds( 8 ), "00002590-0002-001d-8e98-a271dfe8dc01" ); // 0x2590 = 9616
			var byPid = Assigned( T0, T0.AddSeconds( 5 ), other, named );
			Assert.AreEqual( "named", byPid["7744"] );
			Assert.AreEqual( "other", byPid["9616"] );
			Assert.AreEqual( 7744, named.Pid );
		}

		// ---- the version of the crashed file -----------------------------------------------------------

		[TestMethod()]
		public void AnUnchangedFileGivesItsOwnVersionAndTheEventsIsKeptWhenItDiffers()
		{
			CrashBuilder.ChooseVersion( "1.2.2.22179", "1.2.2.218787", T0.AddDays( -3 ), T0, out var v, out var ev, out var note );

			Assert.AreEqual( "1.2.2.218787", v );
			Assert.AreEqual( "1.2.2.22179", ev );
			Assert.IsNull( note );

			CrashBuilder.ChooseVersion( "1.2.3.4", "1.2.3.4", T0.AddDays( -3 ), T0, out v, out ev, out note );
			Assert.AreEqual( "1.2.3.4", v );
			Assert.IsNull( ev, "nothing differs: nothing to keep" );
		}

		[TestMethod()]
		public void AFileChangedSinceTheCrashKeepsTheEventsVersionWithANote()
		{
			CrashBuilder.ChooseVersion( "1.2.2.22179", "1.2.2.999999", T0.AddHours( 1 ), T0, out var v, out var ev, out var note );

			Assert.AreEqual( "1.2.2.22179", v );
			Assert.IsNull( ev );
			StringAssert.Contains( note!, "as Windows recorded it (16 bits per part)" );
			StringAssert.Contains( note!, "changed since the crash" );
		}

		[TestMethod()]
		public void WithoutTheFileTheEventIsAllThereIs()
		{
			CrashBuilder.ChooseVersion( "1.2.2.22179", null, null, T0, out var v, out var ev, out var note );
			Assert.AreEqual( "1.2.2.22179", v );
			Assert.IsNull( ev );
			Assert.IsNull( note );
		}

		[TestMethod()]
		public void TheEntryTakesTheVersionsOfTheExeAndTheFaultingModuleFromTheirFiles()
		{
			var table = new CrashAppTable();
			table.Apps.Add( new CrashApp { App = "m1.a", ExePath = @"C:\Apps\A\A.exe", ExeName = "A.exe" } );
			var ctx = new CrashContext { Machine = "m1", Table = table };
			ctx.FileWritten = p => p.EndsWith( "A.exe" ) ? T0.AddDays( -1 ) : p.EndsWith( "foo.dll" ) ? T0.AddHours( 1 ) : null; // the dll was replaced
			ctx.FileVersion = p => p.EndsWith( "A.exe" ) ? "1.2.2.218787" : "5.0.0.100000";

			var ev = new RawEvent
			{
				Provider = CrashBuilder.ProviderAppError, Id = 1000, TimeUtc = T0,
				Data = new() { "A.exe", "1.2.2.22179", "", "foo.dll", "5.0.0.34464", "", "c0000005", "0000000000012345", "0x4d2", "", @"C:\Apps\A\A.exe", @"C:\Apps\A\foo.dll", "", "", "" },
			};

			var e = CrashBuilder.Build( new[] { ev }, ctx ).Single();

			Assert.AreEqual( "1.2.2.218787", e.exeVersion );
			Assert.AreEqual( "1.2.2.22179", e.eventExeVersion );
			Assert.IsNull( e.exeVersionNote );
			Assert.AreEqual( "5.0.0.34464", e.faultingModuleVersion, "the module has changed since: the event's" );
			Assert.IsNull( e.eventFaultingModuleVersion );
			StringAssert.Contains( e.faultingModuleVersionNote!, "changed since the crash" );
		}
	}
}
