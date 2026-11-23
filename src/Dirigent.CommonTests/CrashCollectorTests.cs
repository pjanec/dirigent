using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.Crashes;
using Dirigent.Web;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;

namespace Dirigent.Tests
{
	/// <summary>Collecting crashes: what goes into the archive, how the folders are named, what is masked, what is left out and said so.</summary>
	[TestClass()]
	public class CrashCollectorTests
	{
		static readonly DateTime T0 = new( 2026, 11, 20, 12, 3, 11, DateTimeKind.Utc );

		// ---- the masking rule --------------------------------------------------------------------------

		[TestMethod()]
		public void ANameWithSecretTokenPasswordOrKeyInItOrEndingWithPatIsMaskedWhateverTheCase()
		{
			foreach( var name in new[] { "DB_PASSWORD", "ApiToken", "my_secret_thing", "LicenseKey", "KEYBOARD", "GITHUB_PAT", "deploy_pat", "passWORD" } )
				Assert.IsTrue( CrashCollector.IsSecretName( name ), name );

			foreach( var name in new[] { "PATH", "PAT", "SPATIAL", "PATTERN", "TEMP", "DIRIGENT_PLAN", "PAT_HOME" } )
				Assert.IsFalse( CrashCollector.IsSecretName( name ), name );
		}

		[TestMethod()]
		public void MaskedValuesAreThreeStarsAndTheOthersStay()
		{
			var masked = CrashCollector.MaskSecrets( new Dictionary<string, string> { ["PATH"] = @"C:\x", ["API_KEY"] = "abc", ["build_pat"] = "ghp_1" } );

			Assert.AreEqual( @"C:\x", masked["PATH"] );
			Assert.AreEqual( "***", masked["API_KEY"] );
			Assert.AreEqual( "***", masked["build_pat"] );
		}

		// ---- the request ---------------------------------------------------------------------------------

		[TestMethod()]
		public void TheCrashesOfARequestTakeTheFiltersOfTheListingSingularOrPluralOneValueOrAList()
		{
			var request = DownloadJobs.ParseRequest( "{\"crashes\":{\"app\":\"m1.a\",\"components\":[\"A\",\"B\"],\"component\":\"C\",\"kind\":[\"crash\",\"hang\"],\"machines\":\"m2\",\"dumps\":false,\"maxDumpBytes\":5,\"since\":\"2026-11-20T14:00:00+02:00\",\"newestPerApp\":1}}" );
			var c = request!.crashes!;

			CollectionAssert.AreEqual( new[] { "m1.a" }, c.apps );
			CollectionAssert.AreEqual( new[] { "A", "B", "C" }, c.components );
			CollectionAssert.AreEqual( new[] { "crash", "hang" }, c.kinds );
			CollectionAssert.AreEqual( new[] { "m2" }, c.machines );
			Assert.IsFalse( c.dumps );
			Assert.IsTrue( c.context, "on unless said" );
			Assert.AreEqual( 5L, c.maxDumpBytes );
			Assert.AreEqual( 1, c.newestPerApp );
			Assert.AreEqual( new DateTime( 2026, 11, 20, 12, 0, 0, DateTimeKind.Utc ), c.Resolve( DateTime.UtcNow ).since );
		}

		// ---- the names of the folders ------------------------------------------------------------------

		static CrashEntry Entry( string? app, string? exe = null, string kind = CrashKind.Crash )
			=> new() { id = "m1/x/1", machine = "m1", app = app, exe = exe, kind = kind, time = T0 };

		[TestMethod()]
		public void ACrashFolderIsNamedByTheLocalTimeAndTheAppOrTheExeOrTheKind()
		{
			var used = new HashSet<string>();
			var local = T0.ToLocalTime().ToString( "yyyy-MM-dd_HH-mm-ss" );

			Assert.AreEqual( $"crashes/{local}_m1.viewer", CrashCollector.FolderName( Entry( "m1.viewer", @"C:\Apps\Viewer.exe" ), used ) );
			Assert.AreEqual( $"crashes/{local}_Viewer", CrashCollector.FolderName( Entry( null, @"C:\Apps\Viewer.exe" ), used ), "no app: the exe, without its extension" );
			Assert.AreEqual( $"crashes/{local}_gpuReset", CrashCollector.FolderName( Entry( null, null, CrashKind.GpuReset ), used ) );
			Assert.AreEqual( $"crashes/{local}_a_b", CrashCollector.FolderName( Entry( "a:b" ), used ), "what a folder name cannot hold becomes _" );
		}

		[TestMethod()]
		public void TwoCrashesOfOneAppInOneSecondGetNumberedFolders()
		{
			var used = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
			var first = CrashCollector.FolderName( Entry( "m1.a" ), used );
			var second = CrashCollector.FolderName( Entry( "m1.a" ), used );

			Assert.AreNotEqual( first, second );
			StringAssert.EndsWith( second, "_2" );
		}

		// ---- the dumps ---------------------------------------------------------------------------------

		static CrashEntry WithDump( long? size, bool exists = true )
		{
			var e = Entry( "m1.a" );
			e.dump = new CrashDump { path = @"C:\Dumps\a.1.dmp", exists = exists, size = size };
			return e;
		}

