using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dirigent.Tests
{
	[TestClass()]
	public class FileScanTests
	{
		string _root = string.Empty;

		[TestInitialize()]
		public void SetUp()
		{
			_root = Path.Combine( Path.GetTempPath(), "DirigentFileScanTests_" + Guid.NewGuid().ToString( "N" ) );
			Directory.CreateDirectory( _root );
		}

		[TestCleanup()]
		public void TearDown()
		{
			try { Directory.Delete( _root, true ); } catch {}
		}

		/// <summary>
		/// Creates a file with given content, aged given number of days.
		/// </summary>
		void MakeFile( string relPath, int ageDays, int sizeBytes = 10 )
		{
			var fullPath = Path.Combine( _root, relPath );
			Directory.CreateDirectory( Path.GetDirectoryName( fullPath )! );
			File.WriteAllBytes( fullPath, new byte[sizeBytes] );
			File.SetLastWriteTimeUtc( fullPath, DateTime.UtcNow.AddDays( -ageDays ) );
		}

		List<string> Scan( string? mask, double maxAgeSeconds = 0, int maxFiles = 0, long maxTotalBytes = 0, bool recursive = true )
			=> FileScan.FindMatchingFiles( _root, mask, maxAgeSeconds, maxFiles, maxTotalBytes, recursive )
				.Files
				.Select( x => x.RelPath.Replace( '\\', '/' ) )
				.ToList();

		/// <summary>The files that matched but did not fit the size budget.</summary>
		List<string> Skipped( string? mask, long maxTotalBytes )
			=> FileScan.FindMatchingFiles( _root, mask, 0, 0, maxTotalBytes, true )
				.Skipped
				.Select( x => x.RelPath.Replace( '\\', '/' ) )
				.ToList();

		[TestMethod()]
		public void NewestFirstTest()
		{
			MakeFile( "old.log", 10 );
			MakeFile( "middle.log", 5 );
			MakeFile( "new.log", 1 );

			// the files must come back newest first, so that any limit keeps the most recent ones
			CollectionAssert.AreEqual(
				new List<string>() { "new.log", "middle.log", "old.log" },
				Scan( "*.log" ) );

			// a single file requested = the newest one, not the oldest
			CollectionAssert.AreEqual(
				new List<string>() { "new.log" },
				Scan( "*.log", maxFiles: 1 ) );

			CollectionAssert.AreEqual(
				new List<string>() { "new.log", "middle.log" },
				Scan( "*.log", maxFiles: 2 ) );
		}

		[TestMethod()]
		public void MaxSecondsTest()
		{
			MakeFile( "old.log", 10 );
			MakeFile( "yesterday.log", 1 );
			MakeFile( "today.log", 0 );

			// "not older than 2 days"
			var res = Scan( "*.log", maxAgeSeconds: 2 * 24 * 3600 );
			CollectionAssert.AreEqual( new List<string>() { "today.log", "yesterday.log" }, res );

			// 0 = whatever age
			Assert.AreEqual( 3, Scan( "*.log", maxAgeSeconds: 0 ).Count );
		}

		[TestMethod()]
		public void MaxTotalBytesTest()
		{
			MakeFile( "new.log", 1, sizeBytes: 100 );
			MakeFile( "old.log", 2, sizeBytes: 100 );

			// only the newest one fits into the budget
			CollectionAssert.AreEqual( new List<string>() { "new.log" }, Scan( "*.log", maxTotalBytes: 150 ) );

			// both fit
			Assert.AreEqual( 2, Scan( "*.log", maxTotalBytes: 250 ).Count );

			// at least one file is returned even if it exceeds the budget on its own
			CollectionAssert.AreEqual( new List<string>() { "new.log" }, Scan( "*.log", maxTotalBytes: 1 ) );
		}

		[TestMethod()]
		public void OneBigFileDoesNotEndTheScanTest()
		{
			// an unrotated log file among the rotated ones is the everyday case of this
			MakeFile( "new.log", 1, sizeBytes: 100 );
			MakeFile( "huge.log", 2, sizeBytes: 5000 );
			MakeFile( "old.log", 3, sizeBytes: 100 );

			// the outlier is passed over, the smaller file behind it still fits and is taken;
			// stopping at the outlier would have thrown away the whole older part of the folder
			CollectionAssert.AreEqual(
				new List<string>() { "new.log", "old.log" },
				Scan( "*.log", maxTotalBytes: 300 ) );
		}

		[TestMethod()]
		public void SkippedFilesAreReportedTest()
		{
			MakeFile( "new.log", 1, sizeBytes: 100 );
			MakeFile( "huge.log", 2, sizeBytes: 5000 );
			MakeFile( "old.log", 3, sizeBytes: 100 );

			// what a limit pushed out has to be knowable - the user did ask for those files
			CollectionAssert.AreEqual( new List<string>() { "huge.log" }, Skipped( "*.log", 300 ) );

			var skipped = FileScan.FindMatchingFiles( _root, "*.log", 0, 0, 300, true ).Skipped;
			Assert.AreEqual( 5000L, skipped[0].Bytes, "the size of the skipped file is reported too" );

			// nothing was left out when everything fits
			Assert.AreEqual( 0, Skipped( "*.log", 100000 ).Count );

			// nor when no size budget applies at all
			Assert.AreEqual( 0, FileScan.FindMatchingFiles( _root, "*.log", 0, 0, 0, true ).Skipped.Count );
		}

		[TestMethod()]
		public void TailedFilesCountOnlyWhatWillBeCollectedTest()
		{
			MakeFile( "new.log", 1, sizeBytes: 100 );
			MakeFile( "huge.log", 2, sizeBytes: 5000 );
			MakeFile( "old.log", 3, sizeBytes: 100 );

			// with only the last 50 bytes of a big file collected, the budget it consumes is 50,
			// not 5000 - so everything fits and nothing is dropped
			var res = FileScan.FindMatchingFiles( _root, "*.log", 0, 0, 300, true, tailBytes: 50 );

			CollectionAssert.AreEquivalent(
				new List<string>() { "new.log", "huge.log", "old.log" },
				res.Files.Select( x => x.RelPath ).ToList() );
			Assert.AreEqual( 0, res.Skipped.Count );

			// without the tail setting the same folder does not fit
			Assert.AreEqual( 1, FileScan.FindMatchingFiles( _root, "*.log", 0, 0, 300, true ).Skipped.Count );
		}

		[TestMethod()]
		public void MaxFilesStopsTheScanTest()
		{
			// unlike the size budget, a reached count limit really is the end - nothing further
			// can be taken, so nothing is reported as skipped either
			MakeFile( "new.log", 1 );
			MakeFile( "middle.log", 2 );
			MakeFile( "old.log", 3 );

			var res = FileScan.FindMatchingFiles( _root, "*.log", 0, 2, 0, true );

			CollectionAssert.AreEqual(
				new List<string>() { "new.log", "middle.log" },
				res.Files.Select( x => x.RelPath ).ToList() );
			Assert.AreEqual( 0, res.Skipped.Count );
		}

		[TestMethod()]
		public void RecursionAndMaskTest()
		{
			MakeFile( "top.log", 1 );
			MakeFile( "sub/nested.log", 2 );
			MakeFile( "sub/deeper/deep.log", 3 );
			MakeFile( "sub/other.txt", 4 );

			// a plain file name mask applies at any depth
			CollectionAssert.AreEquivalent(
				new List<string>() { "top.log", "sub/nested.log", "sub/deeper/deep.log" },
				Scan( "*.log" ) );

			// non-recursive scan (the way the 'Newest' filter uses it)
			CollectionAssert.AreEquivalent(
				new List<string>() { "top.log" },
				Scan( "*.log", recursive: false ) );

			// alternatives
			CollectionAssert.AreEquivalent(
				new List<string>() { "top.log", "sub/nested.log", "sub/deeper/deep.log", "sub/other.txt" },
				Scan( "**/*.{log,txt}" ) );

			// a path glob restricting the location
			CollectionAssert.AreEquivalent(
				new List<string>() { "sub/nested.log", "sub/deeper/deep.log" },
				Scan( "sub/**/*.log" ) );

			// empty mask = everything
			Assert.AreEqual( 4, Scan( "" ).Count );
		}

		[TestMethod()]
		public void EmptyFolderTest()
		{
			Assert.AreEqual( 0, Scan( "*.log" ).Count );
		}

		// ---- a time window chooses the files: the written span (creation .. last write) must overlap it ----

		static readonly DateTime _t0 = new DateTime( 2026, 11, 21, 14, 0, 0, DateTimeKind.Utc );
		static readonly DateTime _t1 = new DateTime( 2026, 11, 21, 15, 0, 0, DateTimeKind.Utc );

		void MakeSpan( string name, DateTime createdUtc, DateTime writtenUtc, int sizeBytes = 10 )
		{
			var fullPath = Path.Combine( _root, name );
			File.WriteAllBytes( fullPath, new byte[sizeBytes] );
			File.SetCreationTimeUtc( fullPath, createdUtc );
			File.SetLastWriteTimeUtc( fullPath, writtenUtc );
		}

		List<string> ScanWindow( FileWindow window, double maxAgeSeconds = 0, int maxFiles = 0, long maxTotalBytes = 0 )
			=> FileScan.FindMatchingFiles( _root, "*.log", maxAgeSeconds, maxFiles, maxTotalBytes, true, window: window )
				.Files.Select( x => x.RelPath ).OrderBy( x => x ).ToList();

		[TestMethod()]
		public void AWindowTakesTheFilesWhoseSpanOverlapsItTest()
		{
			MakeSpan( "inside.log", _t0.AddMinutes( 5 ), _t1.AddMinutes( -5 ) );
			MakeSpan( "before.log", _t0.AddHours( -3 ), _t0.AddMinutes( -1 ) );   // last written before the window
			MakeSpan( "after.log", _t1.AddMinutes( 1 ), _t1.AddHours( 2 ) );      // created after the window
			MakeSpan( "around.log", _t0.AddHours( -3 ), _t1.AddHours( 2 ) );      // spans the whole window
			MakeSpan( "ends-in.log", _t0.AddHours( -3 ), _t0.AddMinutes( 5 ) );
			MakeSpan( "starts-in.log", _t1.AddMinutes( -5 ), _t1.AddHours( 2 ) );

			CollectionAssert.AreEqual( new[] { "around.log", "ends-in.log", "inside.log", "starts-in.log" },
				ScanWindow( new FileWindow() { SinceUtc = _t0, UntilUtc = _t1 } ) );

			// an open end: only the creation matters at the one side, only the last write at the other
			CollectionAssert.AreEqual( new[] { "after.log", "around.log", "ends-in.log", "inside.log", "starts-in.log" },
				ScanWindow( new FileWindow() { SinceUtc = _t0 } ) );
			CollectionAssert.AreEqual( new[] { "around.log", "before.log", "ends-in.log", "inside.log", "starts-in.log" },
				ScanWindow( new FileWindow() { UntilUtc = _t1 } ) );
		}

		[TestMethod()]
		public void AWindowOverridesTheCountAndAgeLimitsTest()
		{
			MakeSpan( "a.log", _t0.AddMinutes( 1 ), _t0.AddMinutes( 10 ) );
			MakeSpan( "b.log", _t0.AddMinutes( 11 ), _t0.AddMinutes( 20 ) );
			MakeSpan( "c.log", _t0.AddMinutes( 21 ), _t0.AddMinutes( 30 ) );

			var window = new FileWindow() { SinceUtc = _t0, UntilUtc = _t1 };

			// long ago (far older than the one second of age allowed), and more files than the one allowed
			CollectionAssert.AreEqual( new[] { "a.log", "b.log", "c.log" }, ScanWindow( window, maxAgeSeconds: 1, maxFiles: 1 ) );

			// without a window the limits choose, as ever
			Assert.AreEqual( 0, Scan( "*.log", maxAgeSeconds: 1 ).Count );
			Assert.AreEqual( 1, Scan( "*.log", maxFiles: 1 ).Count );
		}

		[TestMethod()]
		public void UntilAloneKeepsTheLimitsAsOfUntilTest()
		{
			// runs long before, just before and after 'until'
			MakeSpan( "old.log", _t1.AddDays( -30 ), _t1.AddDays( -30 ).AddMinutes( 10 ) );
			MakeSpan( "a.log", _t1.AddMinutes( -50 ), _t1.AddMinutes( -40 ) );
			MakeSpan( "b.log", _t1.AddMinutes( -30 ), _t1.AddMinutes( -20 ) );
			MakeSpan( "later.log", _t1.AddMinutes( 10 ), _t1.AddMinutes( 20 ) );

			var window = new FileWindow() { UntilUtc = _t1 };

			// the newest file as of 'until', not of now - and not every file ever written before it
			CollectionAssert.AreEqual( new[] { "b.log" }, ScanWindow( window, maxFiles: 1 ) );

			// the age counts from 'until'
			CollectionAssert.AreEqual( new[] { "a.log", "b.log" }, ScanWindow( window, maxAgeSeconds: 3600 ) );
		}

		[TestMethod()]
		public void AWindowLeavesTheSizeBudgetInForceTest()
		{
			MakeSpan( "a.log", _t0.AddMinutes( 1 ), _t0.AddMinutes( 10 ), sizeBytes: 100 );
			MakeSpan( "b.log", _t0.AddMinutes( 11 ), _t0.AddMinutes( 20 ), sizeBytes: 100 );

			var window = new FileWindow() { SinceUtc = _t0, UntilUtc = _t1 };

			Assert.AreEqual( 1, ScanWindow( window, maxTotalBytes: 150 ).Count, "the budget still applies" );
			Assert.AreEqual( 2, ScanWindow( window, maxTotalBytes: 250 ).Count );
		}
	}
}
