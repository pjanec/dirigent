using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dirigent.Tests
{
	[TestClass()]
	public class SharedConfigVfsTests
	{
		static SharedConfig Parse( string xml )
			=> new SharedConfigReader( new StringReader( xml ) ).Config;

		static VfsNodeDef FindNode( SharedConfig cfg, string id )
		{
			var node = cfg.VfsNodes.FirstOrDefault( x => x.Id == id );
			Assert.IsNotNull( node, $"Node '{id}' not found in the parsed config" );
			return node!;
		}

		[TestMethod()]
		public void FolderLimitsTest()
		{
			var cfg = Parse( @"
				<Shared>
					<Machine Name='m1' IP='127.0.0.1'>
						<Folder Id='logs' Path='D:\Logs' Mask='**/*.{log,txt}'
								MaxFiles='10' MaxSeconds='172800' MaxTotalBytes='1048576'/>
						<Folder Id='plain' Path='D:\Other'/>
					</Machine>
				</Shared>" );

			var folder = (FolderDef) FindNode( cfg, "logs" );
			Assert.AreEqual( @"**/*.{log,txt}", folder.Mask );
			Assert.AreEqual( 10, folder.MaxFiles );
			Assert.AreEqual( 172800.0, folder.MaxSeconds );
			Assert.AreEqual( 1048576L, folder.MaxTotalBytes );
			Assert.AreEqual( "m1", folder.MachineId );

			// no limits by default, keeping the behavior of the configs written before they existed
			var plain = (FolderDef) FindNode( cfg, "plain" );
			Assert.AreEqual( 0, plain.MaxFiles );
			Assert.AreEqual( 0.0, plain.MaxSeconds );
			Assert.AreEqual( 0L, plain.MaxTotalBytes );
		}

		[TestMethod()]
		public void TailBytesTest()
		{
			var cfg = Parse( @"
				<Shared>
					<Machine Name='m1' IP='127.0.0.1'>
						<Folder Id='logs' Path='D:\Logs' Mask='*.log' TailBytes='52428800'/>
						<File Id='one' Path='D:\Logs\huge.log' TailBytes='1024'/>
						<File Id='newest' Path='D:\Logs' Mask='*.log' Filter='Newest' TailBytes='2048'/>
						<Folder Id='plain' Path='D:\Other'/>
					</Machine>
				</Shared>" );

			// the setting is available on anything that yields files, folders and single files alike
			Assert.AreEqual( 52428800L, FindNode( cfg, "logs" ).TailBytes );
			Assert.AreEqual( 1024L, FindNode( cfg, "one" ).TailBytes );
			Assert.AreEqual( 2048L, FindNode( cfg, "newest" ).TailBytes );

			// whole files unless asked otherwise
			Assert.AreEqual( 0L, FindNode( cfg, "plain" ).TailBytes );
		}

		[TestMethod()]
		public void AppIdTupleOnVfsNodeTest()
		{
			var cfg = Parse( @"
				<Shared>
					<FileRef Id='byTuple' AppIdTuple='m2.camera'/>
					<FileRef Id='byFields' MachineId='m3' AppId='tracker'/>
					<FileRef Id='tupleOverridden' AppIdTuple='m2.camera' MachineId='*'/>
					<FileRef Id='appOnly' AppIdTuple='camera'/>
				</Shared>" );

			// AppIdTuple sets both the machine and the app
			var byTuple = FindNode( cfg, "byTuple" );
			Assert.AreEqual( "m2", byTuple.MachineId );
			Assert.AreEqual( "camera", byTuple.AppId );

			var byFields = FindNode( cfg, "byFields" );
			Assert.AreEqual( "m3", byFields.MachineId );
			Assert.AreEqual( "tracker", byFields.AppId );

			// the individual attributes still win over the tuple
			var overridden = FindNode( cfg, "tupleOverridden" );
			Assert.AreEqual( "*", overridden.MachineId );
			Assert.AreEqual( "camera", overridden.AppId );

			// no dot = app only, any machine
			var appOnly = FindNode( cfg, "appOnly" );
			Assert.AreEqual( "", appOnly.MachineId );
			Assert.AreEqual( "camera", appOnly.AppId );
		}

		[TestMethod()]
		public void TemplateBoundVfsNodesTest()
		{
			// a node declared once in a template must yield one node per app, bound to that very app
			var cfg = Parse( @"
				<Shared>
					<AppTemplate Name='base' ExeFullPath='c:\windows\notepad.exe'>
						<File Id='log' Title='Recent logs' Path='%APP_STARTUPDIR%\logs'
							  Mask='*.log' Filter='Newest' MaxFiles='10' MaxSeconds='172800'/>
					</AppTemplate>
					<App AppIdTuple='m1.camera' Template='base' StartupDir='D:\apps\camera'/>
					<App AppIdTuple='m2.tracker' Template='base' StartupDir='E:\apps\tracker'/>
				</Shared>" );

			var logNodes = cfg.VfsNodes.Where( x => x.Id == "log" ).ToList();
			Assert.AreEqual( 2, logNodes.Count );

			CollectionAssert.AreEquivalent(
				new List<string>() { "m1.camera", "m2.tracker" },
				logNodes.Select( x => $"{x.MachineId}.{x.AppId}" ).ToList() );

			// the filter parameters stay available for the resolution (they are read from the raw xml)
			foreach( var n in logNodes )
			{
				Assert.AreEqual( "Newest", n.Filter );
				StringAssert.Contains( n.Xml!, "MaxSeconds=\"172800\"" );
			}
		}

		[TestMethod()]
		public void OnlyTopLevelNodesAreReferenceableTest()
		{
			// Only the nodes declared directly under Shared / Machine / App go to the registry
			// searched by FileRef. The nodes nested inside a container are not referenceable.
			var cfg = Parse( @"
				<Shared>
					<FilePackage Id='pack'>
						<File Id='nested' Path='\\server\share\nested.txt'/>
					</FilePackage>
					<File Id='topLevel' Path='\\server\share\top.txt'/>
				</Shared>" );

			Assert.IsTrue( cfg.VfsNodes.Any( x => x.Id == "pack" ) );
			Assert.IsTrue( cfg.VfsNodes.Any( x => x.Id == "topLevel" ) );
			Assert.IsFalse( cfg.VfsNodes.Any( x => x.Id == "nested" ),
				"A node nested in a container must not be in the FileRef lookup registry" );

			// it is of course still part of the package content
			var pack = FindNode( cfg, "pack" );
			Assert.AreEqual( 1, pack.Children.Count );
			Assert.AreEqual( "nested", pack.Children[0].Id );
		}

		[TestMethod()]
		public void UnknownContentIsToleratedTest()
		{
			// unknown elements and attributes must not break the config loading
			var cfg = Parse( @"
				<Shared>
					<FilePackage Id='pack' SomeFutureAttribute='42'>
						<ScriptedContent Script='PackageScripts/Test1' Args=''/>
						<SomethingCompletelyUnknown/>
						<File Id='real' Path='\\server\share\file.txt'/>
					</FilePackage>
					<SomeFutureSection><Whatever/></SomeFutureSection>
				</Shared>" );

			var pack = FindNode( cfg, "pack" );

			// the unknown children are skipped, the known ones are kept
			Assert.AreEqual( 1, pack.Children.Count );
			Assert.AreEqual( "real", pack.Children[0].Id );
		}

		[TestMethod()]
		public void ShippedExampleConfigParsesTest()
		{
			// guards against the example config drifting away from what the reader accepts
			var path = Path.GetFullPath( Path.Combine( AppContext.BaseDirectory, @"..\..\..\..\..\config\SharedConfig.xml" ) );
			if( !File.Exists( path ) )
			{
				Assert.Inconclusive( $"Example config not found at {path}" );
				return;
			}

			var cfg = new SharedConfigReader( File.OpenText( path ) ).Config;

			Assert.IsTrue( cfg.VfsNodes.Count > 0, "No VFS nodes loaded from the example config" );
			Assert.IsTrue( cfg.Machines.Count > 0, "No machines loaded from the example config" );
		}
	}
}
