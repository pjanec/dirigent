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
		public void AskCommentIsAnAttributeOfItsOwnTest()
		{
			// Not a word inside Args: that string is the script's own arguments, and a UI directive
			// hidden in it would be found by a substring search that any other argument could trip.
			var cfg = Parse( @"
				<Shared>
					<Machine Name='m1' IP='127.0.0.1'>
						<FilePackage Id='asks' Description='Everything, from both machines.'>
							<Script Name='BuiltIns/DownloadZipped.cs' Args='someArg askComment inside' AskComment='1'/>
						</FilePackage>
						<FilePackage Id='quiet'>
							<Script Name='BuiltIns/DownloadZipped.cs' Args='askComment'/>
						</FilePackage>
					</Machine>
				</Shared>" );

			var asks = (ScriptActionDef) FindNode( cfg, "asks" ).Actions.Single();
			Assert.IsTrue( asks.AskComment );
			Assert.AreEqual( "someArg askComment inside", asks.Args, "the script's own arguments are left alone" );
			Assert.AreEqual( "Everything, from both machines.", FindNode( cfg, "asks" ).Description );

			// the word in Args means nothing on its own
			var quiet = (ScriptActionDef) FindNode( cfg, "quiet" ).Actions.Single();
			Assert.IsFalse( quiet.AskComment );
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
		public void NodeTemplateDefaultsAndOverridesTest()
		{
			// the template's attributes are defaults; a node's own attribute wins; the Template attribute is gone
			// from what the rest of the system sees, and the template itself is not a node
			var cfg = Parse( @"
				<Shared>
					<NodeTemplate Name='log' Filter='Newest' MaxFiles='3' MaxSeconds='604800' TailBytes='1000' Clearable='1'/>
					<Machine Name='m1' IP='127.0.0.1'>
						<File Template='log' Id='plain' Path='D:\Logs' Mask='a*.log'/>
						<File Template='log' Id='over' Path='D:\Logs' Mask='b*.log' MaxFiles='2' TailBytes='5'/>
						<Folder Template='log' Id='folder' Path='D:\Logs' Mask='*.log'/>
					</Machine>
				</Shared>" );

			var plain = FindNode( cfg, "plain" );
			Assert.AreEqual( "Newest", plain.Filter );
			Assert.AreEqual( 1000L, plain.TailBytes );
			Assert.IsTrue( plain.Clearable );
			StringAssert.Contains( plain.Xml!, "MaxFiles=\"3\"" );
			StringAssert.Contains( plain.Xml!, "MaxSeconds=\"604800\"" );
			StringAssert.Contains( plain.Xml!, "Mask=\"a*.log\"" );
			Assert.IsFalse( plain.Xml!.Contains( "Template" ), "the Template attribute is consumed" );

			var over = FindNode( cfg, "over" );
			StringAssert.Contains( over.Xml!, "MaxFiles=\"2\"" );
			StringAssert.Contains( over.Xml!, "MaxSeconds=\"604800\"" );
			Assert.AreEqual( 5L, over.TailBytes );

			// any node kind takes a template
			Assert.AreEqual( "Newest", FindNode( cfg, "folder" ).Filter );
			Assert.AreEqual( 3, ( (FolderDef) FindNode( cfg, "folder" ) ).MaxFiles );

			Assert.AreEqual( 3, cfg.VfsNodes.Count, "NodeTemplate must not become a node" );
		}

		[TestMethod()]
		public void NodeTemplateChainingTest()
		{
			// the derived template wins over its base; the node wins over both
			var cfg = Parse( @"
				<Shared>
					<NodeTemplate Name='base' Filter='Newest' MaxFiles='3' TailBytes='100' Clearable='1'/>
					<NodeTemplate Name='derived' Template='base' MaxFiles='7' TailBytes='200'/>
					<Machine Name='m1' IP='127.0.0.1'>
						<File Template='derived' Id='a' Path='D:\Logs' Mask='*.log'/>
						<File Template='derived' Id='b' Path='D:\Logs' Mask='*.log' TailBytes='300'/>
					</Machine>
				</Shared>" );

			var a = FindNode( cfg, "a" );
			Assert.AreEqual( "Newest", a.Filter, "inherited from the base" );
			Assert.IsTrue( a.Clearable, "inherited from the base" );
			StringAssert.Contains( a.Xml!, "MaxFiles=\"7\"" );
			Assert.AreEqual( 200L, a.TailBytes );
			Assert.AreEqual( 300L, FindNode( cfg, "b" ).TailBytes );
		}

		[TestMethod()]
		public void NodeTemplateChildActionsTest()
		{
			// children of the template come first, so a node's own action with the same Id replaces the template's
			var cfg = Parse( @"
				<Shared>
					<NodeTemplate Name='base'>
						<Script Id='dl' Name='Base.cs'/>
					</NodeTemplate>
					<NodeTemplate Name='derived' Template='base'>
						<Script Id='extra' Name='Extra.cs'/>
					</NodeTemplate>
					<Machine Name='m1' IP='127.0.0.1'>
						<File Template='derived' Id='plain' Path='D:\a.log'/>
						<File Template='derived' Id='own' Path='D:\b.log'>
							<Script Id='dl' Name='Own.cs'/>
						</File>
					</Machine>
				</Shared>" );

			var plain = FindNode( cfg, "plain" );
			CollectionAssert.AreEqual( new[] { "dl", "extra" }, plain.Actions.Select( x => x.Id ).ToArray() );
			Assert.AreEqual( "Base.cs", ( (ScriptActionDef) plain.Actions[0] ).Name );

			var own = FindNode( cfg, "own" );
			CollectionAssert.AreEqual( new[] { "dl", "extra" }, own.Actions.Select( x => x.Id ).ToArray() );
			Assert.AreEqual( "Own.cs", ( (ScriptActionDef) own.Actions[0] ).Name, "the node's own action wins" );
		}

		[TestMethod()]
		public void NodeTemplateInsideAppTemplateAndContainersTest()
		{
			// a template used in an AppTemplate gives per-app nodes, and %APP_ID%/%MACHINE_ID% in a title are
			// expanded with the ids of the app that node belongs to
			var cfg = Parse( @"
				<Shared>
					<NodeTemplate Name='log' Filter='Newest' MaxFiles='3' Clearable='1'/>
					<AppTemplate Name='base' ExeFullPath='c:\windows\notepad.exe'>
						<File Template='log' Id='log' Title='Log/%APP_ID% on %machine_id%' Path='D:\logs' Mask='%APP_ID%*.log'/>
					</AppTemplate>
					<App AppIdTuple='m1.camera' Template='base'/>
					<App AppIdTuple='m2.tracker' Template='base'/>
					<Machine Name='m1' IP='127.0.0.1'>
						<FilePackage Id='pack' Title='Pack'>
							<File Template='log' Id='nested' Path='D:\n' Mask='*.log'/>
						</FilePackage>
					</Machine>
				</Shared>" );

			var logs = cfg.VfsNodes.Where( x => x.Id == "log" ).ToList();
			Assert.AreEqual( 2, logs.Count );
			CollectionAssert.AreEquivalent(
				new[] { "Log/camera on m1", "Log/tracker on m2" },
				logs.Select( x => x.Title ).ToArray() );
			foreach( var n in logs )
			{
				Assert.AreEqual( "Newest", n.Filter );
				Assert.IsTrue( n.Clearable );
				StringAssert.Contains( n.Xml!, "Mask=\"%APP_ID%*.log\"", "the mask is expanded later, on the owning machine" );
			}

			var nested = FindNode( cfg, "pack" ).Children.Single();
			Assert.AreEqual( "Newest", nested.Filter );
		}

		[TestMethod()]
		public void TemplatedNodeDeepInsideATemplateIsExpandedTest()
		{
			// a package template holding a folder holding a templated file: the file is two levels below
			// the copied-in child, and must not keep a bare Template= that nothing resolves
			var cfg = Parse( @"
				<Shared>
					<NodeTemplate Name='log' Filter='Newest' MaxFiles='3'/>
					<NodeTemplate Name='bundle'>
						<VFolder Id='inner' Title='Inner'>
							<File Template='log' Id='deep' Path='D:\deep' Mask='*.log'/>
						</VFolder>
					</NodeTemplate>
					<Machine Name='m1' IP='127.0.0.1'>
						<FilePackage Template='bundle' Id='pack' Title='Pack'/>
					</Machine>
				</Shared>" );

			var deep = FindNode( cfg, "pack" ).Children.Single().Children.Single();
			Assert.AreEqual( "deep", deep.Id );
			Assert.AreEqual( "Newest", deep.Filter );
			StringAssert.Contains( deep.Xml!, "MaxFiles=\"3\"" );
			Assert.IsFalse( deep.Xml!.Contains( "Template=" ), "the template reference must be resolved, not left behind" );
		}

		[TestMethod()]
		public void TitleIdsAreLeftAloneWhenUnknownTest()
		{
			var cfg = Parse( @"
				<Shared>
					<File Id='global' Title='Log/%APP_ID%/%MACHINE_ID%/%OTHER%' Path='\\srv\share\a.log'/>
					<FileRef Id='wild' Title='All %APP_ID%' MachineId='*' AppId='*'/>
				</Shared>" );

			Assert.AreEqual( "Log/%APP_ID%/%MACHINE_ID%/%OTHER%", FindNode( cfg, "global" ).Title );
			Assert.AreEqual( "All %APP_ID%", FindNode( cfg, "wild" ).Title );
		}

		static void AssertConfigError( string xml, string expectedInMessage )
		{
			var ex = Assert.ThrowsException<ConfigurationErrorException>( () => Parse( xml ) );
			StringAssert.Contains( ex.Message, expectedInMessage );
		}

		[TestMethod()]
		public void NodeTemplateLoadErrorsTest()
		{
			AssertConfigError( @"<Shared><File Template='nope' Id='x' Path='\\s\h\a'/></Shared>", "'nope' not found" );

			AssertConfigError( @"<Shared>
				<NodeTemplate Name='a' Template='nope'/>
				<File Template='a' Id='x' Path='\\s\h\a'/></Shared>", "'nope' not found" );

			AssertConfigError( @"<Shared>
				<NodeTemplate Name='a' Template='b'/><NodeTemplate Name='b' Template='a'/>
				<File Template='a' Id='x' Path='\\s\h\a'/></Shared>", "a -> b -> a" );

			AssertConfigError( @"<Shared>
				<NodeTemplate Name='a' Template='a'/>
				<File Template='a' Id='x' Path='\\s\h\a'/></Shared>", "a -> a" );

			AssertConfigError( @"<Shared><NodeTemplate MaxFiles='3'/></Shared>", "without a Name" );

			AssertConfigError( @"<Shared><NodeTemplate Name='a'/><NodeTemplate Name='a'/></Shared>", "Duplicate" );

			AssertConfigError( @"<Shared><NodeTemplate Name='a' Guid='6f9619ff-8b86-d011-b42d-00c04fc964ff'/></Shared>", "Guid" );

			// a template whose own child nodes use the template again would never end
			AssertConfigError( @"<Shared>
				<NodeTemplate Name='a'><File Template='a' Id='in' Path='\\s\h\a'/></NodeTemplate>
				<FilePackage Template='a' Id='x'/></Shared>", "a -> a" );
		}

		[TestMethod()]
		public void AppTemplateCycleIsALoadErrorTest()
		{
			// used to recurse until the process died
			AssertConfigError( @"<Shared>
				<AppTemplate Name='a' Template='b'/><AppTemplate Name='b' Template='a'/>
				<App AppIdTuple='m1.x' Template='a' ExeFullPath='c:\x.exe'/></Shared>", "a -> b -> a" );

			AssertConfigError( @"<Shared>
				<AppTemplate Name='a' Template='a'/>
				<App AppIdTuple='m1.x' Template='a' ExeFullPath='c:\x.exe'/></Shared>", "a -> a" );
		}

		[TestMethod()]
		public void DocumentedNodeTemplateExamplesTest()
		{
			// the two examples of docs/Files.md ("Node templates", "Variables in Path, Mask and Title"),
			// as written there
			var cfg = Parse( @"
<Shared>
    <NodeTemplate Name=""log"" Filter=""Newest"" MaxFiles=""3"" MaxSeconds=""604800""
                  TailBytes=""10485760"" Clearable=""1""/>

    <Machine Name=""m1"" IP=""192.168.0.10"">
        <Share Name=""C"" Path=""C:\""/>
        <File Template=""log"" Id=""log.se""  Title=""Log/SE""  Path=""C:\Apps\Viewer\logs"" Mask=""SE*.log""/>
        <File Template=""log"" Id=""log.bdc"" Title=""Log/BDC"" Path=""C:\Apps\Viewer\logs"" Mask=""BDC*.log""
              MaxFiles=""2""/>
    </Machine>

    <AppTemplate Name=""apps.viewer"" StartupDir=""C:\Apps\Viewer\Release\"" ExeFullPath=""C:\x.exe"">
        <File Id=""log"" Title=""Log/%APP_ID%"" Path=""C:\Apps\Viewer\logs"" Mask=""%APP_ID%*.log""
              Filter=""Newest"" MaxFiles=""3""/>
    </AppTemplate>
    <AppTemplate Name=""apps.Viewer_SE"" Template=""apps.viewer"" ExeFullPath=""C:\Apps\Viewer\Release\SE.exe""/>
    <App AppIdTuple=""m1.Viewer_SE"" Template=""apps.Viewer_SE""/>
</Shared>" );

			var se = FindNode( cfg, "log.se" );
			StringAssert.Contains( se.Xml!, "MaxFiles=\"3\"" );
			Assert.AreEqual( 10485760L, se.TailBytes );
			StringAssert.Contains( FindNode( cfg, "log.bdc" ).Xml!, "MaxFiles=\"2\"" );

			var appLog = FindNode( cfg, "log" );
			Assert.AreEqual( "Log/Viewer_SE", appLog.Title );
			Assert.AreEqual( "m1", appLog.MachineId );
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

		[TestMethod()]
		public void AnAppListedInSeveralPlansRegistersItsNodesOnceTest()
		{
			// an app is read once as a default and once per plan; the nodes it brings must not multiply
			var cfg = Parse( @"
				<Shared>
					<AppTemplate Name='base' ExeFullPath='c:\windows\notepad.exe'>
						<File Id='log' Title='Log' Path='D:\logs' Mask='*.log' Filter='Newest' MaxFiles='10'/>
						<Folder Id='dumps' Title='Dumps' Path='D:\dumps' Mask='*.dmp'/>
					</AppTemplate>
					<App AppIdTuple='m1.camera' Template='base'/>
					<Plan Name='p1'><App AppIdTuple='m1.camera' Template='base'/></Plan>
					<Plan Name='p2'><App AppIdTuple='m1.camera' Template='base'/></Plan>
				</Shared>" );

			Assert.AreEqual( 1, cfg.VfsNodes.Count( n => n.Id == "log" ), "each node once" );
			Assert.AreEqual( 1, cfg.VfsNodes.Count( n => n.Id == "dumps" ) );

			var p1 = cfg.Plans.Single( p => p.Name == "p1" ).AppDefs.Single();
			var p2 = cfg.Plans.Single( p => p.Name == "p2" ).AppDefs.Single();

			foreach( var id in new[] { "log", "dumps" } )
			{
				var node = FindNode( cfg, id );
				Assert.AreSame( node, p1.VfsNodes.Single( n => n.Id == id ), $"plan p1 refers to the registered {id}" );
				Assert.AreSame( node, p2.VfsNodes.Single( n => n.Id == id ), "and so does p2" );
			}
		}

		[TestMethod()]
		public void APlanSpecificDefinitionOfANodeStaysSeparateTest()
		{
			var cfg = Parse( @"
				<Shared>
					<AppTemplate Name='base' ExeFullPath='c:\windows\notepad.exe'>
						<File Id='log' Title='Log' Path='D:\logs' Mask='*.log' Filter='Newest' MaxFiles='10'/>
						<Folder Id='dumps' Title='Dumps' Path='D:\dumps' Mask='*.dmp'/>
					</AppTemplate>
					<AppTemplate Name='more' Template='base'>
						<File Id='log' Title='Log' Path='D:\logs' Mask='*.log' Filter='Newest' MaxFiles='99'/>
					</AppTemplate>
					<App AppIdTuple='m1.camera' Template='base'/>
					<Plan Name='p1'><App AppIdTuple='m1.camera' Template='more'/></Plan>
				</Shared>" );

			var logs = cfg.VfsNodes.Where( n => n.Id == "log" ).ToList();
			Assert.AreEqual( 2, logs.Count, "the plan's own definition is another node" );
			Assert.IsTrue( logs.Any( n => n.Xml!.Contains( "MaxFiles=\"10\"" ) ) );
			Assert.IsTrue( logs.Any( n => n.Xml!.Contains( "MaxFiles=\"99\"" ) ) );
			Assert.AreEqual( 1, cfg.VfsNodes.Count( n => n.Id == "dumps" ), "while the identical folder is shared" );
		}

		[TestMethod()]
		public void TwoDifferentExplicitGuidsAreNeverMergedTest()
		{
			var cfg = Parse( @"
				<Shared>
					<Machine Name='m1' IP='127.0.0.1'>
						<Folder Id='a' Guid='11111111-1111-1111-1111-111111111111' Path='D:\x'/>
						<Folder Id='a' Guid='22222222-2222-2222-2222-222222222222' Path='D:\x'/>
					</Machine>
				</Shared>" );

			Assert.AreEqual( 2, cfg.VfsNodes.Count( n => n.Id == "a" ) );
		}

		static void WithRunningVersion( string version, Action act )
		{
			var before = SharedConfigReader.CurrentVersion;
			SharedConfigReader.CurrentVersion = version;
			try { act(); }
			finally { SharedConfigReader.CurrentVersion = before; }
		}

		[TestMethod()]
		public void MinDirigentVersionRefusesAnOlderProgramTest()
		{
			WithRunningVersion( "3.1.18.7", () =>
			{
				var e = Assert.ThrowsException<ConfigurationErrorException>( () => Parse( "<Shared MinDirigentVersion='3.1.19'/>" ) );
				Assert.AreEqual( "This SharedConfig needs Dirigent 3.1.19 or newer; this is 3.1.18.7.", e.Message );
			} );
		}

		[TestMethod()]
		public void MinDirigentVersionAcceptsTheSameOrANewerProgramTest()
		{
			foreach( var running in new[] { "3.1.19", "3.1.19.0", "3.1.19.4", "3.2", "4.0.0.1" } )
				WithRunningVersion( running, () => Parse( "<Shared MinDirigentVersion='3.1.19'/>" ) );

			// missing segments count as 0, in the requirement too
			WithRunningVersion( "3.1.19.4", () => Parse( "<Shared MinDirigentVersion='3.1.19.4.0'/>" ) );
			WithRunningVersion( "3.1.19", () => Assert.ThrowsException<ConfigurationErrorException>( () => Parse( "<Shared MinDirigentVersion='3.1.19.1'/>" ) ) );
		}

		[TestMethod()]
		public void AMalformedMinDirigentVersionIsALoadErrorTest()
		{
			WithRunningVersion( "3.1.19.4", () =>
			{
				var e = Assert.ThrowsException<ConfigurationErrorException>( () => Parse( "<Shared MinDirigentVersion='three'/>" ) );
				StringAssert.Contains( e.Message, "'three'" );
			} );
		}

		[TestMethod()]
		public void AnUnstampedDevelopmentBuildIsNotHeldToMinDirigentVersionTest()
		{
			WithRunningVersion( "1.0.0.0", () => Parse( "<Shared MinDirigentVersion='3.1.19'/>" ) );
		}

		[TestMethod()]
		public void TheTitleTakesTheWorkingFolderNameAndTheLocalVariablesOfTheAppTest()
		{
			var cfg = Parse( @"
				<Shared>
					<AppTemplate Name='base' ExeFullPath='c:\windows\notepad.exe'>
						<File Id='log' Title='Log/%APP_STARTUPDIR_NAME%' Path='C:\IG\Logs\%APP_STARTUPDIR_NAME%' Mask='*.log'/>
						<File Id='cfg' Title='Cfg/%KIND%/%APP_ID%' Path='C:\IG\cfg.xml'/>
						<File Id='odd' Title='Odd/%APP_STARTUPDIR_NAME%/%NOSUCH%' Path='C:\IG\odd.xml'/>
					</AppTemplate>
					<App AppIdTuple='m1.igm1' Template='base' StartupDir='..\RenderHost-3801'><Env><Local Variable='KIND' Value='main'/></Env></App>
					<App AppIdTuple='m1.igm2' Template='base' StartupDir='..\RenderHost-3802\'/>
					<App AppIdTuple='m1.igm3' Template='base' StartupDir='%SOMEWHERE%\Unknown'/>
				</Shared>" );

			string TitleOf( string id, string app ) => cfg.VfsNodes.Single( n => n.Id == id && n.AppId == app ).Title;

			Assert.AreEqual( "Log/RenderHost-3801", TitleOf( "log", "igm1" ) );
			Assert.AreEqual( "Log/RenderHost-3802", TitleOf( "log", "igm2" ), "trailing separators are ignored" );
			Assert.AreEqual( "Cfg/main/igm1", TitleOf( "cfg", "igm1" ), "a <Local> variable of the app" );
			Assert.AreEqual( "Cfg/%KIND%/igm2", TitleOf( "cfg", "igm2" ), "an app without it leaves the token" );
			Assert.AreEqual( "Log/%APP_STARTUPDIR_NAME%", TitleOf( "log", "igm3" ), "a working folder that needs the environment is not guessed" );
			Assert.AreEqual( "Odd/RenderHost-3801/%NOSUCH%", TitleOf( "odd", "igm1" ), "unknown tokens stay as they are" );

			// the path is not touched at load time - the agent expands it, per app
			Assert.AreEqual( @"C:\IG\Logs\%APP_STARTUPDIR_NAME%", cfg.VfsNodes.Single( n => n.Id == "log" && n.AppId == "igm1" ).Path );
		}

		static List<VfsNodeDef> NodesOf( SharedConfig cfg, string app )
			=> cfg.AppDefaults.Single( a => a.Id.ToString() == app ).VfsNodes;

		[TestMethod()]
		public void ADerivedTemplateNodeWithTheSameIdAndTitleReplacesTheInheritedOneTest()
		{
			var cfg = Parse( @"
				<Shared>
					<AppTemplate Name='base' ExeFullPath='c:\windows\notepad.exe'>
						<File Id='log' Title='Log' Path='D:\logs' Mask='*.log' Filter='Newest' MaxFiles='5'/>
						<File Id='cfg' Title='Cfg' Path='D:\cfg.xml'/>
					</AppTemplate>
					<AppTemplate Name='derived' Template='base'>
						<File Id='log' Title='Log' Path='D:\logs' Mask='*.log' Filter='Newest' MaxFiles='50'/>
					</AppTemplate>
					<App AppIdTuple='m1.a' Template='derived'/>
				</Shared>" );

			var nodes = NodesOf( cfg, "m1.a" );
			CollectionAssert.AreEqual( new[] { "log", "cfg" }, nodes.Select( n => n.Id ).ToList(), "replaced in place, not appended" );
			StringAssert.Contains( nodes[0].Xml, "MaxFiles=\"50\"" );

			// and the registry knows the one node only
			Assert.AreEqual( 1, cfg.VfsNodes.Count( n => n.Id == "log" ) );
			Assert.AreSame( nodes[0], cfg.VfsNodes.Single( n => n.Id == "log" ) );
		}

		[TestMethod()]
		public void TheAppItselfReplacesAnInheritedNodeToo()
		{
			var cfg = Parse( @"
				<Shared>
					<AppTemplate Name='base' ExeFullPath='c:\windows\notepad.exe'>
						<File Id='log' Title='Log' Path='D:\logs' MaxFiles='5'/>
					</AppTemplate>
					<App AppIdTuple='m1.a' Template='base'><File Id='log' Title='Log' Path='D:\logs' MaxFiles='7'/></App>
					<App AppIdTuple='m1.b' Template='base'/>
				</Shared>" );

			var a = NodesOf( cfg, "m1.a" ).Single();
			StringAssert.Contains( a.Xml, "MaxFiles=\"7\"" );
			StringAssert.Contains( NodesOf( cfg, "m1.b" ).Single().Xml, "MaxFiles=\"5\"", "another app keeps the inherited one" );
		}

		[TestMethod()]
		public void ADifferentTitleOrNoIdAddsInsteadOfReplacingTest()
		{
			var cfg = Parse( @"
				<Shared>
					<AppTemplate Name='base' ExeFullPath='c:\windows\notepad.exe'>
						<File Id='log' Title='Log' Path='D:\logs' MaxFiles='5'/>
					</AppTemplate>
					<AppTemplate Name='otherTitle' Template='base'>
						<File Id='log' Title='Other title' Path='D:\logs' MaxFiles='50'/>
					</AppTemplate>
					<AppTemplate Name='noId' Template='base'>
						<File Title='Log' Path='D:\logs' MaxFiles='60'/>
					</AppTemplate>
					<App AppIdTuple='m1.a' Template='otherTitle'/>
					<App AppIdTuple='m1.b' Template='noId'/>
				</Shared>" );

			Assert.AreEqual( 2, NodesOf( cfg, "m1.a" ).Count, "another title: both kept" );
			Assert.AreEqual( 2, NodesOf( cfg, "m1.b" ).Count, "no Id: both kept" );
		}
	}
}
