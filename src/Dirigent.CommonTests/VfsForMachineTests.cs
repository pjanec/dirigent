using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent.Tests
{
	/// <summary>
	/// The tree a marking or a download sends to each machine holds that machine's files only - and the files of no machine
	/// for the one that takes them - with every folder (and its notes) kept.
	/// </summary>
	[TestClass()]
	public class VfsForMachineTests
	{
		static VfsNodeDef File( string machine, string path ) => new FileDef() { MachineId = machine, Path = path };

		static VfsNodeDef Folder( string title, params VfsNodeDef[] children )
			=> new VFolderDef() { Title = title, Children = children.ToList() };

		static List<string> Paths( VfsNodeDef node )
			=> node.IsContainer ? node.Children.SelectMany( Paths ).ToList() : new List<string>() { node.Path! };

		[TestMethod()]
		public void EachMachineGetsItsOwnFilesAndTheFoldersTest()
		{
			var pkg = Folder( "pkg",
				Folder( "m1", File( "m1", "a1" ), File( "m1", "a2" ) ),
				Folder( "m2", File( "m2", "b1" ) ),
				File( "", "global" ) );
			pkg.Children[1].AddNote( "something about m2", "m2" );

			var m1 = pkg.ForMachine( "m1", includeGlobals: true );
			CollectionAssert.AreEqual( new[] { "a1", "a2", "global" }, Paths( m1 ) );

			var m2 = pkg.ForMachine( "m2", includeGlobals: false );
			CollectionAssert.AreEqual( new[] { "b1" }, Paths( m2 ) );

			// the folders stay, with their notes - a note is written by the machine it is about
			Assert.AreEqual( 3, m1.Children.Count( c => c.IsContainer ) + m1.Children.Count( c => !c.IsContainer ) );
			Assert.AreEqual( "something about m2", m1.Children[1].Notes!.Single() );

			// the original is untouched
			CollectionAssert.AreEqual( new[] { "a1", "a2", "b1", "global" }, Paths( pkg ) );
		}
	}
}
