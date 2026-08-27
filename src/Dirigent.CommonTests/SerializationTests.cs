using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.Scripts.BuiltIn;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent.Tests
{
	/// <summary>
	/// The arguments and results of the built-in scripts cross the machine boundary, so a member
	/// that does not survive the serialization shows up as a runtime failure in a distributed
	/// setup only. These tests keep that from happening.
	/// </summary>
	/// <remarks>
	/// Two different codecs are involved:
	///  - script arguments and results travel as JSON (Tools.Serialize, TypeNameHandling.Auto)
	///  - the state and definitions travel as MessagePack (contractless resolver)
	/// </remarks>
	[TestClass()]
	public class SerializationTests
	{
		static T RoundTripJson<T>( T value )
		{
			var text = Tools.Serialize( value );
			var back = Tools.Deserialize<T>( text );
			Assert.IsNotNull( back, "JSON round trip returned null" );
			return back!;
		}

		static T RoundTripMsgPack<T>( T value )
		{
			var opts = MessagePack.Resolvers.ContractlessStandardResolver.Options;
			var bytes = MessagePack.MessagePackSerializer.Serialize<T>( value, opts );
			var back = MessagePack.MessagePackSerializer.Deserialize<T>( bytes, opts );
			Assert.IsNotNull( back, "MessagePack round trip returned null" );
			return back!;
		}

		/// <summary>
		/// A tree covering every node type, the way a resolved package looks.
		/// </summary>
		static VfsNodeDef MakeSampleTree()
		{
			return new FilePackageDef()
			{
				Id = "logs.all",
				Title = "Logs/All apps",
				IsContainer = true,
				Children = new List<VfsNodeDef>()
				{
					new VFolderDef()
					{
						Title = "AppLogs",
						IsContainer = true,
						Children = new List<VfsNodeDef>()
						{
							new FileDef()
							{
								Id = "log",
								Title = "Recent logs",
								MachineId = "m1",
								AppId = "camera",
								Path = @"D:\apps\camera\logs\app.log",
							},
							new ResolvedVfsNodeDef()
							{
								Id = "resolved",
								MachineId = "m2",
								AppId = "recorder",
								Path = @"\\192.168.0.12\C\apps\recorder\logs\app.log",
							},
						}
					},
					new FolderDef()
					{
						Id = "logTree",
						Title = "Log tree",
						MachineId = "m1",
						Path = @"D:\Logs",
						Mask = "**/*.{log,txt}",
						MaxFiles = 200,
						MaxSeconds = 172800,
						MaxTotalBytes = 52428800,
					},
					new FileRef()
					{
						Id = "cfg",
						MachineId = "*",
						AppId = "*",
					},
				}
			};
		}

		static void AssertSampleTree( VfsNodeDef root )
		{
			// the concrete types must survive, otherwise the receiving side cannot tell
			// a folder from a package or a file
			Assert.IsInstanceOfType( root, typeof( FilePackageDef ) );

			var appLogs = root.Children[0];
			Assert.IsInstanceOfType( appLogs, typeof( VFolderDef ) );
			Assert.AreEqual( "AppLogs", appLogs.Title );

			var file = appLogs.Children[0];
			Assert.IsInstanceOfType( file, typeof( FileDef ) );
			Assert.AreEqual( "m1", file.MachineId );
			Assert.AreEqual( "camera", file.AppId ); // the download sorts the files by this
			Assert.AreEqual( @"D:\apps\camera\logs\app.log", file.Path );

			var resolved = appLogs.Children[1];
			Assert.IsInstanceOfType( resolved, typeof( ResolvedVfsNodeDef ) );
			Assert.AreEqual( "recorder", resolved.AppId );

			var folder = (FolderDef) root.Children[1];
			Assert.AreEqual( "**/*.{log,txt}", folder.Mask );
			Assert.AreEqual( 200, folder.MaxFiles );
			Assert.AreEqual( 172800.0, folder.MaxSeconds );
			Assert.AreEqual( 52428800L, folder.MaxTotalBytes );

			Assert.IsInstanceOfType( root.Children[2], typeof( FileRef ) );
		}

		[TestMethod()]
		public void VfsNodeTreeAsJsonTest()
		{
			// TypeNameHandling.Auto writes the $type of a *member* whose declared type differs from
			// the actual one, but not of the root object. A VfsNodeDef tree therefore round trips
			// only as a member of a concrete type - which is how the script arguments are shaped.
			// A new script must not declare an abstract type as the root of its args or result.
			var args = RoundTripJson( new ResolveVfsPath.TArgs() { VfsNode = MakeSampleTree() } );
			AssertSampleTree( args.VfsNode! );
		}

		[TestMethod()]
		public void VfsNodeTreeAsMsgPackTest()
		{
			// this is the path the definitions take from the master to the clients
			AssertSampleTree( RoundTripMsgPack( MakeSampleTree() ) );
		}

		[TestMethod()]
		public void DownloadZippedArgsTest()
		{
			var args = new DownloadZipped.TArgs()
			{
				Args = "perMachine",
				VfsNode = MakeSampleTree(),
				Vars = new Dictionary<string, string>() { { "FILE_PATH", @"D:\x.log" } },
			};

			var back = RoundTripJson( args );

			Assert.AreEqual( "perMachine", back.Args );
			Assert.AreEqual( @"D:\x.log", back.Vars!["FILE_PATH"] );
			AssertSampleTree( back.VfsNode! );
		}

		[TestMethod()]
		public void DownloadZippedSlaveArgsTest()
		{
			var args = new DownloadZippedSlave.TArgs()
			{
				Container = MakeSampleTree(),
				DestinationFolder = @"\\192.168.0.10\C\Users\joe\Downloads\Logs_260827_1200_parts",
				ZipFileBaseName = "Logs_260827_1200",
				IncludeGlobals = true,
			};

			var back = RoundTripJson( args );

			Assert.AreEqual( args.DestinationFolder, back.DestinationFolder );
			Assert.AreEqual( args.ZipFileBaseName, back.ZipFileBaseName );
			Assert.IsTrue( back.IncludeGlobals );
			AssertSampleTree( back.Container! );
		}

		[TestMethod()]
		public void DownloadZippedSlaveResultTest()
		{
			var result = new DownloadZippedSlave.TResult()
			{
				ZipFileName = "Logs_260827_1200_m1.zip",
				Exceptions = SerializedException.MkList( new List<Exception>()
				{
					new Exception( "file not found: a.log" ),
					new Exception( "access denied: b.log" ),
				} ),
			};

			var back = RoundTripJson( result );

			Assert.AreEqual( "Logs_260827_1200_m1.zip", back.ZipFileName );
			Assert.AreEqual( 2, back.Exceptions.Count );
			StringAssert.Contains( back.Exceptions[0].Message, "a.log" );
			StringAssert.Contains( back.Exceptions[1].Message, "b.log" );
		}

		[TestMethod()]
		public void MergeZippedArgsTest()
		{
			var args = new MergeZipped.TArgs()
			{
				StagingFolder = @"C:\Users\joe\Downloads\Logs_260827_1200_parts",
				DestinationFile = @"C:\Users\joe\Downloads\Logs_260827_1200.zip",
				PrefixWithMachine = true,
				Parts = new List<MergeZipped.TPart>()
				{
					new MergeZipped.TPart() { FileName = "Logs_260827_1200_m1.zip", MachineName = "m1" },
					new MergeZipped.TPart() { FileName = "Logs_260827_1200_m2.zip", MachineName = "m2" },
				},
			};

			var back = RoundTripJson( args );

			Assert.AreEqual( args.StagingFolder, back.StagingFolder );
			Assert.AreEqual( args.DestinationFile, back.DestinationFile );
			Assert.IsTrue( back.PrefixWithMachine );
			Assert.AreEqual( 2, back.Parts.Count );
			CollectionAssert.AreEqual(
				new List<string>() { "m1", "m2" },
				back.Parts.Select( x => x.MachineName ).ToList() );
			CollectionAssert.AreEqual(
				new List<string>() { "Logs_260827_1200_m1.zip", "Logs_260827_1200_m2.zip" },
				back.Parts.Select( x => x.FileName ).ToList() );
		}

		[TestMethod()]
		public void MergeZippedResultTest()
		{
			var result = new MergeZipped.TResult()
			{
				ZipFileName = "Logs_260827_1200.zip",
				FileCount = 42,
				Exceptions = SerializedException.MkList( new List<Exception>()
				{
					new Exception( "m2: not a zip archive" ),
				} ),
			};

			var back = RoundTripJson( result );

			Assert.AreEqual( "Logs_260827_1200.zip", back.ZipFileName );
			Assert.AreEqual( 42, back.FileCount );
			Assert.AreEqual( 1, back.Exceptions.Count );
			StringAssert.Contains( back.Exceptions[0].Message, "m2" );
		}

		[TestMethod()]
		public void ResolveVfsPathArgsTest()
		{
			// the arguments of the remote resolution, the other cross-machine call of the VFS
			var args = new ResolveVfsPath.TArgs()
			{
				VfsNode = MakeSampleTree(),
				ForceUNC = true,
				IncludeContent = true,
			};

			var back = RoundTripJson( args );

			Assert.IsTrue( back.ForceUNC );
			Assert.IsTrue( back.IncludeContent );
			AssertSampleTree( back.VfsNode! );
		}
	}
}
