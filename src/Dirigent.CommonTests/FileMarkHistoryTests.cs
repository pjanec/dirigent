using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace Dirigent.Tests
{
	/// <summary>
	/// The history of a file's lengths - every mark, and the checkpoints of a plan's MarkInterval - and the collection
	/// "since a time" it makes possible without reading a single timestamp inside a log.
	/// </summary>
	[TestClass()]
	public class FileMarkHistoryTests
	{
		string _folder = "";

		[TestInitialize()]
		public void SetUp()
		{
			_folder = Path.Combine( Path.GetTempPath(), "DirigentMarkHistory_" + Guid.NewGuid().ToString( "N" ) );
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

		FileMarkStore Store() => new FileMarkStore( "m1", _folder );

		static FileMarkStore.SinceStart Since( FileMarkStore store, string path, DateTime sinceUtc )
		{
			var info = new FileInfo( path );
			return store.WhereToStartSince( path, info.Length, info.CreationTimeUtc, info.LastWriteTimeUtc, sinceUtc );
		}

		/// <summary>A moment strictly between two records - the clock of a test is not fine enough to rely on otherwise.</summary>
		static DateTime Between()
		{
			Thread.Sleep( 20 );
			var t = DateTime.UtcNow;
			Thread.Sleep( 20 );
			return t;
		}

		[TestMethod()]
		public void ACheckpointRecordsTheLengthAndLeavesTheMarkAloneTest()
		{
			var path = WriteFile( "app.log", "run one\n" );
			var store = Store();
			store.MarkFile( path );

			File.AppendAllText( path, "more\n" );
			var point = store.Checkpoint( path );

			Assert.IsNotNull( point );
			Assert.AreEqual( 13L, point!.Offset );
			Assert.AreEqual( "Checkpoint", point.MadeBy );
			Assert.AreEqual( 8L, store.Get( path )!.Offset, "the mark is where the Mark put it - a checkpoint never moves it" );
			CollectionAssert.AreEqual( new[] { "Mark", "Checkpoint" }, store.History( path ).Select( p => p.MadeBy ).ToList(),
				"a mark is a record too" );
		}

		[TestMethod()]
		public void SinceStartsAtTheLastRecordBeforeTheTimeTest()
		{
			var path = WriteFile( "app.log", "old\n" );
			var store = Store();
			store.Checkpoint( path );                      // 4 bytes

			File.AppendAllText( path, "middle\n" );
			store.Checkpoint( path );                      // 11 bytes
			var t = Between();
			File.AppendAllText( path, "new\n" );
			store.Checkpoint( path );                      // 15 bytes, after t

			var s = Since( store, path, t );

			Assert.IsFalse( s.Skip );
			Assert.AreEqual( 11L, s.Offset, "from the last record at or before the time - never later, so nothing after it is lost" );
			StringAssert.Contains( s.Note!, "Checkpoint of" );
			StringAssert.Contains( s.Note!, "the last record at or before" );
		}

		[TestMethod()]
		public void AFileNotWrittenSinceTheTimeIsLeftOutTest()
		{
			var path = WriteFile( "app.log", "quiet\n" );
			var store = Store();
			store.Checkpoint( path );
			File.SetLastWriteTimeUtc( path, DateTime.UtcNow.AddHours( -1 ) );

			var s = Since( store, path, DateTime.UtcNow.AddMinutes( -10 ) );

			Assert.IsTrue( s.Skip );
			StringAssert.Contains( s.Note!, "nothing has been written to it since" );
		}

		[TestMethod()]
		public void WithoutARecordBeforeTheTimeTheWholeFileComesTest()
		{
			// too much rather than too little: there is no telling where the time falls in the file
			var path = WriteFile( "app.log", "x\n" );
			var store = Store();
			File.SetCreationTimeUtc( path, DateTime.UtcNow.AddDays( -1 ) );

			var s = Since( store, path, DateTime.UtcNow.AddMinutes( -5 ) );

			Assert.AreEqual( 0L, s.Offset );
			StringAssert.Contains( s.Note!, "no record of its length at or before" );
		}

		[TestMethod()]
		public void AFileCreatedAfterTheTimeComesWholeTest()
		{
			var t = DateTime.UtcNow.AddMinutes( -1 );
			var path = WriteFile( "app.log", "all of it is new\n" );

			var s = Since( Store(), path, t );

			Assert.AreEqual( 0L, s.Offset );
			StringAssert.Contains( s.Note!, "created after" );
		}

		[TestMethod()]
		public void ARotatedFileComesWholeTest()
		{
			var path = WriteFile( "app.log", "a long first life of the file\n" );
			var store = Store();
			store.Checkpoint( path );
			var t = Between();

			File.WriteAllText( path, "short\n" ); // rotated: shorter than the record, the record says nothing about it

			var s = Since( store, path, t );

			Assert.AreEqual( 0L, s.Offset );
			StringAssert.Contains( s.Note!, "replaced, truncated or rotated" );
		}

		[TestMethod()]
		public void TheHistorySurvivesARestartAndGoesWithTheFileTest()
		{
			var path = WriteFile( "app.log", "x\n" );
			var store = Store();
			store.Checkpoint( path );
			store.Save();

			Assert.AreEqual( 1, Store().History( path ).Count, "kept on disk, like the marks" );

			File.Delete( path );
			var again = Store();
			again.Save();
			Assert.AreEqual( 0, Store().History( path ).Count, "forgotten once the file is gone" );
		}

		[TestMethod()]
		public void OldRecordsAreDroppedTest()
		{
			var keep = FileMarkStore.HistoryKeptFor;
			try
			{
				var path = WriteFile( "app.log", "x\n" );
				var store = Store();
				store.Checkpoint( path );

				FileMarkStore.HistoryKeptFor = TimeSpan.Zero;
				store.Checkpoint( path ); // trimming happens as records are added
				Assert.IsTrue( store.History( path ).Count <= 1, "only what is within HistoryKeptFor stays" );
			}
			finally
			{
				FileMarkStore.HistoryKeptFor = keep;
			}
		}
	}
}
