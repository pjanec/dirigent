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
					<Folder Id='a' Guid='11111111-1111-1111-1111-111111111111' Path='D:\x'/>
					<Folder Id='a' Guid='22222222-2222-2222-2222-222222222222' Path='D:\x'/>
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
		public void TheSameIdReplacesWhateverTheTitleAndANewIdOrNoIdAddsTest()
		{
			var cfg = Parse( @"
				<Shared>
					<AppTemplate Name='base' ExeFullPath='c:\windows\notepad.exe'>
						<File Id='log' Title='Log' Path='D:\logs' MaxFiles='5'/>
						<File Id='cfg' Title='Cfg' Path='D:\cfg'/>
					</AppTemplate>
					<AppTemplate Name='renamed' Template='base'>
						<File Id='log' Title='Other title' Path='D:\logs' MaxFiles='50'/>
					</AppTemplate>
					<AppTemplate Name='newId' Template='base'>
						<File Id='log.more' Title='Log' Path='D:\more' MaxFiles='60'/>
					</AppTemplate>
					<AppTemplate Name='noId' Template='base'>
						<File Title='Log' Path='D:\noid'/>
					</AppTemplate>
					<App AppIdTuple='m1.a' Template='renamed'/>
					<App AppIdTuple='m1.b' Template='newId'/>
					<App AppIdTuple='m1.c' Template='noId'/>
				</Shared>" );

			var a = NodesOf( cfg, "m1.a" );
			CollectionAssert.AreEqual( new[] { "log", "cfg" }, a.Select( n => n.Id ).ToList(), "replaced in place" );
			Assert.AreEqual( "Other title", a[0].Title, "whatever the title" );
			StringAssert.Contains( a[0].Xml, "MaxFiles=\"50\"" );

			CollectionAssert.AreEqual( new[] { "log", "cfg", "log.more" }, NodesOf( cfg, "m1.b" ).Select( n => n.Id ).ToList(), "a new Id is added" );
			Assert.AreEqual( 3, NodesOf( cfg, "m1.c" ).Count, "no Id: added" );
		}

		[TestMethod()]
		public void TwoDefinitionsOfOneIdOnOneLevelAreALoadErrorTest()
		{
			string Fail( string xml ) => Assert.ThrowsException<ConfigurationErrorException>( () => Parse( xml ) ).Message;

			StringAssert.Contains(
				Fail( @"<Shared><AppTemplate Name='apps.IOS' ExeFullPath='x.exe'><File Id='log' Path='C:\1'/><File Id='log' Path='C:\2'/></AppTemplate>
						<App AppIdTuple='m1.a' Template='apps.IOS'/></Shared>" ),
				"Duplicate file definition Id 'log' in AppTemplate 'apps.IOS'" );

			StringAssert.Contains(
				Fail( @"<Shared><App AppIdTuple='m1.a' ExeFullPath='x.exe'><File Id='log' Path='C:\1'/><Folder Id='log' Path='C:\2'/></App></Shared>" ),
				"Duplicate file definition Id 'log' in App 'm1.a'" );

			StringAssert.Contains(
				Fail( @"<Shared><MachineTemplate Name='m.base'><File Id='log' Path='C:\1'/><File Id='log' Path='C:\2'/></MachineTemplate>
						<Machine Name='a' IP='1.1.1.1'/></Shared>" ),
				"Duplicate file definition Id 'log' in MachineTemplate 'm.base'" );

			StringAssert.Contains(
				Fail( @"<Shared><Machine Name='a' IP='1.1.1.1'><File Id='log' Path='C:\1'/><File Id='log' Path='C:\2'/></Machine></Shared>" ),
				"Duplicate file definition Id 'log' in Machine 'a'" );

			// also when the machine uses a template, and when the second one comes out of a node template
			StringAssert.Contains(
				Fail( @"<Shared><NodeTemplate Name='n' Id='log'/><MachineTemplate Name='m.base'/>
						<Machine Name='a' IP='1.1.1.1' Template='m.base'><File Id='log' Path='C:\1'/><File Template='n' Path='C:\2'/></Machine></Shared>" ),
				"Duplicate file definition Id 'log' in Machine 'a'" );

			// the same Id in a template and in the machine, or on two machines, is not a duplicate
			Parse( @"<Shared><MachineTemplate Name='m.base'><File Id='log' Path='C:\1'/></MachineTemplate>
					<Machine Name='a' IP='1.1.1.1' Template='m.base'><File Id='log' Path='C:\2'/></Machine>
					<Machine Name='b' IP='1.1.1.2' Template='m.base'/></Shared>" );
		}

		// ---- machine templates --------------------------------------------------------------------

		const string SiteConfig = @"
			<Shared>
				<NodeTemplate Name='log.dirigent' Id='log.dirigent' Title='Log/Dirigent agent' Path='C:\Dirigent\logs' Mask='*.log'/>

				<MachineTemplate Name='m.base' MAC='00-11-22-33-44-55'>
					<Share Name='C' Path='C:\'/>
					<Folder Template='log.dirigent'/>
					<File Id='cfg' Title='Config' Path='C:\cfg.xml'/>
					<File Id='ver' Title='Version' Path='C:\ver.txt'/>
					<Folder Id='crash' Title='Crash dumps' Path='C:\dumps' Mask='*.dmp'/>
				</MachineTemplate>
				<MachineTemplate Name='m.ios' Template='m.base'>
					<File Id='ios1' Title='IOS one' Path='C:\ios1.log'/>
					<File Id='ios2' Title='IOS two' Path='C:\ios2.log'/>
				</MachineTemplate>
				<MachineTemplate Name='m.viewer'>
					<File Id='viewer1' Title='Viewer one' Path='C:\viewer1.log'/>
				</MachineTemplate>

				<Machine Name='a' IP='10.0.0.1' Template='m.ios'/>
				<Machine Name='b' IP='10.0.0.2' Template='m.ios;m.viewer'/>
				<Machine Name='c' IP='10.0.0.3' Template='m.ios'>
					<Share Name='C' Path='D:\'/>
					<Share Name='X' Path='X:\'/>
					<Folder Template='log.dirigent' Title='Log/Dirigent agent and MASTER'/>
					<File Id='own' Title='Own' Path='C:\own.txt'/>
				</Machine>
				<Machine Name='d' IP='10.0.0.4' MAC='AA-BB-CC-DD-EE-FF' Template='m.base'/>
				<Machine Name='plain' IP='10.0.0.5'><File Id='only' Path='C:\only.txt'/></Machine>
			</Shared>";

		static List<string> Ids( SharedConfig cfg, string machine )
			=> cfg.Machines.Single( m => m.Id == machine ).VfsNodes.Select( n => n.Id ).ToList();

		[TestMethod()]
		public void MachinesGetTheSharesAndNodesOfTheirTemplatesTest()
		{
			var cfg = Parse( SiteConfig );

			// m.ios = m.base + 2
			CollectionAssert.AreEqual( new[] { "log.dirigent", "cfg", "ver", "crash", "ios1", "ios2" }, Ids( cfg, "a" ) );
			CollectionAssert.AreEqual( new[] { "C" }, cfg.Machines.Single( m => m.Id == "a" ).FileShares.Select( s => s.Name ).ToList() );

			// a mixin comes after
			CollectionAssert.AreEqual( new[] { "log.dirigent", "cfg", "ver", "crash", "ios1", "ios2", "viewer1" }, Ids( cfg, "b" ) );

			// the template of the node gave it its title and its path
			var log = cfg.Machines.Single( m => m.Id == "a" ).VfsNodes[0];
			Assert.AreEqual( "Log/Dirigent agent", log.Title );
			Assert.AreEqual( @"C:\Dirigent\logs", log.Path );
			Assert.AreEqual( "a", log.MachineId );

			// a machine without a template is read as it always was
			CollectionAssert.AreEqual( new[] { "only" }, Ids( cfg, "plain" ) );
		}

		[TestMethod()]
		public void AMachineReplacesAnInheritedNodeEvenWithAnotherTitleTest()
		{
			var cfg = Parse( SiteConfig );

			var c = cfg.Machines.Single( m => m.Id == "c" ).VfsNodes;
			Assert.AreEqual( 1, c.Count( n => n.Id == "log.dirigent" ), "replaced, not duplicated" );
			Assert.AreEqual( "log.dirigent", c[0].Id, "in the place of the inherited one" );
			Assert.AreEqual( "Log/Dirigent agent and MASTER", c[0].Title );
			Assert.AreEqual( @"C:\Dirigent\logs", c[0].Path, "the rest still comes from the node template" );

			CollectionAssert.AreEqual( new[] { "log.dirigent", "cfg", "ver", "crash", "ios1", "ios2", "own" }, Ids( cfg, "c" ) );

			// the registry holds the final nodes only
			Assert.AreEqual( 1, cfg.VfsNodes.Count( n => n.Id == "log.dirigent" && n.MachineId == "c" ) );
			Assert.AreEqual( 7, cfg.VfsNodes.Count( n => n.MachineId == "c" ) );
		}

		[TestMethod()]
		public void AShareOfTheSameNameReplacesTheInheritedOneTest()
		{
			var shares = Parse( SiteConfig ).Machines.Single( m => m.Id == "c" ).FileShares;

			CollectionAssert.AreEqual( new[] { "C", "X" }, shares.Select( s => s.Name ).ToList() );
			Assert.AreEqual( @"D:\", shares[0].Path, "the machine's C, not the template's" );
		}

		[TestMethod()]
		public void TemplateAttributesAreDefaultsAndTheMachinesOwnWinTest()
		{
			var cfg = Parse( SiteConfig );

			Assert.AreEqual( "00-11-22-33-44-55", cfg.Machines.Single( m => m.Id == "a" ).MAC );
			Assert.AreEqual( "AA-BB-CC-DD-EE-FF", cfg.Machines.Single( m => m.Id == "d" ).MAC );
			Assert.AreEqual( "10.0.0.1", cfg.Machines.Single( m => m.Id == "a" ).IP );
		}

		[TestMethod()]
		public void AFileRefFindsANodeABaseTemplateBroughtIntoTheMachineTest()
		{
			var cfg = Parse( SiteConfig );

			var reg = new FileRegistry( new RefStub(), "a", @"C:\", ( id ) => "127.0.0.1" );
			reg.SetVfsNodes( cfg.VfsNodes );
			reg.SetMachines( cfg.Machines );

			var resolved = reg.ResolveAsync( null!, new FileRef() { Guid = Guid.NewGuid(), Id = "cfg", MachineId = "a", AppId = "*" }, false, false, null )
								.GetAwaiter().GetResult();

			Assert.IsNotNull( resolved );
			Assert.AreEqual( @"C:\cfg.xml", resolved!.Path );
		}

		class RefStub : IDirig
		{
			public string Name => "a";

			public Task<TResult?> RunScriptAsync<TArgs, TResult>( string clientId, string scriptName,
					string? sourceCode, TArgs? args, string title, out Guid scriptInstance )
				=> throw new NotImplementedException();

			public Task<VfsNodeDef?> ResolveAsync( VfsNodeDef nodeDef, bool forceUNC, bool includeContent )
				=> throw new NotImplementedException();
		}

		[TestMethod()]
		public void MachineTemplateLoadErrorsNameTheProblemTest()
		{
			string Fail( string xml ) => Assert.ThrowsException<ConfigurationErrorException>( () => Parse( xml ) ).Message;

			StringAssert.Contains( Fail( "<Shared><Machine Name='a' IP='1.1.1.1' Template='m.nosuch'/></Shared>" ), "'m.nosuch' not found" );

			StringAssert.Contains(
				Fail( "<Shared><MachineTemplate Name='m.a' Template='m.b'/><MachineTemplate Name='m.b' Template='m.a'/><Machine Name='a' IP='1.1.1.1' Template='m.a'/></Shared>" ),
				"cycle: m.a -> m.b -> m.a" );

			// a cycle through the mixins of a template
			StringAssert.Contains(
				Fail( "<Shared><MachineTemplate Name='m.a' Template='m.b;m.c'/><MachineTemplate Name='m.b'/><MachineTemplate Name='m.c' Template='m.a'/><Machine Name='a' IP='1.1.1.1' Template='m.a'/></Shared>" ),
				"cycle: m.a -> m.c -> m.a" );

			StringAssert.Contains( Fail( "<Shared><MachineTemplate Name='m.a' IP='1.2.3.4'/></Shared>" ), "'m.a' must not have an IP" );
			StringAssert.Contains( Fail( "<Shared><MachineTemplate Name='m.a' Guid='11111111-1111-1111-1111-111111111111'/></Shared>" ), "'m.a' must not have a Guid" );
			StringAssert.Contains( Fail( "<Shared><MachineTemplate Name='m.a'/><MachineTemplate Name='m.a'/></Shared>" ), "Duplicate MachineTemplate 'm.a'" );
			StringAssert.Contains( Fail( "<Shared><MachineTemplate Share='x'/></Shared>" ), "without a Name" );
		}

		[TestMethod()]
		public void AKindPrefixWildcardMatchesTheKindOnlyTest()
		{
			// what docs/Files.md says of FileRef Id="log.*"
			bool Matches( string pattern, string id ) => System.IO.Enumeration.FileSystemName.MatchesSimpleExpression( pattern, id );

			Assert.IsTrue( Matches( "log.*", "log.viewer" ) );
			Assert.IsTrue( Matches( "log.*", "log.mt.ios" ) );
			Assert.IsTrue( Matches( "log.*", "LOG.Viewer" ), "letter case does not matter" );
			Assert.IsFalse( Matches( "log.*", "logs.x" ) );
			Assert.IsFalse( Matches( "log.*", "log" ) );
			Assert.IsTrue( Matches( "log.?iewer", "log.viewer" ) );
		}
	}
}
