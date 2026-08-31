using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Dirigent.Tests
{
	/// <summary>
	/// Taking the end of a file too big to collect whole.
	/// </summary>
	[TestClass()]
	public class FileTailTests
	{
		static MemoryStream StreamOf( string text )
			=> new MemoryStream( Encoding.UTF8.GetBytes( text ) );

		/// <summary>What a tail of the given size would yield out of the given text.</summary>
		static string TailOf( string text, long tailBytes )
		{
			using var stream = StreamOf( text );
			FileTail.SeekToTailStart( stream, tailBytes );
			using var reader = new StreamReader( stream, Encoding.UTF8 );
			return reader.ReadToEnd();
		}

		[TestMethod()]
		public void AppliesOnlyToBiggerFilesTest()
		{
			Assert.IsFalse( FileTail.Applies( 100, 0 ), "no setting, no truncation" );
			Assert.IsFalse( FileTail.Applies( 100, 100 ), "a file of exactly the limit is taken whole" );
			Assert.IsFalse( FileTail.Applies( 99, 100 ) );
			Assert.IsTrue( FileTail.Applies( 101, 100 ) );
		}

		[TestMethod()]
		public void EffectiveSizeIsWhatGetsCollectedTest()
		{
			// this is what a size budget has to count, or a folder of huge tailed logs would look
			// unaffordable while in fact costing very little
			Assert.AreEqual( 100L, FileTail.EffectiveSize( 100, 0 ) );
			Assert.AreEqual( 100L, FileTail.EffectiveSize( 100, 500 ) );
			Assert.AreEqual( 500L, FileTail.EffectiveSize( 60L * 1024 * 1024 * 1024, 500 ) );
		}

		[TestMethod()]
		public void CutsAtALineBoundaryTest()
		{
			var text = "first line\nsecond line\nthird line\n";

			// 20 bytes from the end lands in the middle of "second line", so the partial line goes
			// and the tail starts at the next whole one
			Assert.AreEqual( "third line\n", TailOf( text, 20 ) );
		}

		[TestMethod()]
		public void ALineBoundaryIsNotSkippedTest()
		{
			var text = "first line\nsecond line\n";

			// exactly on the boundary: "second line\n" is 12 bytes. Skipping forward here would
			// throw away the very line the tail is supposed to start with.
			Assert.AreEqual( "second line\n", TailOf( text, 12 ) );
		}

		[TestMethod()]
		public void WholeFileWhenTailIsBiggerTest()
		{
			var text = "first line\nsecond line\n";

			Assert.AreEqual( text, TailOf( text, 1000 ) );
			Assert.AreEqual( text, TailOf( text, text.Length ) );
		}

		[TestMethod()]
		public void FileWithNoLineBreakIsCutAtTheRawOffsetTest()
		{
			// a binary file, most likely; a tail of it is questionable but must not hang or fail
			var text = new string( 'x', 1000 );

			var tail = TailOf( text, 100 );
			Assert.AreEqual( 100, tail.Length );
		}

		[TestMethod()]
		public void EntryNameSaysHowMuchWasTakenTest()
		{
			// the archive listing alone has to show which files are partial
			Assert.AreEqual( "app.last50MB.log", FileTail.EntryNameFor( "app.log", 50 * 1024 * 1024 ) );
			Assert.AreEqual( "app.last1KB.log", FileTail.EntryNameFor( "app.log", 1024 ) );
			Assert.AreEqual( "dump.last300B.bin", FileTail.EntryNameFor( "dump.bin", 300 ) );
			Assert.AreEqual( "noext.last2KB", FileTail.EntryNameFor( "noext", 2048 ) );
		}

		[TestMethod()]
		public void HeaderCarriesTheFactsTest()
		{
			var header = FileTail.HeaderFor( @"D:\Logs\app.log", 60000, 500, new DateTime( 2026, 8, 28, 14, 32, 0 ) );

			StringAssert.Contains( header, @"D:\Logs\app.log", "which file it came from" );
			StringAssert.Contains( header, "60000", "how big that file was" );
			StringAssert.Contains( header, "500", "how much of it is here" );
			StringAssert.Contains( header, "2026-08-28 14:32:00", "and when it was taken" );
			Assert.IsTrue( header.EndsWith( Environment.NewLine ),
				"the header is a line of its own, the log content starts on the next one" );
		}

		[TestMethod()]
		public void FormatSizeTest()
		{
			Assert.AreEqual( "0B", FileTail.FormatSize( 0 ) );
			Assert.AreEqual( "512B", FileTail.FormatSize( 512 ) );
			Assert.AreEqual( "1KB", FileTail.FormatSize( 1024 ) );
			Assert.AreEqual( "50MB", FileTail.FormatSize( 50 * 1024 * 1024 ) );
			Assert.AreEqual( "2GB", FileTail.FormatSize( 2L * 1024 * 1024 * 1024 ) );

			// a whole number of the smaller unit is said in that unit, staying exact
			Assert.AreEqual( "1224KB", FileTail.FormatSize( 1024 * 1024 + 200 * 1024 ) );

			// no whole number of any unit - one decimal, and always with a dot, whatever the
			// machine's locale, since this ends up in a file name
			Assert.AreEqual( "1.5KB", FileTail.FormatSize( 1536 ) );
			Assert.AreEqual( "1.5MB", FileTail.FormatSize( 1536 * 1024 + 512 ) );
		}
	}
}
