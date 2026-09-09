using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.Scripts.BuiltIn;
using System.IO.Compression;

namespace Dirigent.SystemTests
{
	/// <summary>
	/// The file subsystem end to end across processes: what the declared nodes resolve to on another
	/// machine, and what a collection of them produces.
	/// </summary>
	/// <remarks>
	/// Tier 1 covers the behaviour in depth. What only this tier shows is the transfer really
	/// happening between separate processes - each machine's agent compressing its own files and the
	/// requestor merging the parts - rather than one process reading its own disk.
	/// </remarks>
	[TestClass()]
	public class FileSubsystemTests
	{
		[TestMethod()]
		public void TheDeclaredFileNodesCanBeListed()
		{
			using var world = SystemWorld.Start( "LoggingWorld" );

			var result = world.RunScript<ListVfsNodes.TResult>( "BuiltIns/ListVfsNodes.cs",
					"{\"Filter\":{\"Id\":\"log\"}}" );

			Assert.IsNotNull( result );

			var found = result!.Nodes
					.Select( n => $"{n.MachineId}.{n.AppId}" )
					.OrderBy( x => x )
					.ToList();

			Assert.AreEqual( "m1.camera, m1.tracker, m2.recorder", string.Join( ", ", found ),
				"every application exposes its logs" );
		}

		[TestMethod()]
		public void AFileNodeOnAnotherMachineResolvesToFilesThatExist()
		{
			using var world = SystemWorld.Start( "LoggingWorld" );

			world.Cli( "StartApp m2.recorder" );
			world.WaitAppState( "m2.recorder", "R" );

			var recorder = world.Files.Apps.Single( a => a.IdTuple == "m2.recorder" );

			world.WaitUntil( "the recorder has written its log", SystemWorld.AppTimeout,
					() => File.Exists( Path.Combine( recorder.LogsDir, "app.log" ) ) );

			var result = world.RunScript<ResolveVfsPath.TResult>( "BuiltIns/ResolveVfsPath.cs",
					"{\"Node\":{\"Id\":\"log\",\"MachineId\":\"m2\",\"AppId\":\"recorder\"},\"IncludeContent\":true}" );

			Assert.IsNotNull( result?.VfsNode );

			var paths = new List<string>();
			Collect( result!.VfsNode!, paths );

			Assert.IsTrue( paths.Any( p => p.EndsWith( "app.log", StringComparison.OrdinalIgnoreCase ) ),
				$"the live log is among the resolved files: {string.Join( ", ", paths )}" );

			Assert.IsFalse( paths.Any( p => p.EndsWith( "ancient.log", StringComparison.OrdinalIgnoreCase ) ),
				$"the nine-day-old file was filtered out: {string.Join( ", ", paths )}" );

			var missing = paths.Where( p => !File.Exists( p ) ).ToList();
			Assert.AreEqual( 0, missing.Count,
				$"every resolved path exists; these did not: {string.Join( ", ", missing )}" );
		}

		static void Collect( VfsNodeDef node, List<string> paths )
		{
			if( !string.IsNullOrEmpty( node.Path ) && !node.IsContainer ) paths.Add( node.Path! );
			foreach( var child in node.Children ) Collect( child, paths );
		}

		[TestMethod()]
		public void LogsFromBothMachinesAreCollectedIntoOneArchive()
		{
			using var world = SystemWorld.Start( "LoggingWorld" );

			var apps = new[] { "m1.camera", "m1.tracker", "m2.recorder" };

			foreach( var app in apps ) world.Cli( $"StartApp {app}" );
			foreach( var app in apps ) world.WaitAppState( app, "R" );

			world.WaitUntil( "every application has written its log", SystemWorld.AppTimeout,
					() => world.Files.Apps.All( a => File.Exists( Path.Combine( a.LogsDir, "app.log" ) ) ) );

			var result = world.RunScript<DownloadZipped.TResult>( "BuiltIns/DownloadZipped.cs",
					"{\"Node\":{\"Id\":\"logs.all\"}}" );

			Assert.IsNotNull( result );

			Assert.AreEqual( 0, result!.Errors.Count,
				$"the download reported no errors: {string.Join( " | ", result.Errors )}" );

			Assert.AreEqual( "m1, m2", string.Join( ", ", result.Machines.OrderBy( x => x ) ),
				"both machines contributed" );

			var archive = result.Files.Single();
			Assert.IsTrue( File.Exists( archive ), $"the archive exists: {archive}" );

			// look inside: a folder per machine, a folder per application, and nothing stale
			List<string> entries;
			using( var zip = ZipFile.OpenRead( archive ) )
				entries = zip.Entries.Select( e => e.FullName ).ToList();

			Assert.IsTrue( entries.Any( e => e.StartsWith( "m1/" ) && e.EndsWith( "camera/app.log" ) ),
				$"the camera's log is in there: {string.Join( ", ", entries )}" );

			Assert.IsTrue( entries.Any( e => e.StartsWith( "m2/" ) && e.EndsWith( "recorder/app.log" ) ),
				$"the recorder's log is in there: {string.Join( ", ", entries )}" );

			Assert.IsFalse( entries.Any( e => e.EndsWith( "ancient.log" ) ),
				$"the nine-day-old files were left behind: {string.Join( ", ", entries )}" );

			// the staging folder the machines uploaded their parts to is gone again
			var leftovers = Directory.EnumerateDirectories( world.Files.DownloadFolder )
					.Select( Path.GetFileName ).ToList();

			Assert.AreEqual( 0, leftovers.Count,
				$"no folder is left in the download folder: {string.Join( ", ", leftovers )}" );
		}
	}
}
