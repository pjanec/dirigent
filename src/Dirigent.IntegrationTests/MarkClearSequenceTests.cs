using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// Every combination of Clear, Mark, Unmark and Download, stated as a table.
	/// </summary>
	/// <remarks>
	/// The other tests here take one behaviour each and say why it is right. This one exists so that
	/// nobody has to find out by trying: each row is a sequence of operations and the lines the
	/// archive is expected to hold afterwards, and the whole set is meant to be read as the answer to
	/// "what happens if I do X and then Y".
	///
	/// A script is a list of steps. A bare letter writes that line to the log; `mark`, `clear` and
	/// `unmark` run the operation on the package; `hold` keeps the log open from that point on, the
	/// way a running application does, which is what stops a Clear from emptying it; `rotate` renames
	/// the log aside and lets the next write start a new one. The download happens at the end.
	///
	/// The expectation is the letters the collected log holds, in order - so a row says what an
	/// operator would find in the zip, not which mechanism produced it. Two rows reaching the same
	/// answer by different means is the point: "Clear then run then Download" gives the run's lines
	/// whether the file could be emptied or only marked.
	/// </remarks>
	[TestClass()]
	public class MarkClearSequenceTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 30 );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		// ---- the table --------------------------------------------------------------

		[DataTestMethod]

		// nothing drawn: everything is collected
		[DataRow( "A B", "AB", DisplayName = "no operation at all: the whole log" )]
		[DataRow( "A unmark B", "AB", DisplayName = "unmark without a mark: nothing to undo" )]

		// one operation before the run
		[DataRow( "A mark B", "B", DisplayName = "mark, run: only the run" )]
		[DataRow( "A clear B", "B", DisplayName = "clear, run: only the run" )]
		[DataRow( "A hold clear B", "B", DisplayName = "clear on a log in use: marked instead, same result" )]

		// undoing it
		[DataRow( "A mark B unmark C", "ABC", DisplayName = "unmark after mark: the history is back" )]
		[DataRow( "A hold clear B unmark C", "ABC", DisplayName = "unmark after clear: the history is back" )]
		[DataRow( "A mark B unmark unmark C", "ABC", DisplayName = "unmark twice: the second changes nothing" )]

		// drawing the line again
		[DataRow( "A mark B mark C", "C", DisplayName = "mark twice: the second line wins" )]
		[DataRow( "A hold clear B clear C", "C", DisplayName = "clear twice: the second line wins" )]
		[DataRow( "A mark B hold clear C", "C", DisplayName = "clear after mark: the clear's line wins" )]
		[DataRow( "A hold clear B mark C", "C", DisplayName = "mark after clear: the mark's line wins" )]

		// a cleared file is a new file
		[DataRow( "A clear B mark C", "C", DisplayName = "clear, then mark: the mark cuts the new file" )]
		[DataRow( "A clear B unmark C", "BC", DisplayName = "clear, then unmark: all of the new file" )]

		// rotation, which nobody plans and everybody meets
		[DataRow( "A mark B rotate C", "C", DisplayName = "rotated after a mark: the new file, whole" )]
		[DataRow( "A hold mark B rotate C", "C", DisplayName = "the same for a log that was in use" )]

		public async Task TheSequenceDecidesWhatIsCollected( string script, string expected )
		{
			using var bed = await StartBed();

			await RunScript( bed, script );
			await bed.Operator.DownloadAsync( await Package( bed ), timeout: Timeout );

			Assert.AreEqual( expected, CollectedLines( bed ),
				$"after '{script} download', the archive should hold '{expected}'" );

			// and the invariant that holds through every row of this table
			AssertTheConfigIsUntouched( bed );
		}

		// ---- the rules that are not about one file ----------------------------------

		[TestMethod()]
		public async Task HoldingTheLogIsWhatStopsAClearFromEmptyingIt()
		{
			// The table leans on this: the rows that hold the log exercise the marking half of Clear,
			// the ones that do not exercise the emptying half. If a held file were emptied anyway,
			// half the table would be testing the same path twice and nobody would notice.
			using var bed = await StartBed();

			await RunScript( bed, "A" );

			var free = await bed.Operator.ClearFilesAsync( await Package( bed ), timeout: Timeout );
			Assert.AreEqual( 1, free.Cleared, "a log nobody holds is emptied" );
			Assert.AreEqual( 0, free.Marked );
			Assert.IsFalse( File.Exists( LogPath( bed ) ), "and deleted" );

			await RunScript( bed, "B hold" );

			var held = await bed.Operator.ClearFilesAsync( await Package( bed ), timeout: Timeout );
			Assert.AreEqual( 0, held.Cleared, "a log in use cannot be emptied" );
			Assert.AreEqual( 1, held.Marked, "so a line is drawn under it instead" );
			Assert.IsTrue( File.Exists( LogPath( bed ) ), "and it is still there, with its history" );
		}

		[TestMethod()]
		public async Task DownloadingTwiceGivesTheSameRunTwice()
		{
			// a collection reads the line, it does not consume it - which is what somebody
			// re-downloading after a failed transfer expects
			using var bed = await StartBed();

			await RunScript( bed, "A mark B" );

			await bed.Operator.DownloadAsync( await Package( bed ), timeout: Timeout );
			Assert.AreEqual( "B", CollectedLines( bed ) );

			await bed.Operator.DownloadAsync( await Package( bed ), timeout: Timeout );
			Assert.AreEqual( "B", CollectedLines( bed ), "the second download covers the same run" );

			Assert.AreEqual( 2, Archive.In( bed.DownloadFolder ).Count, "and produced its own archive" );
		}

		[TestMethod()]
		public async Task TwoRunsInARowAreTwoSeparateCollections()
		{
			// the working day this feature exists for
			using var bed = await StartBed();

			await RunScript( bed, "A hold clear B" );
			await bed.Operator.DownloadAsync( await Package( bed ), timeout: Timeout );
			Assert.AreEqual( "B", CollectedLines( bed ), "the first run" );

			await RunScript( bed, "clear C" );
			await bed.Operator.DownloadAsync( await Package( bed ), timeout: Timeout );
			Assert.AreEqual( "C", CollectedLines( bed ), "the second run, and none of the first" );
		}

		[TestMethod()]
		public async Task AQuietLogIsLeftOutOfTheArchiveAndSaidSo()
		{
			// An entry holding nothing would read as a log that is empty, which is not what happened:
			// the file is there and has plenty in it, all of it from before the line.
			using var bed = await StartBed();

			await RunScript( bed, "A hold mark" );

			await bed.Operator.DownloadAsync( await Package( bed ), timeout: Timeout );

			var archive = TheArchive( bed );
			var entries = Archive.EntriesOf( archive );

			Assert.IsFalse( entries.Any( e => e.Contains( "app.since-mark.log", StringComparison.OrdinalIgnoreCase ) ),
				$"no empty entry for a log with nothing new: {string.Join( ", ", entries )}" );

			var report = Archive.TextOf( archive, "_incomplete.txt" );
			StringAssert.Contains( report, "nothing has been written to it",
				$"but the report says the file exists and was quiet:\n{report}" );
			StringAssert.Contains( report, "not in this archive at all", report );

			// the config, which nothing may touch, is in there as always
			AssertTheConfigIsUntouched( bed );
		}

		[TestMethod()]
		public async Task UnmarkWorksOnAFileThatMayNoLongerBeCleared()
		{
			// Unmark ignores Clearable on purpose: it only ever removes a line, which can make a
			// collection more complete but never less. Refusing it on a node whose permission was
			// taken away after it had been marked would leave that line with no way to lift it.
			using var bed = await StartBed();

			await RunScript( bed, "A mark B" );
			Assert.AreEqual( "B", await CollectAndRead( bed ) );

			// the log stops being clearable, as an edited config would have it
			await bed.ReloadSharedConfigAsync( World( clearable: false ) );

			var unmarked = await bed.Operator.UnmarkFilesAsync( await Package( bed ), timeout: Timeout );
			Assert.AreEqual( 1, unmarked.Unmarked, "the line is lifted even so" );

			Assert.AreEqual( "AB", await CollectAndRead( bed ), "and the whole log comes back" );
		}

		[TestMethod()]
		public async Task ClearAndMarkRefuseAFileThatIsNotClearable()
		{
			// the other side of the same coin
			using var bed = await StartBed( clearable: false );

			await RunScript( bed, "A" );

			var cleared = await bed.Operator.ClearFilesAsync( await Package( bed ), timeout: Timeout );
			Assert.AreEqual( 0, cleared.Cleared );
			Assert.AreEqual( 0, cleared.Marked );
			Assert.AreEqual( 2, cleared.Skipped, "the log and the config alike are left alone" );

			var marked = await bed.Operator.MarkFilesAsync( await Package( bed ), timeout: Timeout );
			Assert.AreEqual( 0, marked.Marked );
			Assert.AreEqual( 2, marked.Skipped );

			await RunScript( bed, "B" );
			Assert.AreEqual( "AB", await CollectAndRead( bed ), "so the whole log is collected" );
		}

		// ---- the world --------------------------------------------------------------

		/// <summary>
		/// One machine, one application, one log that may be cleared and one config that may not.
		/// </summary>
		static Scenario World( bool clearable = true )
			=> Scenario.OneMachine()
				.App( "m1.app", a => a
					.LongRunning()
					.WithLogNode( clearable: clearable )
					.WithFileNode( "cfg", "app.cfg", "Config" ) )
				.Seed( "m1.app", "app.cfg", content: TheConfig )
				.Package( "pkg", "Logs/Package", p => p.RefAll( "log" ).RefAll( "cfg" ) );

		const string TheConfig = "the configuration, which nothing may touch\n";

		static Task<TestBed.TestBed> StartBed( bool clearable = true )
			=> TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World( clearable ) } );

		static Task<VfsNodeDef> Package( TestBed.TestBed bed ) => bed.Operator.GetVfsNodeAsync( "pkg" );

		static string LogPath( TestBed.TestBed bed ) => Worlds.LogOf( bed, "m1", "app" );

		/// <summary>The handle a "hold" step opens, released when the bed goes away.</summary>
		readonly List<FileStream> _held = new();

		[TestCleanup()]
		public void ReleaseHeldFiles()
		{
			foreach( var f in _held ) { try { f.Dispose(); } catch {} }
			_held.Clear();
		}

		/// <summary>Runs the steps of a script - see the class remarks for what they mean.</summary>
		async Task RunScript( TestBed.TestBed bed, string script )
		{
			foreach( var step in script.Split( ' ', StringSplitOptions.RemoveEmptyEntries ) )
			{
				switch( step )
				{
					case "mark":
						await bed.Operator.MarkFilesAsync( await Package( bed ), timeout: Timeout );
						break;

					case "clear":
						await bed.Operator.ClearFilesAsync( await Package( bed ), timeout: Timeout );
						break;

					case "unmark":
						await bed.Operator.UnmarkFilesAsync( await Package( bed ), timeout: Timeout );
						break;

					case "hold":
						// as an application writing its log does: readable by the collection, but not
						// openable exclusively, so a Clear cannot empty it
						_held.Add( new FileStream( LogPath( bed ), FileMode.OpenOrCreate, FileAccess.Write,
													FileShare.ReadWrite | FileShare.Delete ) );
						break;

					case "rotate":
						// what a logger does at midnight: the file is moved aside under a new name and
						// the next write starts a fresh one
						ReleaseHeldFiles();
						File.Move( LogPath( bed ), LogPath( bed ) + ".1" );
						break;

					default:
						Assert.AreEqual( 1, step.Length, $"'{step}' is not a step of a script" );
						WriteLine( bed, step );
						break;
				}
			}
		}

		/// <summary>Appends one named line to the log, through the held handle if there is one.</summary>
		void WriteLine( TestBed.TestBed bed, string line )
		{
			var text = Encoding.UTF8.GetBytes( line + "\n" );

			if( _held.Count > 0 )
			{
				var handle = _held[^1];
				handle.Seek( 0, SeekOrigin.End );
				handle.Write( text, 0, text.Length );
				handle.Flush( true );
				return;
			}

			using var file = new FileStream( LogPath( bed ), FileMode.Append, FileAccess.Write );
			file.Write( text, 0, text.Length );
		}

		// ---- reading the answer -----------------------------------------------------

		static string TheArchive( TestBed.TestBed bed )
		{
			var archives = Archive.In( bed.DownloadFolder );
			Assert.IsTrue( archives.Count > 0, $"no archive in {Archive.Describe( bed.DownloadFolder )}" );

			// the newest, so that a test downloading twice reads the right one
			return archives.OrderBy( a => new FileInfo( a ).LastWriteTimeUtc ).Last();
		}

		async Task<string> CollectAndRead( TestBed.TestBed bed )
		{
			await bed.Operator.DownloadAsync( await Package( bed ), timeout: Timeout );
			return CollectedLines( bed );
		}

		/// <summary>
		/// The letters the collected log holds, in order - whatever the entry happens to be called and
		/// whatever header it carries.
		/// </summary>
		static string CollectedLines( TestBed.TestBed bed )
		{
			var archive = TheArchive( bed );

			var entry = Archive.EntriesOf( archive )
					.FirstOrDefault( e => e.Contains( "app.log", StringComparison.OrdinalIgnoreCase )
										|| e.Contains( "app.since-mark.log", StringComparison.OrdinalIgnoreCase ) );

			if( entry is null ) return string.Empty; // the log is not in the archive at all

			var text = Archive.TextOf( archive, entry );

			return string.Concat(
				text.Split( '\n' )
					.Select( l => l.Trim() )
					.Where( l => l.Length == 1 ) ); // the header lines are longer than a letter
		}

		static void AssertTheConfigIsUntouched( TestBed.TestBed bed )
		{
			var onDisk = Path.Combine( bed.RenderContext.AppLogsDir( "m1", "app" ), "app.cfg" );
			Assert.AreEqual( TheConfig, File.ReadAllText( onDisk ),
				"a file that is not Clearable is never emptied" );

			var inArchive = Archive.TextOf( TheArchive( bed ), "app.cfg" );
			Assert.AreEqual( TheConfig, inArchive, "and is always collected whole" );
		}
	}
}
