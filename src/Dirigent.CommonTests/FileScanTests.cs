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
	}
}
