using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System;
using System.IO;
using System.Text;

namespace Dirigent.Tests
{
	/// <summary>
	/// The high-water marks that turn "collect the logs" into "collect this one test run".
	/// </summary>
	/// <remarks>
	/// The rules worth pinning down here are the ones about a mark that no longer describes the file
	/// it was made on - rotation, truncation, replacement. Those decide whether an archive holds too
	/// much or nothing at all, and they are not observable from the outside once a collection has run.
	/// </remarks>
	[TestClass()]
	public class FileMarkStoreTests
	{
		string _folder = "";

		[TestInitialize()]
		public void SetUp()
		{
			_folder = Path.Combine( Path.GetTempPath(), "DirigentMarkTests_" + Guid.NewGuid().ToString( "N" ) );
			Directory.CreateDirectory( _folder );
		}

		[TestCleanup()]
		public void TearDown()
		{
			try { Directory.Delete( _folder, true ); } catch {}
		}

		string WriteFile( string name, string content )
		{
			var path = Path.Combine( _folder, name );
			File.WriteAllText( path, content );
			return path;
		}

		FileMarkStore Store( string machineId = "m1" ) => new FileMarkStore( machineId, _folder );

		[TestMethod()]
		public void MarkSurvivesARestartTest()
		{
			// the point of the file on disk: the mark is made by one script and read by another, on a
			// machine whose agent may be restarted in between
			var path = WriteFile( "app.log", "one\ntwo\n" );

			var store = Store();
			var mark = store.MarkFile( path );
			Assert.IsNotNull( mark );
			Assert.AreEqual( 8L, mark!.Offset, "the length at the moment of marking" );
			store.Save();

			var reloaded = Store();
			var back = reloaded.Get( path );
			Assert.IsNotNull( back, "a mark not found after a restart would collect the whole history" );
			Assert.AreEqual( 8L, back!.Offset );
		}

		[TestMethod()]
		public void MarksOfDifferentMachinesDoNotMixTest()
		{
			// several agents share one folder in the test bed, and would share one file if the machine
			// id were not part of its name
			var path = WriteFile( "app.log", "x\n" );

			var m1 = Store( "m1" );
			m1.MarkFile( path );
			m1.Save();

			Assert.IsNull( Store( "m2" ).Get( path ), "m2 must not see what m1 marked" );
		}

		[TestMethod()]
		public void PathsAreMatchedIgnoringCaseTest()
		{
			// the mark is made from a resolved path and read back from another resolution of the same
			// node; on Windows those can differ in case
			var path = WriteFile( "app.log", "x\n" );

			var store = Store();
			store.MarkFile( path );

			Assert.IsNotNull( store.Get( path.ToUpperInvariant() ) );
		}

		[TestMethod()]
		public void UnmarkedFileIsCollectedWholeTest()
		{
			var path = WriteFile( "app.log", "one\ntwo\n" );

			var (offset, note) = Store().WhereToStart( path, 8, File.GetCreationTimeUtc( path ) );

			Assert.AreEqual( 0L, offset );
			Assert.IsNull( note, "nothing to explain about a file collected whole" );
		}

		[TestMethod()]
		public void CollectionStartsAtTheMarkTest()
		{
			var path = WriteFile( "app.log", "before\n" );
			var store = Store();
			store.MarkFile( path );

			File.AppendAllText( path, "after\n" );
			var info = new FileInfo( path );

			var (offset, note) = store.WhereToStart( path, info.Length, info.CreationTimeUtc );

			Assert.AreEqual( 7L, offset, "only what was written after the mark" );
			StringAssert.Contains( note!, "mark", "and the entry says so" );
		}

		[TestMethod()]
		public void AReplacedFileIsCollectedWholeTest()
		{
			// what rotation looks like from here: app.log is renamed away and a fresh one takes its
			// place, so the mark's offset points into a file that no longer exists. Collecting the
			// whole of the new file gives slightly more than the run rather than nothing of it.
			//
			// Note what does NOT catch this: the creation time. NTFS tunneling puts the original one
			// back on a file recreated under the same name within about fifteen seconds, which is
			// precisely the rotation case - so this test would pass on the creation time check alone
			// only by luck; the bytes before the mark are what actually notice.
			var path = WriteFile( "app.log", "the marked file\n" );
			var store = Store();
			store.MarkFile( path );

			File.Delete( path );
			System.Threading.Thread.Sleep( 30 );
			File.WriteAllText( path, "a fresh file, longer than the one that was marked\n" );

			var info = new FileInfo( path );
			var (offset, note) = store.WhereToStart( path, info.Length, info.CreationTimeUtc );

			Assert.AreEqual( 0L, offset, "the mark describes a file that is gone" );
			StringAssert.Contains( note!, "replaced", "and the archive has to say why it holds more" );
		}

