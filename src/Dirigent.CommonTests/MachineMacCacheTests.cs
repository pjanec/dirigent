using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dirigent.Tests
{
	/// <summary>
	/// The master's memory of what each agent reported about its adapters.
	/// </summary>
	[TestClass()]
	public class MachineMacCacheTests
	{
		string _folder = "";

		[TestInitialize()]
		public void SetUp()
		{
			_folder = Path.Combine( Path.GetTempPath(), "DirigentMacCache_" + Guid.NewGuid().ToString( "N" ) );
		}

		[TestCleanup()]
		public void TearDown()
		{
			try { if( Directory.Exists( _folder ) ) Directory.Delete( _folder, true ); } catch { }
		}

		static List<NicInfo> Nics( string mac, string ip = "192.168.1.10" )
			=> new() { new NicInfo( mac, ip, 24 ) };

		[TestMethod()]
		public void AReportIsPersistedAndFoundByAFreshInstance()
		{
			var cache = new MachineMacCache( _folder );
			cache.Update( "m1", Nics( "AA-BB-CC-DD-EE-01" ) );

			Assert.IsTrue( File.Exists( Path.Combine( _folder, "MachineMacs.json" ) ) );

			var reloaded = new MachineMacCache( _folder );
			CollectionAssert.AreEqual( Nics( "AA-BB-CC-DD-EE-01" ), reloaded.Get( "m1" ) );
			Assert.AreEqual( 0, reloaded.Get( "unknown" ).Count );
		}

		[TestMethod()]
		public void ANewReportReplacesTheOldOneWholesale()
		{
			var cache = new MachineMacCache( _folder );
			cache.Update( "m1", new List<NicInfo>()
			{
				new NicInfo( "AA-BB-CC-DD-EE-01", "192.168.1.10", 24 ),
				new NicInfo( "AA-BB-CC-DD-EE-02", "10.0.0.10", 8 ),
			} );

			// the computer was swapped
			cache.Update( "m1", Nics( "11-22-33-44-55-66", "192.168.1.11" ) );

			CollectionAssert.AreEqual( Nics( "11-22-33-44-55-66", "192.168.1.11" ), cache.Get( "m1" ) );
			CollectionAssert.AreEqual( Nics( "11-22-33-44-55-66", "192.168.1.11" ), new MachineMacCache( _folder ).Get( "m1" ) );
		}

		[TestMethod()]
		public void AnEmptyReportDoesNotEraseWhatIsKnown()
		{
			var cache = new MachineMacCache( _folder );
			cache.Update( "m1", Nics( "AA-BB-CC-DD-EE-01" ) );

			cache.Update( "m1", new List<NicInfo>() );

			CollectionAssert.AreEqual( Nics( "AA-BB-CC-DD-EE-01" ), cache.Get( "m1" ) );
			CollectionAssert.AreEqual( Nics( "AA-BB-CC-DD-EE-01" ), new MachineMacCache( _folder ).Get( "m1" ) );
		}

		[TestMethod()]
		public void AnEmptyReportFromAnUnknownMachineWritesNothing()
		{
			var cache = new MachineMacCache( _folder );
			cache.Update( "m1", new List<NicInfo>() );

			Assert.IsFalse( File.Exists( cache.FilePath ) );
		}

		[TestMethod()]
		public void UnchangedContentDoesNotRewriteTheFile()
		{
			var cache = new MachineMacCache( _folder );
			cache.Update( "m1", Nics( "AA-BB-CC-DD-EE-01" ) );
			Assert.AreEqual( 1, cache.WriteCount );

			// make any rewrite visible by the timestamp too
			var old = new DateTime( 2020, 1, 1, 0, 0, 0, DateTimeKind.Utc );
			File.SetLastWriteTimeUtc( cache.FilePath, old );

			cache.Update( "m1", Nics( "AA-BB-CC-DD-EE-01" ) );

			Assert.AreEqual( 1, cache.WriteCount, "the same report again is not a change" );
			Assert.AreEqual( old, File.GetLastWriteTimeUtc( cache.FilePath ) );

			// and a fresh instance that loaded the same content does not rewrite it either
			var reloaded = new MachineMacCache( _folder );
			reloaded.Update( "m1", Nics( "AA-BB-CC-DD-EE-01" ) );
			Assert.AreEqual( 0, reloaded.WriteCount );
			Assert.AreEqual( old, File.GetLastWriteTimeUtc( cache.FilePath ) );
		}

		[TestMethod()]
		public void ACorruptFileIsAnEmptyCacheNotAnException()
		{
			Directory.CreateDirectory( _folder );
			File.WriteAllText( Path.Combine( _folder, "MachineMacs.json" ), "{ this is not json" );

			var cache = new MachineMacCache( _folder );

			Assert.AreEqual( 0, cache.Get( "m1" ).Count );

			// and it heals on the next report
			cache.Update( "m1", Nics( "AA-BB-CC-DD-EE-01" ) );
			CollectionAssert.AreEqual( Nics( "AA-BB-CC-DD-EE-01" ), new MachineMacCache( _folder ).Get( "m1" ) );
		}

		[TestMethod()]
		public void NoTemporaryFileIsLeftBehind()
		{
			var cache = new MachineMacCache( _folder );
			cache.Update( "m1", Nics( "AA-BB-CC-DD-EE-01" ) );

			CollectionAssert.AreEqual( new[] { "MachineMacs.json" },
				Directory.GetFiles( _folder ).Select( Path.GetFileName ).ToArray() );
		}
	}
}
