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
	/// One selection - file definitions with their filters, and applications - for the preview, the download and the
	/// marking; and the resolver rule that makes a selection of overlapping packages mean what it says: a definition
	/// reached through two references is there twice.
	/// </summary>
	[TestClass()]
	public class FileSelectionTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 60 );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		/// <summary>The markable world (camera on m1, recorder on m2, each with a clearable log) and some packages over it.</summary>
		static Scenario World()
			=> Worlds.MarkableWorld()
				// everything logged
				.RawXml( "<FilePackage Id='pkg.logs' Title='All logs' Groups='component;major'><FileRef Id='log' MachineId='*' AppId='*'/></FilePackage>" )
				// the camera's log alone - which pkg.logs holds as well
				.RawXml( "<FilePackage Id='pkg.cam' Title='Camera' Groups='Component'><FileRef Id='log' MachineId='{m1}' AppId='camera'/></FilePackage>" )
				.RawXml( "<FilePackage Id='pkg.cam2' Title='Camera too' Groups='minor'><FileRef Id='log' MachineId='{m1}' AppId='camera'/></FilePackage>" )
				// two references to one definition in one package
				.RawXml( "<FilePackage Id='pkg.twice' Title='Twice'><FileRef Id='log' MachineId='{m1}' AppId='camera'/><FileRef Id='log' MachineId='{m1}' AppId='camera' Title='again'/></FilePackage>" )
				// two packages naming the same package
				.RawXml( "<FilePackage Id='pkg.outer' Title='Outer'><FileRef Id='pkg.cam'/><FileRef Id='pkg.cam2'/></FilePackage>" )
				// a real cycle: A holds B, B holds A
				.RawXml( "<FilePackage Id='cyc.a' Title='A'><FileRef Id='log' MachineId='{m1}' AppId='camera'/><FileRef Id='cyc.b'/></FilePackage>" )
				.RawXml( "<FilePackage Id='cyc.b' Title='B'><FileRef Id='log' MachineId='{m2}' AppId='recorder'/><FileRef Id='cyc.a'/></FilePackage>" );

		static Task<TestBed.TestBed> StartBed() => TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World() } );

		static Task OnMaster( TestBed.TestBed bed, Action act ) => bed.Master.AddSynchronousOp( act ).WaitAsync();

		static async Task<FileDefFilesResult> Preview( TestBed.TestBed bed, string id )
		{
			var lines = await bed.SendCliCommandAsync( $"GetFiles {id}", Timeout );
			StringAssert.StartsWith( lines[0], $"FILES:{id}:", string.Join( "|", lines ) );
			return JsonSerializer.Deserialize<FileDefFilesResult>( lines[0].Substring( $"FILES:{id}:".Length ) )!;
		}

		static async Task<FilesQueryResult> Query( TestBed.TestBed bed, FileSelectionRequest request )
		{
			FileSelection? selection = null;
			await OnMaster( bed, () => selection = FileSelection.Parse( bed.Master, request ) );
			return await selection!.ResolveAsync( true );
		}

		static List<string> LogPaths( FileDefFilesResult r ) => r.fileDefs.SelectMany( d => d.files ).Select( f => f.localPath! ).ToList();

		// ---- the resolver: an ancestor guard, not a visited list ------------------------------------------

		[TestMethod()]
		public async Task ADefinitionReachedTwiceByOnePackageIsThereTwice()
		{
			using var bed = await StartBed();
			var log = Worlds.LogOf( bed, "m1", "camera" );

			var twice = LogPaths( await Preview( bed, "pkg.twice" ) );
			CollectionAssert.AreEqual( new[] { log, log }, twice, "both references resolve" );
		}

		[TestMethod()]
		public async Task ADefinitionReachedThroughTwoPackagesIsThereTwice()
		{
			using var bed = await StartBed();
			var log = Worlds.LogOf( bed, "m1", "camera" );

			CollectionAssert.AreEqual( new[] { log, log }, LogPaths( await Preview( bed, "pkg.outer" ) ) );
		}

		[TestMethod()]
		public async Task ARealCycleStillEndsAndHoldsEachFileOnce()
		{
			using var bed = await StartBed();

			var a = LogPaths( await Preview( bed, "cyc.a" ) );
			CollectionAssert.AreEquivalent( new[] { Worlds.LogOf( bed, "m1", "camera" ), Worlds.LogOf( bed, "m2", "recorder" ) }, a,
				"A holds its own log and, through B, the recorder's - and B's way back to A is refused" );
		}

		// ---- the selection --------------------------------------------------------------------------------

		[TestMethod()]
		public async Task AnUnknownItemIsNamedAndNothingStartsAndAnEmptySelectionIsRefused()
		{
			using var bed = await StartBed();

			var request = new FileSelectionRequest()
			{
				fileDefs = new() { new DownloadFileDefSelection() { id = "pkg.cam" }, new DownloadFileDefSelection() { id = "no.such.def" } },
				apps = new() { "m1.camera" },
			};

			Exception? failure = null;
			await OnMaster( bed, () => { try { FileSelection.Parse( bed.Master, request ); } catch( Exception e ) { failure = e; } } );
			Assert.IsInstanceOfType( failure, typeof( AppFilesNotFoundException ) );
			StringAssert.Contains( failure!.Message, "no.such.def" );

			var app = new FileSelectionRequest() { apps = new() { "m1.camera", "m1.ghost" } };
			await OnMaster( bed, () => { failure = null; try { FileSelection.Parse( bed.Master, app ); } catch( Exception e ) { failure = e; } } );
			Assert.IsInstanceOfType( failure, typeof( AppFilesNotFoundException ) );
			StringAssert.Contains( failure!.Message, "ghost" );

			await OnMaster( bed, () => { failure = null; try { FileSelection.Parse( bed.Master, new FileSelectionRequest() ); } catch( Exception e ) { failure = e; } } );
			Assert.AreEqual( typeof( ArgumentException ), failure!.GetType(), "an empty selection is a bad request" );

			// the CLI download names the unknown one and starts no job
			var lines = await bed.SendCliCommandAsync( "DownloadPackage pkg.cam no.such.def wait=1", Timeout );
			StringAssert.StartsWith( lines[0], "ERROR", string.Join( "|", lines ) );
			StringAssert.Contains( lines[0], "no.such.def" );
			Assert.AreEqual( 0, bed.Master.DownloadJobs.List().Count, "all the items are looked up before anything runs" );
		}

		[TestMethod()]
		public async Task AQueryPreviewsEachItemAndAddsUpTheTotals()
		{
			using var bed = await StartBed();

			var res = await Query( bed, new FileSelectionRequest()
			{
				fileDefs = new() { new DownloadFileDefSelection() { id = "pkg.logs" }, new DownloadFileDefSelection() { id = "pkg.cam" } },
				apps = new() { "m2.recorder" },
			} );

			Assert.AreEqual( 3, res.items.Count );
			var logs = (FileDefFilesResult) res.items[0];
			var cam = (FileDefFilesResult) res.items[1];
			var rec = (AppFilesResult) res.items[2];
			Assert.AreEqual( "pkg.logs", logs.id );
			Assert.AreEqual( "m2.recorder", rec.app );

			Assert.AreEqual( 2, logs.totals.files );
			Assert.AreEqual( 1, cam.totals.files );
			Assert.AreEqual( 2, rec.totals.files, "the recorder's log and its config" );

			Assert.AreEqual( logs.totals.files + cam.totals.files + rec.totals.files, res.totals.files, "no deduplication: the zip holds each copy" );
			Assert.AreEqual( logs.totals.bytes + cam.totals.bytes + rec.totals.bytes, res.totals.bytes );
			Assert.AreEqual( new FileInfo( Worlds.LogOf( bed, "m1", "camera" ) ).Length, cam.totals.bytes );
			Assert.AreEqual( cam.totals.bytes, cam.totals.bytesToCollect, "nothing marked, nothing cut" );

			// the single route's answer has the totals as well
			var single = await Preview( bed, "pkg.logs" );
			Assert.AreEqual( 2, single.totals.files );
		}

		[TestMethod()]
		public async Task TotalsCountOnlyWhatADownloadWouldTake()
		{
			using var bed = await StartBed();

			await bed.SendCliCommandAsync( "MarkFiles pkg.cam", Timeout );
			var marked = await Preview( bed, "pkg.cam" );
			var f = marked.fileDefs.Single().files.Single();
			Assert.IsNotNull( f.markedAtBytes );
			Assert.AreEqual( f.size, f.markedAtBytes, "the mark is at the end of the file" );
			Assert.AreEqual( 0, marked.totals.bytesToCollect, "everything before the mark is left out" );
			Assert.AreEqual( f.size, marked.totals.bytes );
		}

		[TestMethod()]
		public void TheBytesToCollectFormula()
		{
			Assert.AreEqual( 100, FilesTotals.BytesToCollect( 100, null, null ) );
			Assert.AreEqual( 60, FilesTotals.BytesToCollect( 100, 40, null ), "from the mark on" );
			Assert.AreEqual( 10, FilesTotals.BytesToCollect( 100, null, 10 ), "no more than the tail" );
			Assert.AreEqual( 10, FilesTotals.BytesToCollect( 100, 40, 10 ), "the smaller of the two" );
			Assert.AreEqual( 5, FilesTotals.BytesToCollect( 100, 95, 10 ) );
			Assert.AreEqual( 0, FilesTotals.BytesToCollect( 100, 250, null ), "a file shorter than its mark - never below zero" );
			Assert.AreEqual( 0, FilesTotals.BytesToCollect( null, null, null ), "unknown size counts as nothing" );

			var t = FilesTotals.Of( new[]
			{
				new AppFileInfo() { size = 100, markedAtBytes = 40 },
				new AppFileInfo() { size = 50, truncatedTo = 10 },
				new AppFileInfo() { size = null },
			} );
			Assert.AreEqual( 3, t.files );
			Assert.AreEqual( 150, t.bytes );
			Assert.AreEqual( 70, t.bytesToCollect );
		}

		[TestMethod()]
		public async Task EachPreviewedFileSaysWhichApplicationAndFileDefinitionItCameFrom()
		{
			using var bed = await StartBed();

			// a member of a package: the definition the package referred to, not the package
			var pkg = ( await Preview( bed, "pkg.cam" ) ).fileDefs.Single().files.Single();
			Assert.AreEqual( "log", pkg.fileDefId );
			Assert.AreEqual( "camera", pkg.app );
			Assert.AreEqual( "m1", pkg.machine );

			// a file of a folder scan of the app's own definition
			var lines = await bed.SendCliCommandAsync( "GetAppFiles m1.camera", Timeout );
			var res = JsonSerializer.Deserialize<AppFilesResult>( lines[0].Substring( "APPFILES:m1.camera:".Length ) )!;
			var files = res.fileDefs.SelectMany( d => d.files ).ToList();
			var log = files.Single( x => x.localPath!.EndsWith( "app.log" ) );
			var cfg = files.Single( x => x.localPath!.EndsWith( "app.cfg" ) );
			Assert.AreEqual( "log", log.fileDefId );
			Assert.AreEqual( "cfg", cfg.fileDefId );
			Assert.AreEqual( "camera", log.app );
		}

		[TestMethod()]
		public async Task TheGroupFilterMatchesOneEntryOfTheList()
		{
			using var bed = await StartBed();

			List<FileDefInfo> Ask( string? type, string group )
			{
				var l = new List<FileDefInfo>();
				bed.Master.AddSynchronousOp( () => l = FileDefListing.List( bed.Master, type, null, null, null, group ) ).WaitAsync().Wait();
				return l;
			}

			CollectionAssert.AreEqual( new[] { "pkg.cam", "pkg.logs" }, Ask( "FilePackage", "component" ).Select( x => x.id ).ToList(), "case-insensitive, and one entry of 'component;major'" );
			CollectionAssert.AreEqual( new[] { "pkg.logs" }, Ask( "FilePackage", "major" ).Select( x => x.id ).ToList() );
			CollectionAssert.AreEqual( new[] { "pkg.cam", "pkg.logs" }, Ask( "FilePackage", "comp*" ).Select( x => x.id ).ToList(), "wildcards as for ids" );
			Assert.AreEqual( 0, Ask( "FilePackage", "nothing" ).Count );

			var lines = await bed.SendCliCommandAsync( "GetFileDefs type=FilePackage group=minor", Timeout );
			StringAssert.Contains( lines[0], "pkg.cam2" );
			Assert.IsFalse( lines[0].Contains( "pkg.logs" ) );
		}

		// ---- downloading a selection ----------------------------------------------------------------------

		static async Task<DownloadJobView> Finish( TestBed.TestBed bed, Guid id )
		{
			DownloadJobView? view = null;
			await bed.WaitUntilAsync( async () =>
			{
				view = bed.Master.DownloadJobs.Get( id );
				return await Task.FromResult( view is not null && view.status != "Running" );
			}, Timeout, "the download ends" );
			return view!;
		}

		static async Task<DownloadJobView> Download( TestBed.TestBed bed, DownloadRequest request )
		{
			Guid id = default;
			await OnMaster( bed, () => id = bed.Master.DownloadJobs.Start( request ) );
			var view = await Finish( bed, id );
			Assert.AreEqual( "Finished", view.status, string.Join( "; ", view.errors ) );
			return view;
		}

		[TestMethod()]
		public async Task OneItemInTheSelectionFormMakesTheArchiveOfTheSingleForm()
		{
			using var bed = await StartBed();

			var single = await Download( bed, new DownloadRequest() { fileDef = new DownloadFileDefSelection() { id = "pkg.logs" } } );
			var viaSelection = await Download( bed, new DownloadRequest() { fileDefs = new() { new DownloadFileDefSelection() { id = "pkg.logs" } } } );

			var a = Archive.EntriesOf( single.archives[0].localPath ).Where( e => !e.EndsWith( "_comment.txt" ) ).OrderBy( e => e ).ToList();
			var b = Archive.EntriesOf( viaSelection.archives[0].localPath ).Where( e => !e.EndsWith( "_comment.txt" ) ).OrderBy( e => e ).ToList();
			CollectionAssert.AreEqual( a, b, "the same layout, no extra folder" );
			Assert.IsTrue( a.Count > 0 );

			// an app on its own, too
			var app1 = await Download( bed, new DownloadRequest() { app = "m1.camera" } );
			var app2 = await Download( bed, new DownloadRequest() { apps = new() { "m1.camera" } } );
			CollectionAssert.AreEqual(
				Archive.EntriesOf( app1.archives[0].localPath ).Where( e => !e.EndsWith( "_comment.txt" ) ).OrderBy( e => e ).ToList(),
				Archive.EntriesOf( app2.archives[0].localPath ).Where( e => !e.EndsWith( "_comment.txt" ) ).OrderBy( e => e ).ToList() );
		}

		[TestMethod()]
		public async Task SeveralItemsMakeAFolderEachAndASharedFileIsInBoth()
		{
			using var bed = await StartBed();

			// pkg.logs holds the camera's log and the recorder's; pkg.cam holds the camera's again
			var job = await Download( bed, new DownloadRequest()
			{
				fileDefs = new() { new DownloadFileDefSelection() { id = "pkg.logs" }, new DownloadFileDefSelection() { id = "pkg.cam" } },
				comment = "two packages",
			} );

			var entries = Archive.EntriesOf( job.archives[0].localPath );
			var text = string.Join( ", ", entries );

			var camera = entries.Where( e => e.Contains( "m1/" ) && e.EndsWith( "app.log" ) ).ToList();
			Assert.AreEqual( 2, camera.Count, text );
			Assert.IsTrue( camera.Any( e => e.Contains( "/All logs/" ) ), text );
			Assert.IsTrue( camera.Any( e => e.Contains( "/Camera/" ) ), text );
			Assert.AreEqual( 1, entries.Count( e => e.Contains( "m2/" ) && e.EndsWith( "app.log" ) ), text );

			var comment = Archive.TextOf( job.archives[0].localPath, "_comment.txt" );
			StringAssert.Contains( comment, "pkg.logs" );
			StringAssert.Contains( comment, "pkg.cam" );
			StringAssert.Contains( comment, "two packages" );

			// the job file echoes what was asked
			var echoed = bed.Master.DownloadJobs.Get( Guid.Parse( job.id ) )!.request;
			Assert.AreEqual( 2, echoed.fileDefs!.Count );
		}

		[TestMethod()]
		public async Task TheSameTitleTwiceGetsASuffixAndTheCliTakesSeveralIds()
		{
			using var bed = await StartBed();

			// pkg.cam and pkg.cam2 are titled differently; the same package twice is the same title twice
			var lines = await bed.SendCliCommandAsync( "DownloadPackage pkg.cam pkg.cam wait=1", Timeout );
			Assert.AreEqual( "END", lines[1], string.Join( "|", lines ) );
			var job = JsonSerializer.Deserialize<DownloadJobView>( lines[0].Substring( lines[0].IndexOf( '{' ) ) )!;
			Assert.AreEqual( "Finished", job.status, string.Join( "; ", job.errors ) );

			var entries = Archive.EntriesOf( job.archives[0].localPath );
			var text = string.Join( ", ", entries );
			Assert.IsTrue( entries.Any( e => e.StartsWith( "Camera/" ) && e.EndsWith( "app.log" ) ), text );
			Assert.IsTrue( entries.Any( e => e.StartsWith( "Camera (2)/" ) && e.EndsWith( "app.log" ) ), text );
		}

		// ---- marking a selection --------------------------------------------------------------------------

		[TestMethod()]
		public async Task AMarkOverTwoPackagesAndAnAppIsOneOperationOverTheUnion()
		{
			using var bed = await StartBed();

			MarkReport? report = null;
			await OnMaster( bed, () =>
			{
				var sel = FileSelection.Parse( bed.Master, new FileSelectionRequest()
				{
					fileDefs = new() { new DownloadFileDefSelection() { id = "pkg.logs" }, new DownloadFileDefSelection() { id = "pkg.cam" } },
					apps = new() { "m1.camera" },
				} );
				_op = MarkOperation.StartForSelection( bed.Master, MarkOperation.EKind.Mark, sel );
			} );

			await bed.WaitUntilAsync( async () =>
			{
				Dirigent.ScriptState? st = null;
				await OnMaster( bed, () => st = _op!.State( bed.Master ) );
				if( st is null || st.IsAlive ) return false;
				report = _op!.ReportOf( st );
				return true;
			}, Timeout, "the marking ends" );

			Assert.AreEqual( "mark", report!.operation );
			StringAssert.Contains( report.selection, "pkg.logs" );
			StringAssert.Contains( report.selection, "pkg.cam" );
			StringAssert.Contains( report.selection, "m1.camera" );

			var m1 = report.machines.Single( m => m.machine == "m1" );
			CollectionAssert.AreEqual( new[] { Worlds.LogOf( bed, "m1", "camera" ) }, m1.marked, "the camera's log is marked once, whoever reached it" );
			var m2 = report.machines.Single( m => m.machine == "m2" );
			CollectionAssert.AreEqual( new[] { Worlds.LogOf( bed, "m2", "recorder" ) }, m2.marked );
		}

		MarkOperation? _op;

		[TestMethod()]
		public async Task TheCliMarksSeveralIdsAndSeveralApps()
		{
			using var bed = await StartBed();

			var lines = await bed.SendCliCommandAsync( "MarkFiles pkg.cam pkg.logs", Timeout );
			Assert.AreEqual( "END", lines[1], string.Join( "|", lines ) );
			var report = JsonSerializer.Deserialize<MarkReport>( lines[0].Substring( "MARKED:".Length ) )!;
			Assert.AreEqual( 2, report.machines.Sum( m => m.marked.Count ) );

			lines = await bed.SendCliCommandAsync( "ClearAppFiles m1.camera m2.recorder", Timeout );
			report = JsonSerializer.Deserialize<MarkReport>( lines[0].Substring( "CLEARED:".Length ) )!;
			Assert.AreEqual( 2, report.machines.Sum( m => m.cleared.Count ) );

			lines = await bed.SendCliCommandAsync( "UnmarkFiles pkg.cam nosuch", Timeout );
			StringAssert.StartsWith( lines[0], "ERROR" );
			StringAssert.Contains( lines[0], "nosuch" );
		}
	}
}
