using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.Crashes;
using Dirigent.Web;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Dirigent.Tests
{
	/// <summary>What a real site found: machine ids that differ by case, machines that never answer, reports and dumps matched by time.</summary>
	[TestClass()]
	public class CrashSiteFixesTests
	{
		static readonly DateTime T0 = new( 2026, 11, 20, 12, 3, 11, DateTimeKind.Utc );

		// ---- a machine that never answers --------------------------------------------------------------

		[TestMethod()]
		public async Task AMachineThatDoesNotAnswerInTimeIsTimedOutWithTheSecondsSaid()
		{
			var never = new TaskCompletionSource<int>().Task;

			var e = await Assert.ThrowsExceptionAsync<TimeoutException>( () => AnswerTimeout.Within( never, TimeSpan.FromMilliseconds( 100 ) ) );
			Assert.AreEqual( "no answer within 0.1 s", e.Message );
			Assert.AreEqual( "no answer within 60 s", AnswerTimeout.Message( AnswerTimeout.Default ) );

			Assert.AreEqual( 7, await AnswerTimeout.Within( Task.FromResult( 7 ), TimeSpan.FromSeconds( 5 ) ) );
		}

		[TestMethod()]
		public void TheTimeoutParameterIsSecondsOrAnError()
		{
			Assert.IsNull( CrashFilterParser.ParseTimeout( null ) );
			Assert.AreEqual( TimeSpan.FromSeconds( 2.5 ), CrashFilterParser.ParseTimeout( "2.5" ) );
			Assert.ThrowsException<ArgumentException>( () => CrashFilterParser.ParseTimeout( "soon" ) );
			Assert.ThrowsException<ArgumentException>( () => CrashFilterParser.ParseTimeout( "0" ) );
		}

		// ---- machine ids that differ by case -----------------------------------------------------------

		[TestMethod()]
		public void AnAppWhoseMachineDiffersFromADefinedOneOnlyByCaseIsWarnedAboutNamingBoth()
		{
			var warnings = ConfigChecks.MachineIdCaseMismatches( new[] { "SRV1", "SRV2" },
				new[] { new AppIdTuple( "srv1.Notifier" ), new AppIdTuple( "srv1.Mailer" ), new AppIdTuple( "SRV2.Ok" ), new AppIdTuple( "other.App" ) } );

			Assert.AreEqual( 1, warnings.Count, string.Join( "\n", warnings ) );
			StringAssert.Contains( warnings[0], "'srv1'" );
			StringAssert.Contains( warnings[0], "'SRV1'" );
			StringAssert.Contains( warnings[0], "srv1.Notifier" );
			StringAssert.Contains( warnings[0], "srv1.Mailer" );
		}

		// ---- Report.wer --------------------------------------------------------------------------------

		static string Sample( DateTime eventTime, string exe = "Viewer.exe", string path = @"C:\Apps\Viewer\Viewer.exe" )
			=> "Version=1\r\nEventType=APPCRASH\r\nEventTime=" + eventTime.ToFileTimeUtc() + "\r\nReportType=2\r\nSig[0].Name=Application Name\r\nSig[0].Value=" + exe
				+ "\r\nSig[1].Name=Application Version\r\nSig[1].Value=1.0.0.0\r\nAppName=" + exe + "\r\nAppPath=" + path + "\r\n";

		[TestMethod()]
		public void AReportIsReadFromItsUtf16TextEventTimeAndProgram()
		{
			var file = Path.GetTempFileName();
			try
			{
				File.WriteAllText( file, Sample( T0 ), Encoding.Unicode ); // as Windows writes it: UTF-16 with a byte order mark

				var info = WerReports.Parse( File.ReadAllText( file ), file );

				Assert.AreEqual( T0, info.EventTimeUtc );
				Assert.AreEqual( "Viewer.exe", info.AppName );
				Assert.AreEqual( @"C:\Apps\Viewer\Viewer.exe", info.AppPath );
				Assert.AreEqual( "Viewer.exe", info.SigExe );
			}
			finally { File.Delete( file ); }
		}

		[TestMethod()]
		public void TheReportOfACrashIsTheOneOfTheSameProgramWithinFewSecondsOfItsEventTime()
		{
			WerReportInfo R( string name, DateTime t, string exe = "Viewer.exe", string path = @"C:\Apps\Viewer\Viewer.exe" ) => WerReports.Parse( Sample( t, exe, path ), name );

			var reports = new[]
			{
				R( "days-later", T0.AddDays( 2 ) ),
				R( "other-program", T0, "Other.exe", @"C:\Apps\Other.exe" ),
				R( "same-name-elsewhere", T0, "Viewer.exe", @"D:\Elsewhere\Viewer.exe" ),
				R( "right", T0.AddSeconds( 3 ) ),
			};

			Assert.AreEqual( "right", WerReports.Match( reports, "Viewer.exe", @"C:\Apps\Viewer\Viewer.exe", T0 )!.Path );
			Assert.AreEqual( "right", WerReports.Match( reports.Where( r => r.Path != "same-name-elsewhere" ), "viewer.EXE", null, T0 )!.Path, "by name when the event has no path" );
			Assert.IsNull( WerReports.Match( reports, "Viewer.exe", @"C:\Apps\Viewer\Viewer.exe", T0.AddMinutes( 5 ) ) );
		}

		[TestMethod()]
		public void TheReportIsFoundWithoutThe1001AndTheOneOfTheReportQueueWins()
		{
			var table = new CrashAppTable();
			table.Apps.Add( new CrashApp { App = "m1.viewer", ExePath = @"C:\Apps\Viewer\Viewer.exe", ExeName = "Viewer.exe" } );
			var ctx = new CrashContext { Machine = "m1", Table = table };
			ctx.WerReports = ( from, to ) => new[]
			{
				WerReports.Parse( Sample( T0 ), @"C:\ProgramData\Microsoft\Windows\WER\ReportQueue\AppCrash_Viewer.exe_x\Report.wer" ),
			};

			var ev = new RawEvent
			{
				Provider = CrashBuilder.ProviderAppError, Id = 1000, TimeUtc = T0,
				Data = new() { "Viewer.exe", "1.0", "", "foo.dll", "1.0", "", "c0000005", "0000000000012345", "0x4d2", "", @"C:\Apps\Viewer\Viewer.exe", "", "", "", "" },
			};

			Assert.AreEqual( @"C:\ProgramData\Microsoft\Windows\WER\ReportQueue\AppCrash_Viewer.exe_x\Report.wer", CrashBuilder.Build( new[] { ev }, ctx ).Single().werReport );
		}

		// ---- the dump of a bugcheck --------------------------------------------------------------------

		static RawEvent BugCheck( DateTime t ) => new() { Log = "System", Provider = CrashBuilder.ProviderBugCheckShort, Id = 1001, TimeUtc = t, Data = new() { "0x0000009f (0x3, 0x1, 0x2, 0x4)", @"C:\Windows\MEMORY.DMP", "x" } };

		[TestMethod()]
		public void MemoryDmpIsTheDumpOfTheLastBugcheckOnlyAndTheOthersAreToldOverwritten()
		{
			var first = T0;
			var second = T0.AddDays(1);
			var third = T0.AddDays(2);
			var ctx = new CrashContext { Machine = "m1" };
			ctx.FileSize = p => p == @"C:\Windows\MEMORY.DMP" ? 4000000000 : null;
			ctx.FileWritten = p => p == @"C:\Windows\MEMORY.DMP" ? third.AddMinutes( 2 ) : null; // written by the last one

			var entries = CrashBuilder.Build( new[] { BugCheck( first ), BugCheck( second ), BugCheck( third ) }, ctx );

			var withDump = entries.Where( e => e.dump!.exists ).ToList();
			Assert.AreEqual( 1, withDump.Count );
			Assert.AreEqual( third, withDump[0].time );

			foreach( var e in entries.Where( e => !e.dump!.exists ) )
				Assert.AreEqual( "overwritten by a later crash", e.dump!.note );

			Assert.AreEqual( 4000000000L, CrashSelection.DistinctDumps( entries ).Where( d => d.exists ).Sum( d => d.size ?? 0 ), "counted once" );
		}

		[TestMethod()]
		public void ABugchecksDumpIsTheMinidumpWrittenWithinMinutesOfIt()
		{
			var ctx = new CrashContext { Machine = "m1" };
			ctx.FindMiniDump = t => t == T0 ? @"C:\Windows\Minidump\112026-1.dmp" : null;
			ctx.FileSize = p => p.EndsWith( "112026-1.dmp" ) ? 300000 : p.EndsWith( "MEMORY.DMP" ) ? 4000000000 : null;
			ctx.FileWritten = p => p.EndsWith( "112026-1.dmp" ) ? T0.AddMinutes( 2 ) : p.EndsWith( "MEMORY.DMP" ) ? T0.AddDays( 3 ) : null;

			var e = CrashBuilder.Build( new[] { BugCheck( T0 ) }, ctx ).Single();

			Assert.AreEqual( @"C:\Windows\Minidump\112026-1.dmp", e.dump!.path );
			Assert.AreEqual( 300000L, e.dump.size );
		}

		[TestMethod()]
		public void TheSameDumpFileIsCountedOnceWhateverTheNumberOfEntriesNamingIt()
		{
			CrashEntry E( string path ) => new() { id = path, dump = new CrashDump { path = path, exists = true, size = 100 } };
			var list = new[] { E( @"C:\D\a.dmp" ), E( @"c:\d\A.DMP" ), E( @"C:\D\b.dmp" ) };

			Assert.AreEqual( 200L, new CrashSelection().DumpBytesOf( list ) );
		}

		// ---- the files as they are now -----------------------------------------------------------------

		[TestMethod()]
		public void AFileWrittenAfterTheCrashIsFlaggedAndTheNoteCountsThem()
		{
			var v = new CrashVersions { files = {
				new() { name = "old.dll", modified = T0.AddDays( -30 ) },
				new() { name = "new.dll", modified = T0.AddHours( 2 ) },
				new() { name = "newer.dll", modified = T0.AddDays( 1 ) } } };
			var now = T0.AddDays( 5 );

			var marked = CrashCollector.MarkChanged( v, T0, now );

			Assert.AreEqual( false, marked.files[0].modifiedAfterCrash, "false, not absent, for a file unchanged since" );
			Assert.AreEqual( true, marked.files[1].modifiedAfterCrash );
			Assert.AreEqual( true, marked.files[2].modifiedAfterCrash );
			Assert.AreEqual( now, marked.collectedAt );
			StringAssert.StartsWith( marked.note!, "2 files changed after the crash" );
			Assert.IsNull( v.files[1].modifiedAfterCrash, "the cached original is left alone" );
		}

		// ---- the query of the log ----------------------------------------------------------------------

		[TestMethod()]
		public void EveryClauseOfTheEventQueryNamesTheProviderAsWellAsTheId()
		{
			foreach( var (log, wanted) in new[] { ("Application", CrashBuilder.ApplicationWanted), ("System", CrashBuilder.SystemWanted) } )
			{
				var xpath = CrashEventXml.BuildXPath( new CrashEventQuery { Log = log, FromUtc = T0, ToUtc = T0.AddHours( 1 ), Wanted = wanted } );

				// a bare "EventID=1000" would take the events of every program that logs with that number
				Assert.AreEqual( wanted.Count, Regex.Matches( xpath, "EventID=" ).Count );
				foreach( var (provider, id) in wanted )
					StringAssert.Contains( xpath, $"(Provider[@Name='{provider}'] and EventID={id})" );
			}

			CollectionAssert.Contains( CrashBuilder.ApplicationWanted, ("Application Error", 1000) );
			CollectionAssert.Contains( CrashBuilder.ApplicationWanted, ("Application Hang", 1002) );
			CollectionAssert.Contains( CrashBuilder.ApplicationWanted, (".NET Runtime", 1026) );
			CollectionAssert.Contains( CrashBuilder.ApplicationWanted, ("Windows Error Reporting", 1001) );
		}
	}
}
