using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using Dirigent.Web;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// `GetAppFiles` - the actual files of one application instance, with machine-local and UNC
	/// paths. The same AppFilesQuery answers GET /api/appdefs/{id}/files; that road is smoked in the
	/// system tests, these go through the CLI because that is the in-process way to reach it.
	/// </summary>
	/// <remarks>
	/// Every machine of a tier-1 bed is this one, so the master resolves all nodes itself; what is
	/// pinned here is the choice of definition, the flattening, the UNC step and the per-node errors.
	/// </remarks>
	[TestClass()]
	public class AppFilesQueryTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 30 );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		static string SystemDriveRoot => Path.GetPathRoot( Path.GetTempPath() )!;

		/// <summary>
		/// m1.a has a folder on m1 (which has a share) and one on m2 (which has none); plan "planX"
		/// has its own, different definition of the same application.
		/// </summary>
		static Scenario World()
			=> Scenario.TwoMachines()
				.Share( "m1", "C", SystemDriveRoot )
				.Share( "m2", "pk2", "{temp}\\pk2" )
				.App( "m1.a", a => a.LongRunning().RawXml(
					"<Folder Id='logs' Title='Logs' Path='{applogs}' Mask='*.log'/>"
					+ "<Folder Id='remote' Title='Remote' MachineId='{m2}' Path='{temp}\\m2files' Mask='*.log'/>"
					+ "<Folder Id='offline' Title='Offline' MachineId='ghost' Path='C:/x' Mask='*.log'/>" ) )
				.RawXml(
					"<Plan Name='planX'><App AppIdTuple='{m1}.a' ExeFullPath='{testapp}' CmdLineArgs='--run-forever' StartupDir='{temp}'>"
					+ "<Folder Id='plannode' Title='PlanLogs' Path='{temp}\\planfiles' Mask='*.log'/></App></Plan>" )
				.RawXml(
					"<FilePackage Id='pkg' Title='Pkg' Description='First package'>"
					+ "<Folder Id='pk1' Title='M1' MachineId='{m1}' Path='{temp}\\pk1' Mask='*.log'/>"
					+ "<Folder Id='pk2' Title='M2' MachineId='{m2}' Path='{temp}\\pk2' Mask='*.log'/>"
					+ "<Folder Id='pk3' Title='M3' MachineId='ghost' Path='C:/x' Mask='*.log'/>"
					+ "</FilePackage>" )
				.RawXml(
					"<FilePackage Id='pkg2' Title='Pkg two'>"
					+ "<Folder Id='pk1b' Title='M1 again' MachineId='{m1}' Path='{temp}\\pk1' Mask='*.log'/>"
					+ "</FilePackage>" )
				.RawXml( "<File Id='tailed' Title='Tailed' MachineId='{m1}' Path='{temp}\\single\\big.txt' TailBytes='10'/>" );

		static async Task<TestBed.TestBed> StartBed()
		{
			var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World() } );

			// the files the nodes point at
			var logs = bed.RenderContext.AppLogsDir( "m1", "a" );
			Directory.CreateDirectory( Path.Combine( logs, "sub" ) );
			File.WriteAllText( Path.Combine( logs, "sub", "a.log" ), "a" );
			File.WriteAllText( Path.Combine( logs, "top.log" ), "t" );

			Directory.CreateDirectory( Path.Combine( bed.TempRoot, "m2files" ) );
			File.WriteAllText( Path.Combine( bed.TempRoot, "m2files", "r.log" ), "r" );

			Directory.CreateDirectory( Path.Combine( bed.TempRoot, "planfiles" ) );
			File.WriteAllText( Path.Combine( bed.TempRoot, "planfiles", "p.log" ), "p" );

			foreach( var d in new[] { "pk1", "pk2" } )
			{
				Directory.CreateDirectory( Path.Combine( bed.TempRoot, d ) );
				File.WriteAllText( Path.Combine( bed.TempRoot, d, d + ".log" ), d );
			}

			Directory.CreateDirectory( Path.Combine( bed.TempRoot, "single" ) );
			File.WriteAllText( Path.Combine( bed.TempRoot, "single", "big.txt" ), string.Concat( Enumerable.Range( 0, 10 ).Select( i => $"L{i}\n" ) ) ); // 30 bytes

			return bed;
		}

		static FileDefFilesResult ParseNode( string line, string id )
		{
			var prefix = $"FILES:{id}:";
			StringAssert.StartsWith( line, prefix, $"the answer line: {line}" );
			return JsonSerializer.Deserialize<FileDefFilesResult>( line.Substring( prefix.Length ) )!;
		}

		static AppFilesResult Parse( string line, string idAsGiven )
		{
			var prefix = $"APPFILES:{idAsGiven}:";
			StringAssert.StartsWith( line, prefix, $"the answer line: {line}" );
			return JsonSerializer.Deserialize<AppFilesResult>( line.Substring( prefix.Length ) )!;
		}

		static async Task<AppFilesResult> Ask( TestBed.TestBed bed, string idAsGiven, string extra = "" )
		{
			var lines = await bed.SendCliCommandAsync( $"GetAppFiles {idAsGiven} {extra}", Timeout );
			Assert.AreEqual( 2, lines.Count, string.Join( " | ", lines ) );
			Assert.AreEqual( "END", lines[1] );
			return Parse( lines[0], idAsGiven );
		}

		[TestMethod()]
		public async Task AnAppInAPlanListsItsFilesWithLocalAndUncPaths()
		{
			using var bed = await StartBed();

			var res = await Ask( bed, "m1.a" + "@planX" );

			Assert.AreEqual( "planX", res.plan );
			Assert.AreEqual( "requested", res.planSource );
			Assert.AreEqual( 1, res.fileDefs.Count );
			Assert.AreEqual( "plannode", res.fileDefs[0].id );

			// the same through the real socket, which is how the CLI exe and PowerShell get it
			using var cli = new CliSession( bed.CliPort );
			var viaSocket = cli.RequestList( "GetAppFiles m1.a@planX" );
			Assert.AreEqual( 1, viaSocket.Count );
			Assert.AreEqual( "plannode", Parse( viaSocket[0], "m1.a@planX" ).fileDefs[0].id );
		}

		[TestMethod()]
		public async Task AFolderNodeIsFlattenedIntoFilesEachWithItsLocalAndUncPath()
		{
			using var bed = await StartBed();

			var res = await Ask( bed, "m1.a" );

			Assert.AreEqual( "none", res.planSource );
			var logs = res.fileDefs.Single( n => n.id == "logs" );
			Assert.IsNull( logs.error, logs.error );
			Assert.AreEqual( "Folder", logs.type );

			var folder = bed.RenderContext.AppLogsDir( "m1", "a" );
			Assert.AreEqual( folder, logs.localPath );

			var files = logs.files.OrderBy( f => f.path ).ToList();
			CollectionAssert.AreEqual( new[] { "Logs/sub/a.log", "Logs/top.log" }, files.Select( f => f.path ).ToList() );

			var a = files[0];
			Assert.AreEqual( Path.Combine( folder, "sub", "a.log" ), a.localPath );
			Assert.AreEqual( "m1", a.machine );

			var rel = a.localPath!.Substring( SystemDriveRoot.Length );
			Assert.AreEqual( @"\\127.0.0.1\C\" + rel, a.uncPath, "through the share of m1" );
			Assert.IsNull( logs.uncError );
		}

		[TestMethod()]
		public async Task WithoutContentOnlyTheNodesAreResolved()
		{
			using var bed = await StartBed();

			var res = await Ask( bed, "m1.a", "content=0" );

			var logs = res.fileDefs.Single( n => n.id == "logs" );
			Assert.AreEqual( bed.RenderContext.AppLogsDir( "m1", "a" ), logs.localPath );
			Assert.AreEqual( 0, logs.files.Count, "no folder scan" );
		}

		[TestMethod()]
		public async Task AnAppThatRanInAPlanAnswersFromThatPlansDefinition()
		{
			using var bed = await StartBed();

			var before = await Ask( bed, "m1.a" );
			Assert.AreEqual( "none", before.planSource );
			CollectionAssert.AreEquivalent( new[] { "logs", "remote", "offline" }, before.fileDefs.Select( n => n.id ).ToList() );

			await bed.Operator.StartPlanAsync( "planX" );
			await bed.WaitUntilAsync(
				async () =>
				{
					var st = await bed.Operator.GetAppStateAsync( bed.App( "m1", "a" ) );
					return ( st?.Running ?? false ) && st.PlanName == "planX";
				},
				Timeout, "m1.a runs as part of planX" );

			var res = await Ask( bed, "m1.a" );

			Assert.AreEqual( "lastRun", res.planSource );
			Assert.AreEqual( "planX", res.plan );
			CollectionAssert.AreEqual( new[] { "plannode" }, res.fileDefs.Select( n => n.id ).ToList() );
			Assert.AreEqual( "PlanLogs/p.log", res.fileDefs[0].files.Single().path );
		}

		[TestMethod()]
		public async Task AFileNoShareCoversHasNoUncPathButStillItsLocalOne()
		{
			using var bed = await StartBed();

			var res = await Ask( bed, "m1.a" );

			var remote = res.fileDefs.Single( n => n.id == "remote" ); // m2 declares no share
			Assert.IsNull( remote.error, remote.error );
			Assert.IsNotNull( remote.localPath );
			Assert.IsNull( remote.uncPath );
			Assert.IsFalse( string.IsNullOrEmpty( remote.uncError ), "the reason is given once, on the node" );

			var file = remote.files.Single();
			Assert.AreEqual( Path.Combine( bed.TempRoot, "m2files", "r.log" ), file.localPath );
			Assert.IsNull( file.uncPath );
		}

		[TestMethod()]
		public async Task AMachineNobodyServesFailsItsOwnNodeOnly()
		{
			using var bed = await StartBed();

			var res = await Ask( bed, "m1.a" );

			var remote = res.fileDefs.Single( n => n.id == "offline" );
			StringAssert.Contains( remote.error, "ghost", "the unreachable machine's node says why" );
			Assert.AreEqual( 0, remote.files.Count );

			var logs = res.fileDefs.Single( n => n.id == "logs" );
			Assert.IsNull( logs.error );
			Assert.AreEqual( 2, logs.files.Count, "the other node is answered regardless" );
		}

		[TestMethod()]
		public async Task AnUnknownAppOrPlanIsAnError()
		{
			using var bed = await StartBed();

			foreach( var id in new[] { "m1.nosuch", "m1.a@nosuchplan", "m1.nosuch@planX" } )
			{
				var lines = await bed.SendCliCommandAsync( $"GetAppFiles {id}", Timeout );
				Assert.AreEqual( 1, lines.Count, string.Join( " | ", lines ) );
				StringAssert.StartsWith( lines[0], "ERROR", id );
			}
		}

		[TestMethod()]
		public async Task APackageSpanningTwoMachinesListsFilesOfBothWithTheirOwnMachine()
		{
			using var bed = await StartBed();

			var lines = await bed.SendCliCommandAsync( "GetFiles pkg", Timeout );
			Assert.AreEqual( 2, lines.Count, string.Join( " | ", lines ) );
			Assert.AreEqual( "END", lines[1] );

			var res = ParseNode( lines[0], "pkg" );
			Assert.AreEqual( 1, res.fileDefs.Count, "one entry for the whole package" );
			Assert.AreEqual( "FilePackage", res.fileDefs[0].type, "the definition itself, not a reference to it" );
			Assert.AreEqual( "Pkg", res.fileDefs[0].title );
			Assert.AreEqual( "First package", res.fileDefs[0].description );
			Assert.IsNull( res.fileDefs[0].error, res.fileDefs[0].error );

			var files = res.fileDefs[0].files.OrderBy( f => f.machine ).ToList();
			Assert.AreEqual( 2, files.Count );

			Assert.AreEqual( "m1", files[0].machine );
			Assert.AreEqual( Path.Combine( bed.TempRoot, "pk1", "pk1.log" ), files[0].localPath );
			Assert.AreEqual( @"\\127.0.0.1\C\" + files[0].localPath!.Substring( SystemDriveRoot.Length ), files[0].uncPath );

			Assert.AreEqual( "m2", files[1].machine );
			Assert.AreEqual( Path.Combine( bed.TempRoot, "pk2", "pk2.log" ), files[1].localPath );
			Assert.AreEqual( @"\\127.0.0.1\pk2\pk2.log", files[1].uncPath, "through m2's own share" );
		}

		[TestMethod()]
		public async Task APackageMemberOnAnUnreachableMachineBecomesANoteAndTheRestIsListed()
		{
			using var bed = await StartBed();

			var lines = await bed.SendCliCommandAsync( "GetFiles pkg", Timeout );
			var res = ParseNode( lines[0], "pkg" );

			var node = res.fileDefs.Single();
			Assert.IsNull( node.error, node.error );
			Assert.AreEqual( 2, node.files.Count, "the members that could be resolved are listed" );
			Assert.IsTrue( node.notes.Any( n => n.Contains( "'M3'" ) ),
				$"a note names the failing member; notes: {string.Join( " | ", node.notes )}" );
		}

		[TestMethod()]
		public async Task AWildcardMatchingTwoDefinitionsGivesTwoEntries()
		{
			using var bed = await StartBed();

			var lines = await bed.SendCliCommandAsync( "GetFiles pkg*", Timeout );
			var res = ParseNode( lines[0], "pkg*" );

			CollectionAssert.AreEqual( new[] { "pkg", "pkg2" }, res.fileDefs.Select( d => d.id ).ToList() );
			CollectionAssert.AreEqual( new[] { "Pkg", "Pkg two" }, res.fileDefs.Select( d => d.title ).ToList() );
			Assert.IsTrue( res.fileDefs.All( d => d.type == "FilePackage" ) );
			Assert.AreEqual( 1, res.fileDefs[1].files.Count, "each entry has the files of its own definition" );
		}

		[TestMethod()]
		public async Task PreviewedFilesCarrySizeAndModifiedTime()
		{
			using var bed = await StartBed();

			var res = await Ask( bed, "m1.a" );
			var logs = res.fileDefs.Single( n => n.id == "logs" );

			var top = logs.files.Single( f => f.path == "Logs/top.log" );
			var onDisk = new FileInfo( top.localPath! );
			Assert.AreEqual( onDisk.Length, top.size );
			Assert.AreEqual( onDisk.LastWriteTimeUtc, top.modified!.Value.ToUniversalTime() );
			Assert.IsNull( top.truncatedTo, "no TailBytes, nothing is cut" );
		}

		[TestMethod()]
		public async Task ABiggerFileThanTailBytesSaysWhatADownloadWouldHold()
		{
			using var bed = await StartBed();

			var lines = await bed.SendCliCommandAsync( "GetFiles tailed", Timeout );
			var entry = ParseNode( lines[0], "tailed" ).fileDefs.Single();

			Assert.AreEqual( "File", entry.type );
			var file = entry.files.Single();
			Assert.AreEqual( 30L, file.size );
			Assert.AreEqual( 10L, file.truncatedTo );
			Assert.IsNotNull( file.modified );
		}

		[TestMethod()]
		public async Task AnUnknownNodeIdIsAnError()
		{
			using var bed = await StartBed();

			var lines = await bed.SendCliCommandAsync( "GetFiles nosuchnode", Timeout );
			Assert.AreEqual( 1, lines.Count, string.Join( " | ", lines ) );
			StringAssert.StartsWith( lines[0], "ERROR" );
		}
	}
}