		[TestMethod()]
		public void AReplacementOfTheSameLengthIsNoticedTest()
		{
			// the case nothing but the content can catch: same name, same creation time (tunneling),
			// same length - and every byte of it belongs to a different run. Starting at the mark
			// would hand over a slice of the middle of an unrelated file as if it were the test run.
			var path = WriteFile( "app.log", "the run that was marked\n" );
			var store = Store();
			store.MarkFile( path );

			File.Delete( path );
			File.WriteAllText( path, "a different file, same len\n" );
			File.AppendAllText( path, "and something after it\n" );

			var info = new FileInfo( path );
			var (offset, note) = store.WhereToStart( path, info.Length, info.CreationTimeUtc );

			Assert.AreEqual( 0L, offset, "a mark is only worth its offset while the file behind it is the same" );
			StringAssert.Contains( note!, "replaced" );
		}

		[TestMethod()]
		public void ATruncatedFileIsCollectedWholeTest()
		{
			// somebody emptied the file behind our back: the mark is past the end of it now, and
			// starting there would collect nothing at all
			var path = WriteFile( "app.log", "a long first run\n" );
			var store = Store();
			store.MarkFile( path );

			File.WriteAllText( path, "short\n" );

			var info = new FileInfo( path );
			var (offset, note) = store.WhereToStart( path, info.Length, info.CreationTimeUtc );

			Assert.AreEqual( 0L, offset );
			StringAssert.Contains( note!, "truncated" );
		}

		[TestMethod()]
		public void MarkOfAnEmptyFileSaysNothingTest()
		{
			// a mark at byte zero is the same as no mark; a header explaining that the entry starts at
			// byte 0 would only puzzle whoever reads it
			var path = WriteFile( "app.log", "" );
			var store = Store();
			store.MarkFile( path );

			File.AppendAllText( path, "the run\n" );
			var info = new FileInfo( path );

			var (offset, note) = store.WhereToStart( path, info.Length, info.CreationTimeUtc );

			Assert.AreEqual( 0L, offset );
			Assert.IsNull( note );
		}

		[TestMethod()]
		public void UnmarkRestoresTheWholeHistoryTest()
		{
			var path = WriteFile( "app.log", "before\n" );
			var store = Store();
			store.MarkFile( path );
			File.AppendAllText( path, "after\n" );

			Assert.IsTrue( store.Unmark( path ) );
			Assert.IsFalse( store.Unmark( path ), "and says when there was nothing to drop" );

			var info = new FileInfo( path );
			Assert.AreEqual( 0L, store.WhereToStart( path, info.Length, info.CreationTimeUtc ).Offset );
		}

		[TestMethod()]
		public void MarkingAFileThatIsNotThereFailsQuietlyTest()
		{
			// a log not written yet: nothing to mark, and nothing needs to happen either - with no
			// mark the collection takes the file whole, which is what the run produced
			Assert.IsNull( Store().MarkFile( Path.Combine( _folder, "never-written.log" ) ) );
		}

		[TestMethod()]
		public void ADamagedStoreMeansNoMarksTest()
		{
			// a store that cannot be read must not fail a collection: no marks is the same state as
			// nothing having been marked yet
			var path = WriteFile( "app.log", "x\n" );
			File.WriteAllText( FileMarkStore.GetFilePath( "m1", _folder ), "{ this is not json" );

			var store = Store();

			Assert.IsNull( store.Get( path ) );
			Assert.IsNotNull( store.MarkFile( path ), "and marking still works afterwards" );
		}

		[TestMethod()]
		public void MarkedFileIsCutAtALineBoundaryTest()
		{
			// the two halves of the feature meeting: the store says where the run begins, FileTail
			// makes sure the entry does not start mid-line
			var path = WriteFile( "app.log", "first line\nsecond li" );
			var store = Store();
			store.MarkFile( path ); // 20 bytes, in the middle of a line

			File.AppendAllText( path, "ne\nthird line\n" );

			var info = new FileInfo( path );
			var (offset, _) = store.WhereToStart( path, info.Length, info.CreationTimeUtc );

			using var stream = File.OpenRead( path );
			FileTail.SeekToStart( stream, offset );
			using var reader = new StreamReader( stream, Encoding.UTF8 );

			Assert.AreEqual( "third line\n", reader.ReadToEnd().Replace( "\r\n", "\n" ),
				"the half line the mark fell into belongs to what was there before" );
		}
	}
}