		[TestMethod()]
		public void ADumpBiggerThanTheLimitIsLeftOutWholeAndTheArchiveSaysWhy()
		{
			var asked = new CrashSelection { maxDumpBytes = 1000 };

			var (path, missing, tooBig) = CrashCollector.ChooseDump( WithDump( 1001 ), asked );

			Assert.IsNull( path, "never cut: it is in whole or not at all" );
			Assert.IsTrue( tooBig, "the _crashes.json and the cover note have to say it" );
			StringAssert.Contains( missing!, "limit" );
			StringAssert.Contains( missing!, @"C:\Dumps\a.1.dmp" );

			Assert.AreEqual( @"C:\Dumps\a.1.dmp", CrashCollector.ChooseDump( WithDump( 1000 ), asked ).path, "the limit itself still fits" );
			Assert.AreEqual( @"C:\Dumps\a.1.dmp", CrashCollector.ChooseDump( WithDump( 1L << 40 ), new CrashSelection() ).path, "no limit: any size" );
		}

		[TestMethod()]
		public void WithoutADumpTheFolderSaysWhyNone()
		{
			var none = CrashCollector.ChooseDump( Entry( "m1.a" ), new CrashSelection() );
			Assert.IsNull( none.path );
			Assert.IsFalse( none.tooBig );
			StringAssert.Contains( none.missing!, "no dump" );

			var gone = CrashCollector.ChooseDump( WithDump( null, exists: false ), new CrashSelection() );
			StringAssert.Contains( gone.missing!, "is not there" );
		}

		[TestMethod()]
		public void TheDumpBytesOfAPreviewCountOnlyTheDumpsThatWouldBeCollected()
		{
			var list = new[] { WithDump( 500 ), WithDump( 5000 ), WithDump( null, exists: false ), Entry( "m1.b" ) };

			Assert.AreEqual( 5500L, new CrashSelection().DumpBytesOf( list ) );
			Assert.AreEqual( 500L, new CrashSelection { maxDumpBytes = 1000 }.DumpBytesOf( list ) );
			Assert.AreEqual( 0L, new CrashSelection { dumps = false }.DumpBytesOf( list ) );
		}

		[TestMethod()]
		public void TheSelectionTakesItsWindowFromTheRequestWhenItHasNoneOfItsOwn()
		{
			var now = T0.AddDays( 30 );
			var own = new CrashSelection { since = T0 }.ResolveWith( now.AddDays( -2 ), null, now );
			Assert.AreEqual( T0, own.since, "its own wins" );

			var inherited = new CrashSelection().ResolveWith( T0, T0.AddHours( 1 ), now );
			Assert.AreEqual( T0, inherited.since );
			Assert.AreEqual( T0.AddHours( 1 ), inherited.until );

			Assert.AreEqual( now.AddDays( -7 ), new CrashSelection().ResolveWith( null, null, now ).since, "else the last 7 days" );
			Assert.ThrowsException<ArgumentException>( () => new CrashSelection().ResolveWith( T0, T0.AddHours( -1 ), now ) );
		}

		// ---- finding the dump in the application's own folders -----------------------------------------

