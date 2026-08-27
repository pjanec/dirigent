using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.Scripts.BuiltIn;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Dirigent.Tests
{
	/// <summary>
	/// Joining of the per-machine archives into the single one the user gets.
	/// </summary>
	[TestClass()]
	public class MergeZippedTests
	{
		string _root = string.Empty;
		string _staging = string.Empty;
		string _destination = string.Empty;

		[TestInitialize()]
		public void SetUp()
		{
			_root = Path.Combine( Path.GetTempPath(), "DirigentMergeTests_" + Guid.NewGuid().ToString( "N" ) );
			_staging = Path.Combine( _root, "Logs_260827_1200_parts" );
			_destination = Path.Combine( _root, "Logs_260827_1200.zip" );
			Directory.CreateDirectory( _staging );
		}

		[TestCleanup()]
		public void TearDown()
		{
			try { Directory.Delete( _root, true ); } catch {}
		}

		/// <summary>
		/// Creates a zip in the staging folder holding the given "entryPath = content" items.
		/// </summary>
		void MakePart( string fileName, params string[] entries )
		{
			using var zip = ZipFile.Open( Path.Combine( _staging, fileName ), ZipArchiveMode.Create );
			foreach( var e in entries )
			{
				var entry = zip.CreateEntry( e );
				using var w = new StreamWriter( entry.Open() );
				w.Write( "content of " + e );
			}
		}

		static List<string> EntriesOf( string zipPath )
		{
			using var zip = ZipFile.OpenRead( zipPath );
			return zip.Entries.Select( x => x.FullName ).ToList();
		}

		static string ReadEntry( string zipPath, string entryName )
		{
			using var zip = ZipFile.OpenRead( zipPath );
			var entry = zip.GetEntry( entryName );
			Assert.IsNotNull( entry, $"Entry '{entryName}' not found in the archive" );
			using var r = new StreamReader( entry!.Open() );
			return r.ReadToEnd();
		}

		MergeZipped.TArgs MakeArgs( bool prefixWithMachine, params (string File, string Machine)[] parts )
			=> new MergeZipped.TArgs()
			{
				StagingFolder = _staging,
				DestinationFile = _destination,
				PrefixWithMachine = prefixWithMachine,
				Parts = parts.Select( p => new MergeZipped.TPart() { FileName = p.File, MachineName = p.Machine } ).ToList(),
			};

		[TestMethod()]
		public void MergesUnderMachineFoldersTest()
		{
			MakePart( "Logs_260827_1200_m1.zip", "AppLogs/camera/app.log", "AppLogs/tracker/app.log" );
			MakePart( "Logs_260827_1200_m2.zip", "AppLogs/recorder/app.log" );

			var result = MergeZipped.Merge( MakeArgs( true,
				("Logs_260827_1200_m1.zip", "m1"),
				("Logs_260827_1200_m2.zip", "m2") ) );

			Assert.AreEqual( "Logs_260827_1200.zip", result.ZipFileName );
			Assert.AreEqual( 3, result.FileCount );
			Assert.AreEqual( 0, result.Exceptions.Count );

			CollectionAssert.AreEquivalent(
				new List<string>()
				{
					"m1/AppLogs/camera/app.log",
					"m1/AppLogs/tracker/app.log",
					"m2/AppLogs/recorder/app.log",
				},
				EntriesOf( _destination ) );

			// the content survives the repacking
			Assert.AreEqual( "content of AppLogs/recorder/app.log",
				ReadEntry( _destination, "m2/AppLogs/recorder/app.log" ) );
		}

		[TestMethod()]
		public void SingleMachineNeedsNoMachineFolderTest()
		{
			MakePart( "Logs_260827_1200_m1.zip", "AppLogs/camera/app.log" );

			// one part and no prefix wanted - the part just becomes the result
			var result = MergeZipped.Merge( MakeArgs( false, ("Logs_260827_1200_m1.zip", "m1") ) );

			Assert.AreEqual( "Logs_260827_1200.zip", result.ZipFileName );
			Assert.AreEqual( 1, result.FileCount );
			CollectionAssert.AreEqual( new List<string>() { "AppLogs/camera/app.log" }, EntriesOf( _destination ) );
		}

		[TestMethod()]
		public void StagingFolderIsRemovedTest()
		{
			MakePart( "Logs_260827_1200_m1.zip", "a.log" );
			MakePart( "Logs_260827_1200_m2.zip", "b.log" );

			MergeZipped.Merge( MakeArgs( true,
				("Logs_260827_1200_m1.zip", "m1"),
				("Logs_260827_1200_m2.zip", "m2") ) );

			Assert.IsFalse( Directory.Exists( _staging ), "The staging folder must not be left behind" );
			Assert.IsTrue( File.Exists( _destination ) );
		}

		[TestMethod()]
		public void BrokenPartDoesNotSpoilTheRestTest()
		{
			MakePart( "Logs_260827_1200_m1.zip", "a.log" );
			File.WriteAllText( Path.Combine( _staging, "Logs_260827_1200_m2.zip" ), "this is not a zip file" );

			var result = MergeZipped.Merge( MakeArgs( true,
				("Logs_260827_1200_m1.zip", "m1"),
				("Logs_260827_1200_m2.zip", "m2") ) );

			// the good part is there, the bad one is reported
			CollectionAssert.AreEqual( new List<string>() { "m1/a.log" }, EntriesOf( _destination ) );
			Assert.AreEqual( 1, result.Exceptions.Count );
			StringAssert.Contains( result.Exceptions[0].Message, "m2" );
		}

		[TestMethod()]
		public void NothingToMergeTest()
		{
			var result = MergeZipped.Merge( MakeArgs( true ) );

			// no empty archive is produced, but the staging folder is cleaned up anyway
			Assert.AreEqual( "", result.ZipFileName );
			Assert.IsFalse( File.Exists( _destination ) );
			Assert.IsFalse( Directory.Exists( _staging ) );
		}

		[TestMethod()]
		public void DuplicateEntryNamesGetSuffixedTest()
		{
			// the same entry name from two machines, with no machine folder to tell them apart
			MakePart( "Logs_260827_1200_m1.zip", "app.log" );
			MakePart( "Logs_260827_1200_m2.zip", "app.log" );

			var result = MergeZipped.Merge( MakeArgs( false,
				("Logs_260827_1200_m1.zip", "m1"),
				("Logs_260827_1200_m2.zip", "m2") ) );

			Assert.AreEqual( 2, result.FileCount );
			CollectionAssert.AreEquivalent( new List<string>() { "app.log", "app_2.log" }, EntriesOf( _destination ) );
		}
	}
}
