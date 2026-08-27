using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.Scripts.BuiltIn;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// The VFS scripts driven the way anything without a GUI drives them: by naming a node with its
	/// config id and letting the script resolve it. No new commands are involved - StartScript and
	/// GetScriptState already carry this, which is what these tests are here to keep true.
	/// </summary>
	[TestClass()]
	public class VfsScriptTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 30 );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		static Task<TestBed.TestBed> StartBed()
			=> TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = Worlds.LoggingWorld() } );

		[TestMethod()]
		public async Task NodesCanBeListedWithoutAGui()
		{
			using var bed = await StartBed();

			var all = await bed.Operator.RunScriptAsync<ListVfsNodes.TArgs, ListVfsNodes.TResult>(
				ListVfsNodes._Name, new ListVfsNodes.TArgs(), timeout: Timeout );

			Assert.IsNotNull( all );

			// the package, and one log node per application
			var package = all!.Nodes.Single( n => n.Id == "logs.all" );
			Assert.AreEqual( "FilePackage", package.Type );
			Assert.AreEqual( "Logs/All apps", package.Title );

			var logs = all.Nodes.Where( n => n.Id == "log" ).ToList();
			CollectionAssert.AreEquivalent(
				new[] { "m1.camera", "m1.tracker", "m2.recorder" },
				logs.Select( n => $"{n.MachineId}.{n.AppId}" ).ToList() );

			// the declared path, not a resolved one - nothing was looked up on any disk
			Assert.IsTrue( logs.All( n => n.Path!.Contains( "logs" ) ),
				$"paths: {string.Join( ", ", logs.Select( n => n.Path ) )}" );
		}

		[TestMethod()]
		public async Task ListingCanBeNarrowedToOneMachine()
		{
			using var bed = await StartBed();

			var m1 = await bed.Operator.RunScriptAsync<ListVfsNodes.TArgs, ListVfsNodes.TResult>(
				ListVfsNodes._Name,
				new ListVfsNodes.TArgs() { Filter = new VfsNodeSelector() { Id = "log", MachineId = "m1" } },
				timeout: Timeout );

			CollectionAssert.AreEquivalent(
				new[] { "camera", "tracker" },
				m1!.Nodes.Select( n => n.AppId ).ToList() );
		}

		[TestMethod()]
		public async Task NodeCanBeResolvedByIdFromAnotherMachine()
		{
			// what "is the file really there" looks like without a GUI: the master resolves a node
			// it does not own by dispatching to the machine that does
			using var bed = await StartBed();

			await Worlds.StartLoggingApps( bed, Timeout );

			var resolved = await bed.Operator.RunScriptAsync<ResolveVfsPath.TArgs, ResolveVfsPath.TResult>(
				ResolveVfsPath._Name,
				new ResolveVfsPath.TArgs()
				{
					Node = new VfsNodeSelector() { Id = "log", MachineId = "m2", AppId = "recorder" },
					IncludeContent = true,
				},
				timeout: Timeout );

			Assert.IsNotNull( resolved?.VfsNode, "the recorder's logs should resolve" );

			var files = Files( resolved!.VfsNode! ).ToList();
			Assert.IsTrue( files.Any( f => Path.GetFileName( f ) == "app.log" ),
				$"the live log should be in there, got: {string.Join( ", ", files.Select( Path.GetFileName ) )}" );
			Assert.IsFalse( files.Any( f => Path.GetFileName( f ) == "ancient.log" ),
				$"the nine-day-old file should be filtered out, got: {string.Join( ", ", files.Select( Path.GetFileName ) )}" );

			// resolved paths are real and local to the machine that owns them
			Assert.IsTrue( files.All( File.Exists ), "every resolved path should exist" );
		}

		[TestMethod()]
		public async Task DownloadCanBeAskedForByIdAlone()
		{
			using var bed = await StartBed();

			await Worlds.StartLoggingApps( bed, Timeout );

			var result = await bed.Operator.DownloadAsync(
				new VfsNodeSelector() { Id = "logs.all" }, timeout: Timeout );

			Assert.AreEqual( 0, result.Errors.Count,
				$"errors: {string.Join( " | ", result.Errors )}" );

			CollectionAssert.AreEquivalent( new[] { "m1", "m2" }, result.Machines,
				"both machines should have contributed" );
			Assert.AreEqual( "m1", result.DownloadMachine, "the operator sits on m1" );

			// the result names the archive, which is what a caller with no message box needs
			var archive = result.Files.Single();
			Assert.IsTrue( File.Exists( archive ), $"the archive should be there: {archive}" );
			Assert.AreEqual(
				Path.GetFullPath( bed.DownloadFolder ),
				Path.GetFullPath( Path.GetDirectoryName( archive )! ),
				"it belongs in the download folder of the operator's machine" );
		}

		[TestMethod()]
		public async Task DownloadByIdCanBeNarrowedToOneApplication()
		{
			using var bed = await StartBed();

			await Worlds.StartLoggingApps( bed, Timeout );

			var result = await bed.Operator.DownloadAsync(
				new VfsNodeSelector() { Id = "log", MachineId = "m1", AppId = "camera" }, timeout: Timeout );

			Assert.AreEqual( 0, result.Errors.Count, $"errors: {string.Join( " | ", result.Errors )}" );
			CollectionAssert.AreEquivalent( new[] { "m1" }, result.Machines,
				"only the camera's machine should have been asked" );

			var entries = Archive.EntriesOf( result.Files.Single() );
			Assert.IsTrue( entries.Any( e => e.EndsWith( "app.log", StringComparison.OrdinalIgnoreCase ) ),
				$"entries: {string.Join( ", ", entries )}" );
			Assert.IsFalse( entries.Any( e => e.Contains( "tracker", StringComparison.OrdinalIgnoreCase ) ),
				$"entries: {string.Join( ", ", entries )}" );
		}

		[TestMethod()]
		public async Task NamingANodeThatDoesNotExistIsReportedNotSwallowed()
		{
			using var bed = await StartBed();

			var result = await bed.Operator.DownloadAsync(
				new VfsNodeSelector() { Id = "no.such.node" }, timeout: Timeout );

			Assert.AreEqual( 0, result.Files.Count, "nothing should have been produced" );
			Assert.IsTrue( result.Errors.Any( e => e.Contains( "no.such.node" ) ),
				$"the result should say what was not found, got: {string.Join( " | ", result.Errors )}" );
		}

		static System.Collections.Generic.IEnumerable<string> Files( VfsNodeDef node )
		{
			if( !node.IsContainer && !string.IsNullOrEmpty( node.Path ) )
				yield return node.Path!;

			foreach( var child in node.Children )
				foreach( var file in Files( child ) )
					yield return file;
		}
	}
}