		static (string path, DateTime writtenUtc, long size) F( string name, int secondsAfterCrash, long size = 10 )
			=> ( @"D:\Dumps\" + name, T0.AddSeconds( secondsAfterCrash ), size );

		[TestMethod()]
		public void TheDumpNamedForTheExeAndProcessBeatsANewerOne()
		{
			var files = new[] { F( "other.dmp", 5 ), F( "Viewer.exe.1234.dmp", 3 ) };

			var picked = CrashBuilder.PickDump( files, "Viewer.exe", 1234, T0 );

			Assert.AreEqual( @"D:\Dumps\Viewer.exe.1234.dmp", picked!.Value.path );
		}

		[TestMethod()]
		public void WithoutANameTheNewestDumpWrittenWithinAMinuteAfterTheCrashIs()
		{
			var files = new[] { F( "early.dmp", -60 ), F( "a.dmp", 10 ), F( "b.dmp", 40 ), F( "late.dmp", 61 ) };

			Assert.AreEqual( @"D:\Dumps\b.dmp", CrashBuilder.PickDump( files, "Viewer.exe", 1234, T0 )!.Value.path );
			Assert.IsNull( CrashBuilder.PickDump( new[] { F( "early.dmp", -60 ), F( "late.dmp", 61 ) }, "Viewer.exe", 1234, T0 ) );
		}

		[TestMethod()]
		public void TheDumpFoldersOfTheApplicationAreSearchedWhenWindowsNamedNone()
		{
			var table = new CrashAppTable();
			table.Apps.Add( new CrashApp { App = "m1.render", Component = "Render", ExePath = @"C:\Apps\Render\RenderHost.exe", ExeName = "RenderHost.exe" } );
			var ctx = new CrashContext { Machine = "m1", Table = table };
			ctx.AppDumpFolders = app => app == "m1.render" ? new[] { @"D:\Dumps" } : Array.Empty<string>();
			ctx.ListDumps = folder => folder == @"D:\Dumps" ? new[] { ( @"D:\Dumps\RenderHost.exe.1234.dmp", T0.AddSeconds( 2 ), 777L ), ( @"D:\Dumps\not-this.dmp", T0.AddMinutes( -9 ), 1L ) } : Array.Empty<(string, DateTime, long)>();

			var ev = new RawEvent
			{
				Provider = CrashBuilder.ProviderAppError, Id = 1000, TimeUtc = T0,
				Data = new() { "RenderHost.exe", "1.0", "", "foo.dll", "1.0", "", "c0000005", "0000000000012345", "0x4d2", "", @"C:\Apps\Render\RenderHost.exe", "", "", "", "" },
			};

			var entry = CrashBuilder.Build( new[] { ev }, ctx ).Single();

			Assert.AreEqual( @"D:\Dumps\RenderHost.exe.1234.dmp", entry.dump!.path );
			Assert.IsTrue( entry.dump.exists );
			Assert.AreEqual( 777L, entry.dump.size );

			// another application's folder is not searched for this one
			ctx.AppDumpFolders = _ => Array.Empty<string>();
			Assert.IsNull( CrashBuilder.Build( new[] { ev }, ctx ).Single().dump );
		}

		[TestMethod()]
		public void OnlyTheFileDefinitionsWhoseIdStartsWithDumpAreTheDumpFolders()
		{
			var def = new AppDef { Id = new AppIdTuple( "m1.render" ) };
			def.VfsNodes.Add( new FolderDef { Id = "dump.main", Path = @"D:\Dumps", MachineId = "m1", AppId = "render" } );
			def.VfsNodes.Add( new FileDef { Id = "Dump-extra", Path = @"E:\More\*.dmp" } );
			def.VfsNodes.Add( new FolderDef { Id = "log.main", Path = @"D:\Logs" } );
			var group = new VFolderDef { Id = "grp" };
			group.Children.Add( new FolderDef { Id = "dump.nested", Path = @"F:\Nested" } );
			def.VfsNodes.Add( group );

			var folders = CrashDumpFolders.ResolveAsync( new[] { def }, node => System.Threading.Tasks.Task.FromResult<VfsNodeDef?>( node ) ).GetAwaiter().GetResult();

			CollectionAssert.AreEquivalent( new[] { @"D:\Dumps", @"E:\More", @"F:\Nested" }, folders["m1.render"] );
		}

		// ---- the log around a crash --------------------------------------------------------------------

		[TestMethod()]
		public void TheLevelOfAnEventIsReadAndAQueryByLevelHasNoProviderList()
		{
			var ev = CrashEventXml.Parse( "<Event xmlns='http://schemas.microsoft.com/win/2004/08/events/event'><System><Provider Name='Disk'/><EventID>7</EventID><Level>2</Level><TimeCreated SystemTime='2026-11-20T12:03:11.0Z'/></System><EventData><Data>x</Data></EventData></Event>" );
			Assert.AreEqual( 2, ev.Level );

			var xpath = CrashEventXml.BuildXPath( new CrashEventQuery { FromUtc = T0, ToUtc = T0.AddMinutes( 2 ), MaxLevel = 3 } );
			StringAssert.Contains( xpath, "Level>=1 and Level<=3" );
			Assert.IsFalse( xpath.Contains( "Provider[" ) );
		}

		// ---- the archive -------------------------------------------------------------------------------

		[TestMethod()]
		public void TheCrashPartIsWrittenWithTheRecordsAndEachFolderTheDumpAndTheContext()
		{
			var dump = System.IO.Path.GetTempFileName();
			System.IO.File.WriteAllText( dump, "dump bytes" );
			try
			{
				var plan = new CrashPlan { RecordsJson = "{\"crashes\":[]}" };
				var folder = new CrashFolder { Name = "crashes/2026-11-20_13-03-11_m1.a", DumpPath = dump, DumpSize = 10 };
				folder.Texts["crash.json"] = "{}";
				folder.Texts["machine.json"] = "{}";
				plan.Folders.Add( folder );

				using var ms = new System.IO.MemoryStream();
				var used = new HashSet<string>();
				var progress = 0L;
				using( var zip = new ZipArchive( ms, ZipArchiveMode.Create, true ) )
				{
					var files = CrashCollector.Write( zip, plan, n => { used.Add( n ); return n; }, n => progress += n, default );
					Assert.AreEqual( 4, files );
				}

				using var read = new ZipArchive( new System.IO.MemoryStream( ms.ToArray() ) );
				CollectionAssert.AreEquivalent( new[]
				{
					"_crashes.json", "crashes/2026-11-20_13-03-11_m1.a/crash.json", "crashes/2026-11-20_13-03-11_m1.a/machine.json",
					"crashes/2026-11-20_13-03-11_m1.a/" + System.IO.Path.GetFileName( dump ),
				}, read.Entries.Select( e => e.FullName ).ToList() );
				Assert.AreEqual( 10L, progress, "the dump bytes are reported as they are copied" );
			}
			finally { System.IO.File.Delete( dump ); }
		}
	}
}
