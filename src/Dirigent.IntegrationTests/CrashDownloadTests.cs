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
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// Crashes in a download: the agents look them up again with the request's filters (the event log replaced by a list), and add
	/// only the dumps of those crashes, with their context; every archive carries the records of its window.
	/// </summary>
	[TestClass()]
	public class CrashDownloadTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 90 );

		class FakeSource : ICrashEventSource
		{
			public List<RawEvent> Events = new();

			public CrashEventResult Read( CrashEventQuery q )
				=> new() { Events = Events.Where( e => e.Log == q.Log && e.TimeUtc >= q.FromUtc && e.TimeUtc <= q.ToUtc
										&& ( q.MaxLevel is int level ? e.Level <= level : q.Wanted.Any( w => w.provider == e.Provider && w.id == e.Id ) ) ).ToList() };

			public DateTime? OldestEvent( string log ) => DateTime.UtcNow.AddDays( -20 );
		}

		static RawEvent E1000( DateTime t, string exePath, int pid, string module = "foo.dll" )
			=> new()
			{
				Provider = CrashBuilder.ProviderAppError, Id = 1000, TimeUtc = t,
				Data = new() { Path.GetFileName( exePath ), "1.0.0.0", "65aa0000", module, "1.4.2.0", "65bb0000", "c0000005", "0000000000012345",
								"0x" + pid.ToString( "x" ), "01da0000", exePath, @"C:\x\" + module, Guid.NewGuid().ToString(), "", "" },
			};

		static RawEvent E1001For( RawEvent crash, string dumpPath, string folder )
			=> new()
			{
				Provider = CrashBuilder.ProviderWer, Id = 1001, TimeUtc = crash.TimeUtc.AddSeconds( 3 ),
				Data = new() { "Fault bucket 1, type 4", "APPCRASH", "", "0", "x.exe", "", "", "", "", "", "", "", "", "", dumpPath, folder, "", "0", crash.D( 12 ), "98", "", "" },
			};

		readonly Dictionary<string, FakeSource> _logs = new() { ["m1"] = new(), ["m2"] = new() };

		[TestInitialize()]
		public void SetUp()
		{
			Diagnostics.ClearLog();
			QueryCrashes.EnvironmentFactory = machine => new CrashEnvironment
			{
				Source = _logs.TryGetValue( machine, out var s ) ? s : new FakeSource(),
				ReadMachineInfo = () => new CrashMachineInfo { os = "Test OS", ramTotalMB = 1024 },
				ReadVersionsWithHashes = path => new CrashVersions { exeFileVersion = "9.9", files = { new() { name = "a.dll", sha256 = "ab" }, new() { name = "foo.dll", version = "1.4.2.0", sha256 = "cd" } } },
				ReadVersions = path => new CrashVersions { exeFileVersion = "9.9", files = { new() { name = "a.dll", sha256 = "ab" }, new() { name = "foo.dll", version = "1.4.2.0", sha256 = "cd" } } },
			};
		}

		[TestCleanup()]
		public void TearDown() => QueryCrashes.EnvironmentFactory = _ => CrashEnvironment.Real();

		static Scenario World()
			=> Scenario.TwoMachines()
				.App( "m1.render", a => a.LongRunning().Attribute( "Component", "Render" )
					.RawXml( "<Folder Id='log.main' Title='Logs' Path='{applogs}' Mask='*.log'/>" ) )
				.App( "m1.other", a => a.Exe( "other-tool.exe" ).Attribute( "Component", "Other" ) )
				.App( "m2.render", a => a.LongRunning().Attribute( "Component", "Render" )
					.RawXml( "<Folder Id='dump.main' Title='Dumps' Path='{temp}\\dumps2' Mask='*.dmp'/>" ) );

		/// <summary>Two machines, a crash of Render on each, and one of Other on m1 whose dump must stay out.</summary>
		async Task<(TestBed.TestBed bed, string d1, string d2, string d3, string stray)> Crashed( Scenario? scenario = null )
		{
			var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = scenario ?? World() } );
			var exe = bed.TestAppPath;

			var dumps = Path.Combine( bed.TempRoot, "dumps" );
			Directory.CreateDirectory( dumps );
			var d1 = Path.Combine( dumps, "render.dmp" );
			var d2 = Path.Combine( dumps, "other.dmp" );
			File.WriteAllText( d1, "dump of render on m1" );
			File.WriteAllText( d2, "dump of other on m1" );

			// m2: its dump is in the application's own dump folder, named for the exe and the process; another one lies beside it
			var dumps2 = Path.Combine( bed.TempRoot, "dumps2" );
			Directory.CreateDirectory( dumps2 );
			var d3 = Path.Combine( dumps2, $"{Path.GetFileName( exe )}.333.dmp" );
			var stray = Path.Combine( dumps2, "older-and-unrelated.dmp" );
			File.WriteAllText( d3, "dump of render on m2" );
			File.WriteAllText( stray, "somebody else's dump" );
			File.SetLastWriteTimeUtc( stray, DateTime.UtcNow.AddHours( -3 ) );

			var t = DateTime.UtcNow.AddMinutes( -20 );
			var c1 = E1000( t, exe, 111 );
			var c2 = E1000( t.AddMinutes( 1 ), @"C:\Somewhere\other-tool.exe", 222 );
			_logs["m1"].Events.AddRange( new[] { c1, E1001For( c1, d1, Path.Combine( bed.TempRoot, "wer1" ) ), c2, E1001For( c2, d2, Path.Combine( bed.TempRoot, "wer2" ) ) } );
			_logs["m1"].Events.Add( new RawEvent { Log = "System", Provider = "Disk", Id = 7, Level = 2, TimeUtc = t.AddSeconds( -30 ), Data = { "bad block" } } );

			var c3 = E1000( t.AddMinutes( 2 ), exe, 333 );
			_logs["m2"].Events.Add( c3 );
			File.SetLastWriteTimeUtc( d3, t.AddMinutes( 2 ).AddSeconds( 2 ) );

			return (bed, d1, d2, d3, stray);
		}

		static async Task<DownloadJobView> Download( TestBed.TestBed bed, DownloadRequest request )
		{
			Guid id = Guid.Empty;
			var op = bed.Master.AddSynchronousOp( () => id = bed.Master.DownloadJobs.Start( request ) );
			await op.WaitAsync();
			if( op.Exception != null ) throw op.Exception;

			DownloadJobView? view = null;
			await bed.WaitUntilAsync( () =>
			{
				view = bed.Master.DownloadJobs.Get( id );
				return view is not null && view.status != "Running";
			}, Timeout, "the download ends" );

			Assert.AreEqual( "Finished", view!.status, string.Join( "; ", view.errors ) );
			Assert.AreEqual( 0, view.errors.Count, string.Join( "; ", view.errors ) );
			return view;
		}

		/// <summary>Contains, with a message that survives the braces of the JSON it quotes.</summary>
		static void Has( string text, string part, string? _ = null )
			=> Assert.IsTrue( text.Contains( part ), "missing [" + part + "] in: " + text.Replace( "{", "(" ).Replace( "}", ")" ) );

		static DownloadRequest Crashes( string json ) => DownloadJobs.ParseRequest( json )!;

		static Dictionary<string, string> Read( string zipPath )
		{
			using var zip = ZipFile.OpenRead( zipPath );
			return zip.Entries.Where( e => !e.FullName.EndsWith( "/" ) ).ToDictionary( e => e.FullName, e =>
			{
				using var r = new StreamReader( e.Open() );
				return r.ReadToEnd();
			} );
		}

		[TestMethod()]
		public async Task OnlyTheDumpsOfTheMatchingCrashesAreCollectedOnTwoMachinesWithTheirContext()
		{
			var (bed, d1, d2, d3, stray) = await Crashed();
			using var _ = bed;

			var job = await Download( bed, Crashes( "{\"crashes\":{\"components\":[\"Render\"],\"dumps\":true}}" ) );
			var files = Read( job.archives[0].localPath );

			string Find( string machine, string suffix )
			{
				var found = files.Keys.Where( k => k.StartsWith( $"{machine}/crashes/" ) && k.EndsWith( suffix ) ).ToList();
				Assert.AreEqual( 1, found.Count, $"{machine} {suffix} in: " + string.Join( " | ", files.Keys ) );
				return found[0];
			}

			// the dump of Windows' report on m1, the dump found in the application's own folder on m2
			Assert.AreEqual( "dump of render on m1", files[Find( "m1", "render.dmp" )] );
			Assert.AreEqual( "dump of render on m2", files[Find( "m2", ".333.dmp" )] );

			Assert.IsFalse( files.Values.Any( v => v.Contains( "dump of other" ) ), "the dump of the other component stays out" );
			Assert.IsFalse( files.Values.Any( v => v.Contains( "somebody else" ) ), "a dump of the same folder that is no crash's stays out" );
			Assert.IsFalse( files.Keys.Any( k => k.Contains( "other.dmp" ) || k.Contains( "older-and-unrelated" ) ), string.Join( "\n", files.Keys ) );

			// one folder per crash, with what the context has
			var folder = Path.GetDirectoryName( Find( "m1", "render.dmp" ) )!.Replace( '\\', '/' );
			foreach( var name in new[] { "crash.json", "versions.json", "launch.json", "events.txt", "machine.json" } )
				Assert.IsTrue( files.ContainsKey( $"{folder}/{name}" ), $"{folder}/{name} in " + string.Join( "\n", files.Keys ) );

			Has( files[$"{folder}/crash.json"], "\"m1.render\"" );
			Has( files[$"{folder}/versions.json"], "\"sha256\": \"cd\"" );
			Assert.IsTrue( files[$"{folder}/versions.json"].IndexOf( "foo.dll" ) < files[$"{folder}/versions.json"].IndexOf( "a.dll" ), "the faulting module first" );
			Has( files[$"{folder}/launch.json"], "\"declared\"" );
			Has( files[$"{folder}/events.txt"], "1000", "the matched events" );
			Has( files[$"{folder}/events.txt"], "bad block", "and the errors of the two minutes before" );
			Has( files[$"{folder}/machine.json"], "Test OS" );

			// the records: the filtered ones - the Other crash is not among them
			var records = files["m1/_crashes.json"];
			Has( records, "m1.render" );
			Assert.IsFalse( records.Contains( "m1.other" ), records );

			// the cover note says what is in each folder
			Has( files["_comment.txt"], "Crashes" );
			Has( files["_comment.txt"], "crash.json" );
		}

		[TestMethod()]
		public async Task ContextFalseGivesOnlyTheDumpsAndDumpsFalseOnlyTheContext()
		{
			var (bed, _, _, _, _) = await Crashed();
			using var _ = bed;

			var only = Read( ( await Download( bed, Crashes( "{\"crashes\":{\"components\":[\"Render\"],\"machines\":[\"m1\"],\"context\":false}}" ) ) ).archives[0].localPath );
			CollectionAssert.AreEquivalent( new[] { "_crashes.json", only.Keys.Single( k => k.EndsWith( "render.dmp" ) ) }, only.Keys.Where( k => k != "_comment.txt" && k != "_sitestate.json" ).ToList() );

			var noDumps = Read( ( await Download( bed, Crashes( "{\"crashes\":{\"components\":[\"Render\"],\"machines\":[\"m1\"],\"dumps\":false}}" ) ) ).archives[0].localPath );
			Assert.IsFalse( noDumps.Keys.Any( k => k.EndsWith( ".dmp" ) ) );
			Assert.IsTrue( noDumps.Keys.Any( k => k.EndsWith( "/crash.json" ) ) );
		}

		[TestMethod()]
		public async Task ADumpBiggerThanTheLimitIsLeftOutAndTheRecordsAndTheCoverNoteSayWhy()
		{
			var (bed, _, _, _, _) = await Crashed();
			using var _ = bed;

			var files = Read( ( await Download( bed, Crashes( "{\"crashes\":{\"components\":[\"Render\"],\"machines\":[\"m1\"],\"maxDumpBytes\":5}}" ) ) ).archives[0].localPath );

			Assert.IsFalse( files.Keys.Any( k => k.EndsWith( ".dmp" ) ), "a dump is never cut" );
			Has( files["_crashes.json"], "left out" );
			Has( files["_comment.txt"], "limit" );
		}

		[TestMethod()]
		public async Task EveryArchiveHasTheRecordsOfItsWindowAndTheSiteStateCountsTheCrashesOfEachApp()
		{
			var (bed, _, _, _, _) = await Crashed();
			using var _ = bed;

			// an archive that did not ask for crashes: the files of one app of m1
			var job = await Download( bed, Crashes( "{\"app\":\"m1.render\"}" ) );
			var files = Read( job.archives[0].localPath );

			Assert.IsTrue( files.ContainsKey( "_crashes.json" ), string.Join( "\n", files.Keys ) );
			Assert.IsFalse( files.Keys.Any( k => k.StartsWith( "crashes/" ) ), "no crash folders unless asked" );

			var records = JsonDocument.Parse( files["_crashes.json"] ).RootElement;
			Assert.AreEqual( "m1", records.GetProperty( "machine" ).GetString() );
			Assert.AreEqual( 2, records.GetProperty( "crashes" ).GetArrayLength(), "every crash of the window of m1, whatever the app" );

			var state = JsonDocument.Parse( files["_sitestate.json"] ).RootElement;
			var apps = state.GetProperty( "apps" ).EnumerateArray().ToDictionary( a => a.GetProperty( "id" ).GetString()! );
			Assert.AreEqual( 1, apps["m1.render"].GetProperty( "crashes" ).GetInt32() );
			Assert.AreEqual( "crash", apps["m1.render"].GetProperty( "lastCrash" ).GetProperty( "kind" ).GetString() );
			Assert.AreEqual( 1, apps["m1.other"].GetProperty( "crashes" ).GetInt32() );
			Assert.AreEqual( 0, apps["m2.render"].GetProperty( "crashes" ).GetInt32(), "m2 did not take part: the count is what m1 says of its own apps" );
		}

		[TestMethod()]
		public async Task AnUntilBeforeSinceInTheCrashSelectionIsRefused()
		{
			var (bed, _, _, _, _) = await Crashed();
			using var _ = bed;

			Exception? failure = null;
			var op = bed.Master.AddSynchronousOp( () =>
			{
				try { bed.Master.DownloadJobs.Start( Crashes( "{\"crashes\":{\"since\":\"2026-11-21T10:00:00Z\",\"until\":\"2026-11-21T09:00:00Z\"}}" ) ); }
				catch( Exception e ) { failure = e; }
			} );
			await op.WaitAsync();

			Assert.IsInstanceOfType( failure, typeof( ArgumentException ) );
		}
	}
}
