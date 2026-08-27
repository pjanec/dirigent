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
