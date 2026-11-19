using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.TestBed;
using Dirigent.Web;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// Mark, Clear and Unmark as a tool would call them: by file definition or by application, answered with what
	/// happened to which file. Driven through the CLI twins of the REST calls; the machinery underneath is the one
	/// MarkAndClearTests covers from the GUI's side.
	/// </summary>
	[TestClass()]
	public class MarkOperationTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 60 );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		static Task<TestBed.TestBed> StartBed()
			=> TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = Worlds.MarkableWorld() } );

		static async Task<MarkReport> Run( TestBed.TestBed bed, string command, string prefix )
		{
			var lines = await bed.SendCliCommandAsync( command, Timeout );
			Assert.AreEqual( 2, lines.Count, string.Join( " | ", lines ) );
			Assert.AreEqual( "END", lines[1] );
			StringAssert.StartsWith( lines[0], prefix + ":", lines[0] );
			return JsonSerializer.Deserialize<MarkReport>( lines[0].Substring( prefix.Length + 1 ) )!;
		}

		static async Task<FileDefFilesResult> Preview( TestBed.TestBed bed, string fileDefId )
		{
			var lines = await bed.SendCliCommandAsync( $"GetFiles {fileDefId}", Timeout );
			return JsonSerializer.Deserialize<FileDefFilesResult>( lines[0].Substring( $"FILES:{fileDefId}:".Length ) )!;
		}

		[TestMethod()]
		public async Task AMarkListsTheClearableFilesAndSkipsTheOthersAndThePreviewShowsTheLine()
		{
			using var bed = await StartBed();

			var report = await Run( bed, "MarkFiles run.pkg", "MARKED" );

			Assert.AreEqual( "mark", report.operation );
			Assert.AreEqual( "run.pkg", report.selection );
			CollectionAssert.AreEquivalent( new[] { "m1", "m2" }, report.machines.Select( m => m.machine ).ToList() );

			var m1 = report.machines.Single( m => m.machine == "m1" );
			CollectionAssert.AreEqual( new[] { Worlds.LogOf( bed, "m1", "camera" ) }, m1.marked, "the clearable log" );
			CollectionAssert.AreEqual( new[] { Path.Combine( bed.RenderContext.AppLogsDir( "m1", "camera" ), "app.cfg" ) },
				m1.skippedNotClearable, "the config file, which is not" );
			Assert.AreEqual( 0, m1.cleared.Count );
			Assert.IsNull( report.message );

			// the preview of the package says where the line is
			var files = ( await Preview( bed, "run.pkg" ) ).fileDefs.Single().files;
			var log = files.Single( f => f.localPath == Worlds.LogOf( bed, "m1", "camera" ) );
			Assert.IsNotNull( log.markedAt );
			Assert.AreEqual( new FileInfo( Worlds.LogOf( bed, "m1", "camera" ) ).Length, log.markedAtBytes, "where the file ended when it was marked" );
			Assert.IsTrue( DateTime.UtcNow - log.markedAt!.Value.ToUniversalTime() < TimeSpan.FromMinutes( 5 ) );
			Assert.IsNull( files.Single( f => f.localPath!.EndsWith( "app.cfg" ) && f.machine == "m1" ).markedAt, "the config has no line" );

			// and an unmark takes it away
			var unmarked = await Run( bed, "UnmarkFiles run.pkg", "UNMARKED" );
			CollectionAssert.AreEqual( new[] { Worlds.LogOf( bed, "m1", "camera" ) }, unmarked.machines.Single( m => m.machine == "m1" ).unmarked );

			var after = ( await Preview( bed, "run.pkg" ) ).fileDefs.Single().files;
			Assert.IsTrue( after.All( f => f.markedAt is null ), "no line left" );
		}

		[TestMethod()]
		public async Task AClearEmptiesWhatItCanAndTheReportSaysSo()
		{
			using var bed = await StartBed();

			var report = await Run( bed, "ClearFiles run.pkg", "CLEARED" );

			var m1 = report.machines.Single( m => m.machine == "m1" );
			CollectionAssert.AreEqual( new[] { Worlds.LogOf( bed, "m1", "camera" ) }, m1.cleared );
			Assert.IsFalse( File.Exists( Worlds.LogOf( bed, "m1", "camera" ) ) && new FileInfo( Worlds.LogOf( bed, "m1", "camera" ) ).Length > 0, "nobody holds the log, so it is gone" );
		}

		[TestMethod()]
		public async Task AnApplicationIsMarkedByItsOwnFiles()
		{
			using var bed = await StartBed();

			var report = await Run( bed, "MarkAppFiles m1.camera", "MARKED" );

			Assert.AreEqual( "m1.camera", report.selection );
			Assert.AreEqual( "m1", report.machines.Single().machine, "only the machine of the application" );
			CollectionAssert.AreEqual( new[] { Worlds.LogOf( bed, "m1", "camera" ) }, report.machines.Single().marked );
			Assert.AreEqual( 1, report.machines.Single().skippedNotClearable.Count );

			// the other application is untouched
			var other = ( await Preview( bed, "log" ) ).fileDefs.Where( d => d.machine == "m2" ).SelectMany( d => d.files );
			Assert.IsTrue( other.All( f => f.markedAt is null ) );
		}

		[TestMethod()]
		public async Task NothingClearableIsSaidInAMessageAndUnknownThingsAreRefused()
		{
			using var bed = await StartBed();

			// the config files are not Clearable
			var report = await Run( bed, "MarkFiles cfg", "MARKED" );
			Assert.IsTrue( report.machines.All( m => m.marked.Count == 0 ) );
			Assert.IsTrue( report.machines.Sum( m => m.skippedNotClearable.Count ) > 0 );
			StringAssert.Contains( report.message, "Clearable" );

			foreach( var command in new[] { "MarkFiles nosuch", "ClearAppFiles m1.nosuch", "UnmarkAppFiles m1.camera@nosuchplan" } )
			{
				var lines = await bed.SendCliCommandAsync( command, Timeout );
				Assert.AreEqual( 1, lines.Count, string.Join( " | ", lines ) );
				StringAssert.StartsWith( lines[0], "ERROR", command );
			}
		}
	}
}
