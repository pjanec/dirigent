using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Dirigent.Tests
{
	/// <summary>
	/// Resolution of a machine-local folder into the tree of virtual folders and files
	/// the download and browse actions work with.
	/// </summary>
	/// <remarks>
	/// A local folder needs neither the IDirig control nor any network, so a bare FileRegistry
	/// with an IP-providing delegate is enough here.
	/// </remarks>
	[TestClass()]
	public class ResolveFolderTests
	{
		const string _machineId = "m1";

		// IDirig provides a default implementation of everything but the few members below;
		// none of them is reached when resolving a machine-local node
		class DirigStub : IDirig
		{
			public string Name => _machineId;

			public Task<TResult?> RunScriptAsync<TArgs, TResult>( string clientId, string scriptName,
					string? sourceCode, TArgs? args, string title, out Guid scriptInstance )
				=> throw new NotImplementedException();

			public Task<VfsNodeDef?> ResolveAsync( VfsNodeDef nodeDef, bool forceUNC, bool includeContent )
				=> throw new NotImplementedException();
		}

		string _root = string.Empty;
		FileRegistry _reg = null!;

		[TestInitialize()]
		public void SetUp()
		{
			_root = Path.Combine( Path.GetTempPath(), "DirigentResolveTests_" + Guid.NewGuid().ToString( "N" ) );
			Directory.CreateDirectory( _root );

			_reg = new FileRegistry( new DirigStub(), _machineId, _root, ( machineId ) => "127.0.0.1" );
			_reg.SetMachines( new List<MachineDef>()
			{
				new MachineDef() { Id = _machineId, IP = "127.0.0.1" }
			} );
		}

		[TestCleanup()]
		public void TearDown()
		{
			try { Directory.Delete( _root, true ); } catch {}
		}

		void MakeFile( string relPath, int ageDays )
		{
			var fullPath = Path.Combine( _root, relPath );
			Directory.CreateDirectory( Path.GetDirectoryName( fullPath )! );
			File.WriteAllText( fullPath, "x" );
			File.SetLastWriteTimeUtc( fullPath, DateTime.UtcNow.AddDays( -ageDays ) );
		}

		VfsNodeDef Resolve( VfsNodeDef def )
		{
			var resolved = _reg.ResolveAsync( null!, def, false, true, null ).GetAwaiter().GetResult();
			Assert.IsNotNull( resolved );
			return resolved!;
		}

		// "title/title/filename" of every file in the resolved tree
		static List<string> FlattenFiles( VfsNodeDef node, string prefix = "" )
		{
			var res = new List<string>();
			foreach( var child in node.Children )
			{
				if( child.IsContainer )
					res.AddRange( FlattenFiles( child, $"{prefix}{child.Title}/" ) );
				else
					res.Add( $"{prefix}{child.Title}" );
			}
			return res;
		}

		[TestMethod()]
		public void FolderTreeTest()
		{
			MakeFile( "top.log", 1 );
			MakeFile( "sub/nested.log", 1 );
			MakeFile( "sub/deeper/deep.log", 1 );
			MakeFile( "sub/ignored.txt", 1 );
			Directory.CreateDirectory( Path.Combine( _root, "empty" ) );

			var resolved = Resolve( new FolderDef()
			{
				Id = "logs",
				Title = "Logs",
				MachineId = _machineId,
				Path = _root,
				Mask = "*.log",
			} );

			Assert.IsTrue( resolved.IsContainer );
			Assert.AreEqual( "Logs", resolved.Title );

			// the subfolder structure is mirrored, the non-matching file is left out
			CollectionAssert.AreEquivalent(
				new List<string>() { "top.log", "sub/nested.log", "sub/deeper/deep.log" },
				FlattenFiles( resolved ) );

			// a folder with no matching file does not appear at all
			Assert.IsFalse( resolved.Children.Any( x => x.Title == "empty" ) );

			// the files carry the machine of the folder, so that the download knows who owns them
			var top = resolved.Children.First( x => !x.IsContainer );
			Assert.AreEqual( _machineId, top.MachineId );
			Assert.AreEqual( Path.Combine( _root, "top.log" ), top.Path );
		}

		[TestMethod()]
		public void TailSettingReachesTheResolvedFilesTest()
		{
			// the download applies the tail, and only the definition knows about it, so it has to
			// survive resolution - on the files a folder yields as well as on a single file
			MakeFile( "top.log", 1 );
			MakeFile( "sub/nested.log", 1 );

			var folder = Resolve( new FolderDef()
			{
				Id = "logs",
				MachineId = _machineId,
				Path = _root,
				Mask = "*.log",
				TailBytes = 1024,
			} );

			foreach( var file in FlattenNodes( folder ).Where( n => !n.IsContainer ) )
				Assert.AreEqual( 1024L, file.TailBytes, $"{file.Path} lost the setting on the way" );

			var single = Resolve( new FileDef()
			{
				Guid = Guid.NewGuid(),
				Id = "one",
				MachineId = _machineId,
				Path = Path.Combine( _root, "top.log" ),
				TailBytes = 2048,
			} );

			Assert.AreEqual( 2048L, single.TailBytes );
		}

		/// <summary>Every node of the tree, the containers included.</summary>
		static List<VfsNodeDef> FlattenNodes( VfsNodeDef root )
		{
			var res = new List<VfsNodeDef>();
			foreach( var child in root.Children )
			{
				res.Add( child );
				res.AddRange( FlattenNodes( child ) );
			}
			return res;
		}

		[TestMethod()]
		public void FolderAgeLimitTest()
		{
			MakeFile( "fresh.log", 0 );
			MakeFile( "old.log", 10 );

			var resolved = Resolve( new FolderDef()
			{
				Id = "logs",
				Title = "Logs",
				MachineId = _machineId,
				Path = _root,
				Mask = "*.log",
				MaxSeconds = 2 * 24 * 3600,
			} );

			CollectionAssert.AreEqual( new List<string>() { "fresh.log" }, FlattenFiles( resolved ) );
		}

		[TestMethod()]
		public void NewestFilterKeepsAppAssociationTest()
		{
			MakeFile( "old.log", 5 );
			MakeFile( "new.log", 1 );

			// a single file requested = the newest one, with the app association preserved
			// so that the download can sort the files into per-app folders
			var resolved = Resolve( new FileDef()
			{
				Id = "log",
				Title = "Recent logs",
				MachineId = _machineId,
				AppId = "camera",
				Path = _root,
				Filter = "Newest",
				Xml = @"<File Mask=""*.log"" MaxFiles=""1"" MaxSeconds=""172800""/>",
			} );

			Assert.IsFalse( resolved.IsContainer );
			Assert.AreEqual( Path.Combine( _root, "new.log" ), resolved.Path );
			Assert.AreEqual( "camera", resolved.AppId );
			Assert.AreEqual( _machineId, resolved.MachineId );
		}

		[TestMethod()]
		public void NewestFilterMultipleFilesTest()
		{
			MakeFile( "a.log", 3 );
			MakeFile( "b.log", 2 );
			MakeFile( "c.log", 1 );

			var resolved = Resolve( new FileDef()
			{
				Id = "log",
				Title = "Recent logs",
				MachineId = _machineId,
				AppId = "camera",
				Path = _root,
				Filter = "Newest",
				Xml = @"<File Mask=""*.log"" MaxFiles=""2""/>",
			} );

			// multiple files come wrapped in a container named after the node, newest first
			Assert.IsTrue( resolved.IsContainer );
			Assert.AreEqual( "Recent logs", resolved.Title );
			CollectionAssert.AreEqual( new List<string>() { "c.log", "b.log" },
				resolved.Children.Select( x => Path.GetFileName( x.Path! ) ).ToList() );

			// the app association is kept on the files too
			Assert.IsTrue( resolved.Children.All( x => x.AppId == "camera" ) );
		}

		[TestMethod()]
		public void WildcardReferenceIsLookedUpLocallyTest()
		{
			// A reference carrying "*" as its machine names no machine: it asks for the node of that id
			// wherever it lives. Resolving one used to be dispatched to a machine literally called "*",
			// which is what made a package collecting the logs of every machine unusable.
			MakeFile( "app.log", 0 );

			// guids as the config reader hands them out: the field default is Guid.Empty, and two nodes
			// sharing it look like a circular reference to the resolver
			_reg.SetVfsNodes( new List<VfsNodeDef>()
			{
				new FileDef()
				{
					Guid = Guid.NewGuid(),
					Id = "log",
					Title = "Recent logs",
					MachineId = _machineId,
					AppId = "camera",
					Path = Path.Combine( _root, "app.log" ),
				},
			} );

			var resolved = Resolve( new FileRef()
			{
				Guid = Guid.NewGuid(),
				Id = "log",
				MachineId = "*",
				AppId = "*",
			} );

			Assert.AreEqual( Path.Combine( _root, "app.log" ), resolved.Path );
			Assert.AreEqual( _machineId, resolved.MachineId, "the file keeps the machine it really lives on" );
			Assert.AreEqual( "camera", resolved.AppId );
		}
	}
}
