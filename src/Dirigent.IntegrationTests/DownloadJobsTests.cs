using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
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
	/// Download jobs of the master: a collection requested by a diagnostic tool, built into a folder
	/// of its own, listed, fetched, deleted and eventually cleaned up. Driven through the CLI twins of
	/// the REST endpoints and through the manager itself; the HTTP skin is smoked in the system tests.
	/// </summary>
	[TestClass()]
	public class DownloadJobsTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 60 );

		string _root = string.Empty;

		[TestInitialize()]
		public void SetUp()
		{
			Diagnostics.ClearLog();
			_root = Path.Combine( Path.GetTempPath(), "DirigentDownloadJobs_" + Guid.NewGuid().ToString( "N" ) );
			Directory.CreateDirectory( _root );
		}

		[TestCleanup()]
		public void TearDown()
		{
			try { Directory.Delete( _root, true ); } catch { }
		}

		static string SystemDriveRoot => Path.GetPathRoot( Path.GetTempPath() )!;

		/// <summary>m1 has a share; the application, its plan and a package are declared by hand.</summary>
		static Scenario ShareWorld()
			=> Scenario.TwoMachines()
				.Share( "m1", "C", SystemDriveRoot )
				.App( "m1.a", a => a.LongRunning().RawXml(
					"<Folder Id='logs' Title='Logs' Path='{applogs}' Mask='*.log'/>" ) )
				.RawXml(
					"<Plan Name='planX'><App AppIdTuple='{m1}.a' ExeFullPath='{testapp}' CmdLineArgs='--run-forever' StartupDir='{temp}'>"
					+ "<Folder Id='plannode' Title='PlanLogs' Path='{temp}\\planfiles' Mask='*.log'/></App></Plan>" )
				.RawXml(
					"<FilePackage Id='pkg' Title='Pkg' Description='Collect me'>"
					+ "<Folder Id='pk1' Title='M1' MachineId='{m1}' Path='{temp}\\planfiles' Mask='*.log'/></FilePackage>" )
				.RawXml( "<File Id='single' Title='Single' MachineId='{m1}' Path='{temp}\\single\\one.txt'/>" )
				.RawXml( "<File Id='tailed' Title='Tailed' MachineId='{m1}' Path='{temp}\\single\\big.txt' TailBytes='10'/>" );

		static async Task<TestBed.TestBed> StartShareBed()
		{
			var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = ShareWorld() } );
			Directory.CreateDirectory( Path.Combine( bed.TempRoot, "planfiles" ) );
			File.WriteAllText( Path.Combine( bed.TempRoot, "planfiles", "p.log" ), "plan file" );
			File.WriteAllText( Path.Combine( bed.RenderContext.AppLogsDir( "m1", "a" ), "default.log" ), "default" );
			return bed;
		}

		static DownloadJobView ParseDownload( string line )
		{
			StringAssert.StartsWith( line, "DOWNLOAD:", line );
			var json = line.Substring( line.IndexOf( '{' ) );
			return JsonSerializer.Deserialize<DownloadJobView>( json )!;
		}

		static async Task<DownloadJobView> Run( TestBed.TestBed bed, string command )
		{
			var lines = await bed.SendCliCommandAsync( command, Timeout );
			Assert.AreEqual( 2, lines.Count, string.Join( " | ", lines ) );
			Assert.AreEqual( "END", lines[1] );
			return ParseDownload( lines[0] );
		}

		// ---- the jobs themselves ------------------------------------------------------------------

		[TestMethod()]
		public async Task APackageJobFinishesAndItsArchiveCanBeFetched()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = Worlds.LoggingWorld() } );

			var job = await Run( bed, "DownloadPackage logs.all wait=1 \"comment=for the test\"" );

			Assert.AreEqual( "Finished", job.status, string.Join( "; ", job.errors ) );
			Assert.AreEqual( 1, job.files.Count );

			var folder = Path.Combine( bed.Master.DownloadJobs.ArchiveFolder, job.id );
			Assert.IsTrue( File.Exists( Path.Combine( folder, "job.json" ) ), "the job is on disk" );
			Assert.AreEqual( Path.Combine( folder, job.files[0].name ), job.files[0].localPath );
			Assert.IsTrue( File.Exists( job.files[0].localPath ), "the zip is in the job folder" );
			Assert.IsTrue( job.files[0].size > 0 );

			// no share declared in this world: said so, not guessed
			Assert.IsNull( job.files[0].uncPath );
			Assert.IsFalse( string.IsNullOrEmpty( job.files[0].uncError ) );

			var entries = Archive.EntriesOf( job.files[0].localPath );
			Assert.IsTrue( Archive.HasEntryMatching( entries, "m1/", "camera/", "recent.log" ), string.Join( ", ", entries ) );
			Assert.IsTrue( Archive.HasEntryMatching( entries, "m2/", "recorder/", "recent.log" ), string.Join( ", ", entries ) );

			// what the status says it made is what the manager serves, byte for byte
			var served = bed.Master.DownloadJobs.FileToServe( Guid.Parse( job.id ), job.files[0].name );
			CollectionAssert.AreEqual( File.ReadAllBytes( job.files[0].localPath ), File.ReadAllBytes( served ) );

			// and the CLI twin of the status says the same
			var lines = await bed.SendCliCommandAsync( $"GetDownload {job.id}", Timeout );
			Assert.AreEqual( "Finished", ParseDownload( lines[0] ).status );
		}

		[TestMethod()]
		public async Task AnAppJobCollectsTheFilesOfThePlansDefinitionAndGivesUncPaths()
		{
			using var bed = await StartShareBed();

			var job = await Run( bed, "DownloadApp m1.a@planX wait=1" );

			Assert.AreEqual( "Finished", job.status, string.Join( "; ", job.errors ) );
			Assert.AreEqual( "m1.a@planX", job.request.app );

			var entries = Archive.EntriesOf( job.files.Single().localPath );
			Assert.IsTrue( Archive.HasEntryMatching( entries, "p.log" ), string.Join( ", ", entries ) );
			Assert.IsFalse( Archive.HasEntryMatching( entries, "default.log" ), "the plan's definition, not the default one" );

			var rel = job.files[0].localPath.Substring( SystemDriveRoot.Length );
			Assert.AreEqual( @"\\127.0.0.1\C\" + rel, job.files[0].uncPath, "through the share of the master's machine" );
		}

		[TestMethod()]
		public async Task ASingleFileDefinitionIsCollectedAsThatFileAlone()
		{
			using var bed = await StartShareBed();

			var dir = Path.Combine( bed.TempRoot, "single" );
			Directory.CreateDirectory( dir );
			var content = "exactly this content\r\nsecond line\r\n";
			File.WriteAllText( Path.Combine( dir, "one.txt" ), content );
			File.WriteAllText( Path.Combine( dir, "other.txt" ), "must not come along" );
			var big = string.Concat( Enumerable.Range( 0, 10 ).Select( i => $"L{i}\n" ) ); // 30 bytes; the last 10 start mid-line, so the tail begins at the next line
			File.WriteAllText( Path.Combine( dir, "big.txt" ), big );

			// a File with nothing else said: the whole file
			var job = await Run( bed, "DownloadPackage single wait=1" );
			Assert.AreEqual( "Finished", job.status, string.Join( "; ", job.errors ) );

			using( var zip = System.IO.Compression.ZipFile.OpenRead( job.files.Single().localPath ) )
			{
				var files = zip.Entries.Where( e => !e.FullName.EndsWith( "/" ) && !e.Name.StartsWith( "_" ) ).ToList();
				Assert.AreEqual( 1, files.Count, string.Join( ", ", zip.Entries.Select( e => e.FullName ) ) );
				Assert.AreEqual( "one.txt", files[0].Name );

				using var reader = new StreamReader( files[0].Open() );
				Assert.AreEqual( content, reader.ReadToEnd() );
			}

			// the same with TailBytes smaller than the file: the end of it
			var tailJob = await Run( bed, "DownloadPackage tailed wait=1" );
			Assert.AreEqual( "Finished", tailJob.status, string.Join( "; ", tailJob.errors ) );

			using( var zip = System.IO.Compression.ZipFile.OpenRead( tailJob.files.Single().localPath ) )
			{
				var files = zip.Entries.Where( e => !e.FullName.EndsWith( "/" ) && !e.Name.StartsWith( "_" ) ).ToList();
				Assert.AreEqual( 1, files.Count, string.Join( ", ", zip.Entries.Select( e => e.FullName ) ) );
				Assert.AreEqual( "big.last10B.txt", files[0].Name, "a tail is named as one" );

				using var ms = new MemoryStream();
				using( var s = files[0].Open() ) s.CopyTo( ms );
				var text = System.Text.Encoding.UTF8.GetString( ms.ToArray() );
				// a partial file starts with a header saying so (by design), then the tail from the first
				// line boundary within the last 10 bytes
				Assert.IsTrue( text.EndsWith( "L7\nL8\nL9\n" ), $"it ends with the tail of the file, got: {text}" );
				Assert.IsFalse( text.Contains( "L6" ) || text.Contains( "L0" ), $"and holds nothing before the tail, got: {text}" );
			}
		}

		[TestMethod()]
		public async Task AnUnknownNodeOrAppIsRefused()
		{
			using var bed = await StartShareBed();

			foreach( var command in new[] { "DownloadPackage nosuch", "DownloadApp m1.nosuch", "DownloadApp m1.a@nosuchplan" } )
			{
				var lines = await bed.SendCliCommandAsync( command, Timeout );
				Assert.AreEqual( 1, lines.Count, string.Join( " | ", lines ) );
				StringAssert.StartsWith( lines[0], "ERROR", command );
			}
		}

		[TestMethod()]
		public async Task DeletingAJobRemovesItsFolder()
		{
			using var bed = await StartShareBed();

			var job = await Run( bed, "DownloadPackage pkg wait=1" );
			var folder = Path.Combine( bed.Master.DownloadJobs.ArchiveFolder, job.id );
			Assert.IsTrue( Directory.Exists( folder ) );

			var lines = await bed.SendCliCommandAsync( $"DeleteDownload {job.id}", Timeout );
			Assert.AreEqual( "ACK", lines.Single() );
			Assert.IsFalse( Directory.Exists( folder ), "the job folder is gone" );

			var again = await bed.SendCliCommandAsync( $"DeleteDownload {job.id}", Timeout );
			StringAssert.StartsWith( again.Single(), "ERROR" );

			var unknown = await bed.SendCliCommandAsync( $"GetDownload {Guid.NewGuid()}", Timeout );
			StringAssert.StartsWith( unknown.Single(), "ERROR" );
		}

		[TestMethod()]
		public async Task OnlyTheFilesOfTheJobAreServed()
		{
			using var bed = await StartShareBed();

			var job = await Run( bed, "DownloadPackage pkg wait=1" );
			var id = Guid.Parse( job.id );

			foreach( var name in new[] { "job.json", @"..\job.json", "../x.zip", "other.zip", "" } )
			{
				var e = Assert.ThrowsException<DownloadJobException>(
					() => bed.Master.DownloadJobs.FileToServe( id, name ), name );
				Assert.AreEqual( EDownloadJobProblem.NotFound, e.Problem, name );
			}

			Assert.IsNotNull( bed.Master.DownloadJobs.FileToServe( id, job.files[0].name ) );
		}

		[TestMethod()]
		public void ServingCanBeSwitchedOffAndARunningJobIsNotServed()
		{
			var id = Guid.NewGuid();
			var folder = Path.Combine( _root, id.ToString() );
			Directory.CreateDirectory( folder );
			File.WriteAllText( Path.Combine( folder, "a.zip" ), "zip" );
			File.WriteAllText( Path.Combine( folder, "job.json" ),
				$"{{\"id\":\"{id}\",\"status\":\"Finished\",\"created\":\"{DateTime.UtcNow:o}\",\"files\":[\"a.zip\"]}}" );

			var off = new DownloadJobs( _root, 24, 0, serveArchives: false );
			Assert.AreEqual( EDownloadJobProblem.Forbidden,
				Assert.ThrowsException<DownloadJobException>( () => off.FileToServe( id, "a.zip" ) ).Problem );
			Assert.AreEqual( "Finished", off.Get( id )!.status, "listing and status still work" );

			File.WriteAllText( Path.Combine( folder, "job.json" ),
				$"{{\"id\":\"{id}\",\"status\":\"Running\",\"created\":\"{DateTime.UtcNow:o}\",\"files\":[\"a.zip\"]}}" );
			var busy = new DownloadJobs( _root, 24, 0, serveArchives: true, isRunningExtra: g => g == id );
			Assert.AreEqual( EDownloadJobProblem.Conflict,
				Assert.ThrowsException<DownloadJobException>( () => busy.FileToServe( id, "a.zip" ) ).Problem );

			// a running job has no files listed yet; whatever name is asked for, the answer is "not yet"
			Assert.AreEqual( EDownloadJobProblem.Conflict,
				Assert.ThrowsException<DownloadJobException>( () => busy.FileToServe( id, "not-listed.zip" ) ).Problem );
		}

		// ---- cleaning up --------------------------------------------------------------------------

		Guid FabricateJob( string status, DateTime? finishedUtc, int bytes = 10, string? name = null, bool withJobJson = true )
		{
			var id = Guid.NewGuid();
			var folder = Path.Combine( _root, name ?? id.ToString() );
			Directory.CreateDirectory( folder );
			File.WriteAllBytes( Path.Combine( folder, "a.zip" ), new byte[bytes] );

			if( withJobJson )
			{
				var created = ( finishedUtc ?? DateTime.UtcNow ).AddMinutes( -5 );
				var fin = finishedUtc is null ? "null" : $"\"{finishedUtc.Value:o}\"";
				File.WriteAllText( Path.Combine( folder, "job.json" ),
					$"{{\"id\":\"{id}\",\"status\":\"{status}\",\"created\":\"{created:o}\",\"finished\":{fin},\"files\":[\"a.zip\"]}}" );
			}
			return id;
		}

		bool Exists( string name ) => Directory.Exists( Path.Combine( _root, name ) );

		[TestMethod()]
		public void CleanupRemovesOnlyOldFinishedJobsItOwns()
		{
			var old = FabricateJob( "Finished", DateTime.UtcNow.AddHours( -48 ) );
			var fresh = FabricateJob( "Finished", DateTime.UtcNow.AddHours( -1 ) );
			var running = FabricateJob( "Running", null );
			var runningOld = Guid.NewGuid();
			FabricateJob( "Running", DateTime.UtcNow.AddHours( -48 ), name: runningOld.ToString() ); // "running", but nobody runs it: an orphan... except it is running now
			var orphan = FabricateJob( "Running", DateTime.UtcNow.AddHours( -72 ) ); // orphaned, old: goes
			var notAGuid = "photos";
			FabricateJob( "Finished", DateTime.UtcNow.AddHours( -48 ), name: notAGuid );
			var noJobJson = Guid.NewGuid();
			FabricateJob( "Finished", DateTime.UtcNow.AddHours( -48 ), name: noJobJson.ToString(), withJobJson: false );

			var jobs = new DownloadJobs( _root, 24, 0, serveArchives: true, isRunningExtra: g => g == runningOld || g == running );

			jobs.Cleanup();

			Assert.IsFalse( Exists( old.ToString() ), "old and finished: removed" );
			Assert.IsFalse( Exists( orphan.ToString() ), "old and orphaned: removed" );
			Assert.IsTrue( Exists( fresh.ToString() ), "fresh: kept" );
			Assert.IsTrue( Exists( running.ToString() ), "running now: kept" );
			Assert.IsTrue( Exists( runningOld.ToString() ), "running now, however old: kept" );
			Assert.IsTrue( Exists( notAGuid ), "not named by a guid: not ours" );
			Assert.IsTrue( Exists( noJobJson.ToString() ), "a guid, but no job.json: not ours" );
		}

		[TestMethod()]
		public void AJobWhoseArchiveIsInUseStaysOursUntilANextPassRemovesIt()
		{
			var old = FabricateJob( "Finished", DateTime.UtcNow.AddHours( -48 ) );
			var folder = Path.Combine( _root, old.ToString() );
			var jobs = new DownloadJobs( _root, 24, 0, serveArchives: true );

			// someone still copying an archive whose name sorts after job.json, as "logs.all_....zip" does
			var archive = Path.Combine( folder, "logs.all_2026-11-19.zip" );
			File.WriteAllBytes( archive, new byte[10] );
			using( new FileStream( archive, FileMode.Open, FileAccess.Read, FileShare.Read ) )
			{
				jobs.Cleanup();
			}

			Assert.IsTrue( File.Exists( Path.Combine( folder, "job.json" ) ), "half-deleted, but still recognisable as ours" );

			jobs.Cleanup();

			Assert.IsFalse( Exists( old.ToString() ), "the next pass finishes the job" );
		}

		[TestMethod()]
		public void WhenTheTotalIsOverTheCapTheOldestFinishedGoFirst()
		{
			var oldest = FabricateJob( "Finished", DateTime.UtcNow.AddHours( -3 ), bytes: 1000 );
			var middle = FabricateJob( "Finished", DateTime.UtcNow.AddHours( -2 ), bytes: 1000 );
			var newest = FabricateJob( "Finished", DateTime.UtcNow.AddHours( -1 ), bytes: 1000 );

			// job.json files count too: about 3.6 kB in all, room for two jobs but not for three
			var jobs = new DownloadJobs( _root, 24, maxBytes: 2500, serveArchives: true );

			jobs.Cleanup();

			Assert.IsFalse( Exists( oldest.ToString() ), "the oldest went first" );
			Assert.IsTrue( Exists( middle.ToString() ) );
			Assert.IsTrue( Exists( newest.ToString() ) );
		}

		// ---- listing the nodes --------------------------------------------------------------------

		[TestMethod()]
		public async Task NodesAreListedWithTheirDescriptionAndFilteredByType()
		{
			using var bed = await StartShareBed();

			var lines = await bed.SendCliCommandAsync( "GetFileDefs type=filepackage", Timeout );
			Assert.AreEqual( 1, lines.Count );
			StringAssert.StartsWith( lines[0], "FILEDEFS:" );

			var nodes = JsonSerializer.Deserialize<List<FileDefInfo>>( lines[0].Substring( "FILEDEFS:".Length ) )!;
			var pkg = nodes.Single();
			Assert.AreEqual( "pkg", pkg.id );
			Assert.AreEqual( "FilePackage", pkg.type );
			Assert.AreEqual( "Pkg", pkg.title );
			Assert.AreEqual( "Collect me", pkg.description );

			var all = JsonSerializer.Deserialize<List<FileDefInfo>>(
				( await bed.SendCliCommandAsync( "GetFileDefs id=p*g", Timeout ) )[0].Substring( "FILEDEFS:".Length ) )!;
			Assert.AreEqual( "pkg", all.Single().id, "wildcards as in ListVfsNodes" );
		}
	}
}
