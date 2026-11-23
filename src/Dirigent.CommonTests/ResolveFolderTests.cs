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

			public Task<VfsNodeDef?> ResolveAsync( VfsNodeDef nodeDef, bool forceUNC, bool includeContent, FileWindow? window = null )
				=> throw new NotImplementedException();

			// the one app the mask tests refer to; what the registry asks for to build the variables
			public AppDef? GetAppDef( AppIdTuple id )
				=> id.AppId == "camera"
					? new AppDef() { Id = id, ExeFullPath = @"C:\apps\camera\camera.exe", StartupDir = @"C:\apps\camera",
									 EnvVarsToSet = new Dictionary<string, string>() { { "CAM_KIND", "front" } } }
				: id.AppId == "igm"
					// an app with <Local> variables, a <Set> one of the same name, a plan and a working folder with a name
					? new AppDef() { Id = id, ExeFullPath = @"C:\apps\igm\igm.exe", StartupDir = @"..\RenderHost-3801", PlanName = "p1",
									 LocalVarsToSet = new Dictionary<string, string>() { { "LOCALV", "lv" }, { "CLASH", "fromlocal" } },
									 EnvVarsToSet = new Dictionary<string, string>() { { "CLASH", "fromset" } } }
					: null;
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

		/// <summary>The path a File node of the app (or of the machine) resolves to.</summary>
		string PathOf( string path, string? appId = "igm" )
			=> Resolve( new FileDef() { Id = "f", Title = "F", MachineId = _machineId, AppId = appId, Path = path } ).Path!;

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
		public void APathSeesTheLocalVariablesOfItsAppTest()
		{
			// the launcher's own set: what the app's command line could use, a path can use
			_reg.MasterIP = "10.9.8.7";

			Assert.AreEqual( @"D:\x\lv\f", PathOf( @"D:\x\%LOCALV%\f" ) );
			Assert.AreEqual( @"D:\x\p1\10.9.8.7", PathOf( @"D:\x\%DIRIGENT_PLAN%\%DIRIGENT_MASTER_IP%" ) );
			Assert.AreEqual( @"D:\x\igm", PathOf( @"D:\x\%APP_ID%" ) );
		}

		[TestMethod()]
		public void TheNameOfTheWorkingFolderIsAVariableTest()
		{
			Assert.AreEqual( @"C:\IG\Logs\RenderHost-3801", PathOf( @"C:\IG\Logs\%APP_STARTUPDIR_NAME%" ) );
		}

		[TestMethod()]
		public void ASetVariableWinsOverALocalOneOfTheSameNameTest()
		{
			// <Set> goes to the process environment and always won in a path; that has not changed
			Assert.AreEqual( @"D:\x\fromset", PathOf( @"D:\x\%CLASH%" ) );
		}

		[TestMethod()]
		public void TheGlobalVariablesAreForNodesOfAMachineToo()
		{
			_reg.GlobalExpansionVars = new Dictionary<string, string>() { { "GLOBALV", "gv" } };

			Assert.AreEqual( @"D:\x\gv", PathOf( @"D:\x\%GLOBALV%", appId: null ), "a node of the machine" );
			Assert.AreEqual( @"D:\x\gv", PathOf( @"D:\x\%GLOBALV%" ), "a node of an app" );
		}

		[TestMethod()]
		public void TheFileOnlyVariablesAreStillThereTest()
		{
			var downloads = Path.Combine( _root, "dl" );
			var reg = new FileRegistry( new DirigStub(), _machineId, _root, ( machineId ) => "127.0.0.1", downloads );
			reg.SetMachines( new List<MachineDef>() { new MachineDef() { Id = _machineId, IP = "192.168.7.7" } } );

			var node = new FileDef() { Id = "f", Title = "F", MachineId = _machineId, AppId = "igm", Path = @"%DOWNLOADS%\%MACHINE_ID%\%MACHINE_IP%" };
			var resolved = reg.ResolveAsync( null!, node, false, true, null ).GetAwaiter().GetResult();

			Assert.AreEqual( Path.Combine( downloads, "m1", "192.168.7.7" ), resolved!.Path );
		}

		[TestMethod()]
		public void AnUnknownVariableIsStillRemovedTest()
		{
			Assert.AreEqual( @"D:\x\\f", PathOf( @"D:\x\%NO_SUCH_VARIABLE%\f" ), "the empty value leaves the separators" );
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
		public void WildcardReferenceReachesAMachineScopedNodeTest()
		{
			// A node declared under <Machine> has no app at all, and the per-app collection
			// convention is AppId="*". Whether that reaches such a node decides whether the files of
			// something Dirigent does not launch - Dirigent itself, say - can be collected without a
			// reference of their own.
			MakeFile( "dirigent.log", 0 );

			_reg.SetVfsNodes( new List<VfsNodeDef>()
			{
				new FileDef()
				{
					Guid = Guid.NewGuid(),
					Id = "log",
					MachineId = _machineId,
					AppId = null,     // exactly what LoadMachines gives a machine-scoped node
					Path = Path.Combine( _root, "dirigent.log" ),
				},
			} );

			var resolved = Resolve( new FileRef()
			{
				Guid = Guid.NewGuid(),
				Id = "log",
				MachineId = "*",
				AppId = "*",
			} );

			Assert.AreEqual( Path.Combine( _root, "dirigent.log" ), resolved.Path,
				"'*' matches a node with no app, so a machine-scoped node is collected by the "
				+ "ordinary per-app wildcard as long as its Id matches" );
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
		public void NewestMaskExpandsVariablesTest()
		{
			// the mask is expanded like the path: the app variables, the app's own <Set> vars, the OS environment
			MakeFile( "camera_1.log", 3 );
			MakeFile( "camera_2.log", 1 );
			MakeFile( "tracker_9.log", 0 );
			MakeFile( "front.log", 0 );
			MakeFile( "rear.log", 0 );
			Environment.SetEnvironmentVariable( "DIRIGENT_TEST_MASK_EXT", "log" );

			var byAppId = Resolve( new FileDef()
			{
				Id = "log", Title = "Recent logs", MachineId = _machineId, AppId = "camera", Path = _root,
				Filter = "Newest", Xml = @"<File Mask=""%APP_ID%_*.log"" MaxFiles=""5""/>",
			} );
			CollectionAssert.AreEqual( new List<string>() { "camera_2.log", "camera_1.log" },
				byAppId.Children.Select( x => Path.GetFileName( x.Path! ) ).ToList() );

			var byEnv = Resolve( new FileDef()
			{
				Id = "log", MachineId = _machineId, AppId = "camera", Path = _root,
				Filter = "Newest", Xml = @"<File Mask=""%CAM_KIND%.%DIRIGENT_TEST_MASK_EXT%"" MaxFiles=""1""/>",
			} );
			Assert.AreEqual( Path.Combine( _root, "front.log" ), byEnv.Path );
		}

		[TestMethod()]
		public void FolderMaskExpandsVariablesTest()
		{
			MakeFile( "camera_1.log", 1 );
			MakeFile( "camera_2.log", 1 );
			MakeFile( "tracker_9.log", 1 );

			var resolved = Resolve( new FolderDef()
			{
				Id = "logs", MachineId = _machineId, AppId = "camera", Path = _root, Mask = "%APP_ID%_*.log",
			} );

			CollectionAssert.AreEquivalent( new List<string>() { "camera_1.log", "camera_2.log" }, FlattenFiles( resolved ) );
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

		// ---- a time window chooses the files of the Clearable definitions ----

		static readonly DateTime _runsAt = new DateTime( 2026, 11, 21, 12, 0, 0, DateTimeKind.Utc );

		/// <summary>Three runs of an application, each its own log: 12:00-13:00, 14:00-15:00 and 16:00-17:00.</summary>
		void MakeThreeRuns()
		{
			for( int i = 0; i < 3; i++ )
			{
				var p = Path.Combine( _root, $"run{i + 1}.log" );
				File.WriteAllText( p, "x" );
				File.SetCreationTimeUtc( p, _runsAt.AddHours( 2 * i ) );
				File.SetLastWriteTimeUtc( p, _runsAt.AddHours( 2 * i + 1 ) );
			}
		}

		FileDef NewestRunLog( bool clearable, int maxFiles = 1 ) => new FileDef()
		{
			Id = "log", Title = "Logs", MachineId = _machineId, AppId = "camera", Path = _root, Filter = "Newest",
			Xml = $@"<File Mask=""run*.log"" MaxFiles=""{maxFiles}"" MaxSeconds=""172800""/>", Clearable = clearable,
		};

		VfsNodeDef? ResolveIn( VfsNodeDef def, FileWindow? window )
			=> _reg.ResolveAsync( null!, def, false, true, null, window ).GetAwaiter().GetResult();

		// 14:10 - 14:50, inside the second run only
		static readonly FileWindow _middleRun = new FileWindow() { SinceUtc = _runsAt.AddMinutes( 2 * 60 + 10 ), UntilUtc = _runsAt.AddMinutes( 2 * 60 + 50 ) };

		[TestMethod()]
		public void TheWindowChoosesTheFileOfAClearableNewestDefinitionTest()
		{
			MakeThreeRuns();

			var resolved = ResolveIn( NewestRunLog( clearable: true ), _middleRun );

			Assert.IsNotNull( resolved );
			Assert.IsFalse( resolved!.IsContainer, "one file, as MaxFiles=1 says" );
			Assert.AreEqual( Path.Combine( _root, "run2.log" ), resolved.Path, "the one the window falls in, not the newest" );
		}

		[TestMethod()]
		public void TheWindowOverridesMaxFilesWhenSeveralFilesFallInItTest()
		{
			MakeThreeRuns();

			// the afternoon of the 21st: two runs, though only one file is asked for
			var window = new FileWindow() { SinceUtc = _runsAt.AddHours( 2 ), UntilUtc = _runsAt.AddHours( 7 ) };
			var resolved = ResolveIn( NewestRunLog( clearable: true ), window );

			Assert.IsTrue( resolved!.IsContainer );
			CollectionAssert.AreEquivalent( new[] { "run2.log", "run3.log" }, resolved.Children.Select( x => Path.GetFileName( x.Path! ) ).ToList() );

			// a window that holds no file: nothing
			Assert.IsNull( ResolveIn( NewestRunLog( clearable: true ),
				new FileWindow() { SinceUtc = _runsAt.AddDays( 3 ), UntilUtc = _runsAt.AddDays( 4 ) } ) );
		}

		[TestMethod()]
		public void TheWindowDoesNotTouchANonClearableDefinitionTest()
		{
			MakeThreeRuns();

			// a config is whatever its own limits say it is, window or not
			Assert.AreEqual( Path.Combine( _root, "run3.log" ), ResolveIn( NewestRunLog( clearable: false ), _middleRun )!.Path );
		}

		[TestMethod()]
		public void WithoutAWindowTheLimitsChooseAsEverTest()
		{
			MakeThreeRuns();

			Assert.AreEqual( Path.Combine( _root, "run3.log" ), ResolveIn( NewestRunLog( clearable: true ), null )!.Path );
		}

		[TestMethod()]
		public void TheWindowChoosesTheFilesOfAClearableFolderTest()
		{
			MakeThreeRuns();

			FolderDef Folder( bool clearable ) => new FolderDef()
			{
				Id = "dir", Title = "Dir", MachineId = _machineId, Path = _root, Mask = "run*.log", MaxFiles = 1, Clearable = clearable,
			};

			CollectionAssert.AreEqual( new[] { "run2.log" }, ResolveIn( Folder( true ), _middleRun )!.Children.Select( x => Path.GetFileName( x.Path! ) ).ToList() );
			CollectionAssert.AreEqual( new[] { "run3.log" }, ResolveIn( Folder( false ), _middleRun )!.Children.Select( x => Path.GetFileName( x.Path! ) ).ToList(), "a non-clearable folder is not windowed" );
		}

		[TestMethod()]
		public void APlainClearableFileOutsideTheWindowIsLeftOutTest()
		{
			MakeThreeRuns();

			FileDef Plain( string name, bool clearable ) => new FileDef()
			{
				Id = "f", Title = "F", MachineId = _machineId, Path = Path.Combine( _root, name ), Clearable = clearable,
			};

			Assert.IsNotNull( ResolveIn( Plain( "run2.log", true ), _middleRun ) );
			Assert.IsNull( ResolveIn( Plain( "run1.log", true ), _middleRun ), "written before the window: out, like a missing file" );
			Assert.IsNotNull( ResolveIn( Plain( "run1.log", false ), _middleRun ), "a non-clearable file is always there" );
			Assert.IsNotNull( ResolveIn( Plain( "run1.log", true ), null ), "and without a window nothing changes" );
		}
	}
}
