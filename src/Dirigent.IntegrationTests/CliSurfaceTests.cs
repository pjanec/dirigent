using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.Scripts.BuiltIn;
using Dirigent.TestBed;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// The text-command surface, over a real socket to the master. This is the road a PowerShell
	/// driver takes, and the reason the file subsystem needs no commands of its own: StartScript
	/// with JSON arguments plus GetScriptState is enough to list, resolve and download.
	/// </summary>
	[TestClass()]
	public class CliSurfaceTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 30 );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		static Task<TestBed.TestBed> StartBed()
			=> TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = Worlds.LoggingWorld() } );

		[TestMethod()]
		public async Task CommandsAreAnsweredOverTheSocket()
		{
			// the plumbing first: if this fails, nothing below means anything
			using var bed = await StartBed();
			using var cli = new CliSession( bed.CliPort );

			var apps = cli.RequestList( "GetAllAppsState" );
			Assert.IsTrue( apps.Any( a => a.Contains( "m1.camera" ) ),
				$"the applications should be listed, got: {string.Join( " | ", apps )}" );

			var clients = cli.RequestList( "GetAllClientsState" );
			Assert.IsTrue( clients.Count >= 2, $"both agents should show up, got: {string.Join( " | ", clients )}" );
		}

		[TestMethod()]
		public async Task NodesAreListedThroughAScriptWithJsonArguments()
		{
			using var bed = await StartBed();
			using var cli = new CliSession( bed.CliPort );

			var result = await RunScript<ListVfsNodes.TResult>( bed, cli,
				ListVfsNodes._Name, @"{""Filter"":{""Id"":""log""}}" );

			CollectionAssert.AreEquivalent(
				new[] { "m1.camera", "m1.tracker", "m2.recorder" },
				result.Nodes.Select( n => $"{n.MachineId}.{n.AppId}" ).ToList() );
		}

		[TestMethod()]
		public async Task JsonArgumentsMayContainArrays()
		{
			// the request id used to be parsed greedily up to the last "]" in the line, which
			// silently ate the tail of any argument carrying a JSON array
			using var bed = await StartBed();
			using var cli = new CliSession( bed.CliPort );

			var result = await RunScript<MergeZipped.TResult>( bed, cli,
				MergeZipped._Name,
				Tools.Serialize( new MergeZipped.TArgs()
				{
					StagingFolder = Path.Combine( bed.TempRoot, "nothing-here" ),
					DestinationFile = Path.Combine( bed.DownloadFolder, "empty.zip" ),
					Parts = new System.Collections.Generic.List<MergeZipped.TPart>(),
				} ) );

			// an empty merge is a legitimate no-op; what matters is that the arguments arrived whole
			Assert.IsNotNull( result );
			Assert.AreEqual( 0, result.FileCount );
		}

		[TestMethod()]
		public async Task NodeIsResolvedThroughAScript()
		{
			using var bed = await StartBed();
			using var cli = new CliSession( bed.CliPort );

			await Worlds.StartLoggingApps( bed, Timeout );

			var result = await RunScript<ResolveVfsPath.TResult>( bed, cli,
				ResolveVfsPath._Name,
				@"{""Node"":{""Id"":""log"",""MachineId"":""m2"",""AppId"":""recorder""},""IncludeContent"":true}" );

			Assert.IsNotNull( result.VfsNode, "the recorder's logs should resolve" );

			var paths = Flatten( result.VfsNode! ).ToList();
			Assert.IsTrue( paths.Any( p => Path.GetFileName( p ) == "app.log" ),
				$"got: {string.Join( ", ", paths.Select( Path.GetFileName ) )}" );
			Assert.IsTrue( paths.All( File.Exists ), "every resolved path should exist" );
		}

		[TestMethod()]
		public async Task LogsAreDownloadedThroughAScript()
		{
			// the whole point: a bundle of logs collected from two machines, driven entirely from
			// the command line, with the archive path coming back in the script's result
			using var bed = await StartBed();
			using var cli = new CliSession( bed.CliPort );

			await Worlds.StartLoggingApps( bed, Timeout );

			var result = await RunScript<DownloadZipped.TResult>( bed, cli,
				DownloadZipped._Name, @"{""Node"":{""Id"":""logs.all""}}" );

			Assert.AreEqual( 0, result.Errors.Count, $"errors: {string.Join( " | ", result.Errors )}" );
			CollectionAssert.AreEquivalent( new[] { "m1", "m2" }, result.Machines );

			var archive = result.Files.Single();
			Assert.IsTrue( File.Exists( archive ), $"the archive should be there: {archive}" );

			var entries = Archive.EntriesOf( archive );
			Assert.IsTrue( Archive.HasEntryMatching( entries, "m1/", "camera/", "app.log" ),
				$"entries: {string.Join( ", ", entries )}" );
			Assert.IsTrue( Archive.HasEntryMatching( entries, "m2/", "recorder/", "app.log" ),
				$"entries: {string.Join( ", ", entries )}" );
			Assert.IsFalse( entries.Any( e => e.EndsWith( "ancient.log", StringComparison.OrdinalIgnoreCase ) ),
				$"entries: {string.Join( ", ", entries )}" );
		}

		[TestMethod()]
		public async Task GarbageArgumentsFailTheScriptRatherThanBeingIgnored()
		{
			using var bed = await StartBed();
			using var cli = new CliSession( bed.CliPort );

			var guid = Guid.NewGuid();
			Assert.AreEqual( "ACK", cli.Request( $"StartScript {guid} {ListVfsNodes._Name} 'not json at all'" ) );

			var state = await WaitForScript( bed, cli, guid );
			Assert.AreEqual( EScriptStatus.Failed, state.Status,
				"arguments that are not the script's DTO must be a failure, not a silent default" );
		}

		// ---- driving a script the way the CLI does --------------------------------------

		/// <summary>
		/// StartScript with JSON arguments, then poll GetScriptState until it is over - exactly what
		/// a PowerShell driver will do - and deserialize the script's return value.
		/// </summary>
		static async Task<TResult> RunScript<TResult>(
				TestBed.TestBed bed, CliSession cli, string scriptName, string jsonArgs )
		{
			var guid = Guid.NewGuid();

			// single quotes keep the JSON's double quotes intact through the command tokenizer
			var ack = cli.Request( $"StartScript {guid} {scriptName} '{jsonArgs}'" );
			Assert.AreEqual( "ACK", ack, $"starting {scriptName}" );

			var state = await WaitForScript( bed, cli, guid );

			Assert.AreEqual( EScriptStatus.Finished, state.Status,
				$"{scriptName} should have finished: {state.Text} {state.Data}" );

			var result = Tools.Deserialize<TResult>( state.Data );
			Assert.IsNotNull( result, $"{scriptName} returned nothing" );
			return result!;
		}

		static async Task<ScriptState> WaitForScript( TestBed.TestBed bed, CliSession cli, Guid guid )
		{
			ScriptState? state = null;

			await bed.WaitUntilAsync(
				() =>
				{
					state = ReadScriptState( cli, guid );
					return Task.FromResult( state is not null && !state.IsAlive );
				},
				Timeout, $"script {guid} stops running" );

			return state!;
		}

		/// <summary>
		/// The answer is "SCRIPT:&lt;guid&gt;:&lt;json&gt;", or empty while the script is not known yet.
		/// </summary>
		static ScriptState? ReadScriptState( CliSession cli, Guid guid )
		{
			var line = cli.Request( $"GetScriptState {guid}" );
			if( string.IsNullOrEmpty( line ) ) return null;

			var match = Regex.Match( line, @"^SCRIPT:([0-9a-fA-F\-]{36}):(.*)$" );
			Assert.IsTrue( match.Success, $"unexpected answer to GetScriptState: '{line}'" );

			return Tools.Deserialize<ScriptState>( match.Groups[2].Value );
		}

		static System.Collections.Generic.IEnumerable<string> Flatten( VfsNodeDef node )
		{
			if( !node.IsContainer && !string.IsNullOrEmpty( node.Path ) )
				yield return node.Path!;

			foreach( var child in node.Children )
				foreach( var path in Flatten( child ) )
					yield return path;
		}
	}
}
