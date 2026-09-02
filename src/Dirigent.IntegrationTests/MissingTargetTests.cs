using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// What happens to a package when one of the things it names is not on the machine.
	/// </summary>
	/// <remarks>
	/// Reported from production: a `&lt;File Filter="Newest"&gt;` node pointing at a CrashDumps folder
	/// that had never been created - because that machine had never crashed - failed the resolution,
	/// and with it the entire multi-machine collection. Nothing at all was collected, from any machine.
	///
	/// The failure mode was inverted: an incident report is collected right after something has gone
	/// wrong, which is exactly when a dump folder may be missing and exactly when losing everything
	/// else costs the most. It was also invisible to review - the node is valid, the path is right,
	/// and whether the download works depends on whether that machine has ever crashed.
	///
	/// The rule these pin: **a member of a container that cannot be looked up is left out, the
	/// container records why, and everything else is collected.** Asked for on its own, the same node
	/// still fails out loud - there the caller wanted that one thing and there is nothing else to give.
	/// </remarks>
	[TestClass()]
	public class MissingTargetTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 30 );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		/// <summary>A path under the run's temp root that nothing ever creates.</summary>
		static string NeverCreated( TestBed.TestBed bed ) => Path.Combine( bed.TempRoot, "no_such_folder" );

		/// <summary>
		/// An application with a real log, plus one node of each kind pointing at a folder that does
		/// not exist - the three shapes that used to disagree about the same situation.
		/// </summary>
		static Scenario World()
			=> Scenario.OneMachine()
				.App( "m1.app", a => a
					.LongRunning()
					.WithLogNode()
					// a folder that will never be there, as a <Folder> and as Filter="Newest"
					.WithFolderNode( "dump.folder", "{temp}\\no_such_folder", mask: "*.dmp" )
					.RawXml( "<File Id='dump.newest' Title='Crash dumps' Path='{temp}\\no_such_folder'"
							+ " Mask='*.dmp' Filter='Newest' MaxFiles='4'/>" )
					// and one plain file that is not there either
					.RawXml( "<File Id='dump.one' Title='One dump' Path='{temp}\\no_such_folder\\one.dmp'/>" ) )
				.Package( "pkg", "Logs/Everything", p => p
					.RefAll( "log" ).RefAll( "dump.folder" ).RefAll( "dump.newest" ).RefAll( "dump.one" ) );

		static Task<TestBed.TestBed> StartBed()
			=> TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World() } );

		static async Task SeedTheLog( TestBed.TestBed bed )
		{
			var log = Worlds.LogOf( bed, "m1", "app" );
			Directory.CreateDirectory( Path.GetDirectoryName( log )! );
			await File.WriteAllTextAsync( log, "the log that must survive the missing folders" + Environment.NewLine );
		}

		[TestMethod()]
		public async Task AMissingFolderDoesNotCostThePackageEverythingElse()
		{
			using var bed = await StartBed();
			await SeedTheLog( bed );

			Assert.IsFalse( Directory.Exists( NeverCreated( bed ) ), "the folder really is not there" );

			var package = await bed.Operator.GetVfsNodeAsync( "pkg" );
			var result = await bed.Operator.DownloadAsync( package, timeout: Timeout );

			Assert.AreEqual( 1, result.Files.Count, $"an archive was produced: {string.Join( ", ", result.Errors )}" );

			var archive = Archive.In( bed.DownloadFolder ).Single();
			var entries = Archive.EntriesOf( archive );

			Assert.IsTrue( entries.Any( e => e.EndsWith( "app.log", StringComparison.OrdinalIgnoreCase ) ),
				$"the log is in there, which is the whole point: {string.Join( ", ", entries )}" );
		}

		[TestMethod()]
		public async Task TheArchiveSaysWhatItCouldNotLookUp()
		{
			// an archive that quietly lacks the crash dumps is a trap for whoever opens it later
			using var bed = await StartBed();
			await SeedTheLog( bed );

			var package = await bed.Operator.GetVfsNodeAsync( "pkg" );
			await bed.Operator.DownloadAsync( package, timeout: Timeout );

			var report = Archive.TextOf( Archive.In( bed.DownloadFolder ).Single(), "_incomplete.txt" );

			StringAssert.Contains( report, "Crash dumps",
				$"the node is named as the config names it:\n{report}" );
			StringAssert.Contains( report, "on " + bed.MachineIds["m1"],
				$"and so is the machine it belonged to:\n{report}" );
			StringAssert.Contains( report, "no_such_folder",
				$"together with what was actually wrong:\n{report}" );

			// the plain <File> naming a file that is not there is reported the same way, rather than
			// as an error of the collection - one rule for "you asked for it and it is not here"
			StringAssert.Contains( report, "one.dmp",
				$"and so is the missing file:\n{report}" );
		}

		[TestMethod()]
		public async Task TheOperatorIsToldToo()
		{
			// the archive tells whoever opens it later; this tells whoever is standing there now
			using var bed = await StartBed();
			await SeedTheLog( bed );

			var package = await bed.Operator.GetVfsNodeAsync( "pkg" );
			var result = await bed.Operator.DownloadAsync( package, timeout: Timeout );

			Assert.IsTrue( result.NotCollected.Count >= 2,
				$"both missing folder nodes are reported: {string.Join( " | ", result.NotCollected )}" );

			Assert.AreEqual( 0, result.Errors.Count,
				$"and not as errors - the download did what it could: {string.Join( " | ", result.Errors )}" );

			var messages = bed.Operator.Notifications.Select( n => n.Message ?? "" ).ToList();

			var closing = messages.FirstOrDefault( m => m.Contains( "had nothing to collect" ) );
			Assert.IsNotNull( closing,
				$"the closing message says so: {string.Join( " | ", messages )}" );

			// as a count, with the detail in the archive - an incident package over a large system
			// names hundreds of things that are not there, and a dialog listing them is not read
			StringAssert.Contains( closing!, $"{result.NotCollected.Count} item(s)", closing );
			StringAssert.Contains( closing!, "_incomplete.txt", closing );

			foreach( var note in result.NotCollected )
				Assert.IsFalse( closing!.Contains( note ), $"the notes themselves are in the archive:\n{closing}" );
		}

		[TestMethod()]
		public async Task EveryKindOfNodeIsReportedTheSameWay()
		{
			// The three shapes used to disagree about the same situation - a plain <File> reported it
			// as an error of the collection, a <Folder> vanished without a word, and Filter="Newest"
			// threw and took the package with it. One rule now covers all three: what was named and
			// not delivered is recorded, and the rest of the package is collected.
			using var bed = await StartBed();
			await SeedTheLog( bed );

			var package = await bed.Operator.GetVfsNodeAsync( "pkg" );
			var result = await bed.Operator.DownloadAsync( package, timeout: Timeout );

			var report = Archive.TextOf( Archive.In( bed.DownloadFolder ).Single(), "_incomplete.txt" );

			foreach( var (id, what) in new[] {
				( "dump.folder", "a <Folder>" ),
				( "dump.newest", "a <File Filter=\"Newest\">" ),
				( "dump.one", "a plain <File>" ),
			} )
			{
				Assert.IsTrue( result.NotCollected.Any( n => n.Contains( "no_such_folder" ) ),
					$"{what} pointing at something absent belongs in NotCollected: "
					+ $"{string.Join( " | ", result.NotCollected )}" );
			}

			Assert.AreEqual( 3, result.NotCollected.Count,
				$"one entry for each: {string.Join( " | ", result.NotCollected )}" );

			Assert.AreEqual( 0, result.Errors.Count,
				$"and none of them an error: {string.Join( " | ", result.Errors )}" );

			StringAssert.Contains( report, "no_such_folder", $"the archive says so too:\n{report}" );
			StringAssert.Contains( report, "one.dmp", $"about the file as well:\n{report}" );
		}

		[TestMethod()]
		public async Task AskedForOnItsOwnAMissingFolderStillFails()
		{
			// The guard is about not losing the rest of a package. Ask for one folder and nothing
			// else, and there is nothing else to deliver - so it says what is wrong rather than
			// handing back an empty archive.
			using var bed = await StartBed();
			await SeedTheLog( bed );

			foreach( var id in new[] { "dump.folder", "dump.newest" } )
			{
				var node = await bed.Operator.GetVfsNodeAsync( id );

				Exception? failure = null;
				try
				{
					await bed.Operator.DownloadAsync( node, timeout: Timeout );
				}
				catch( Exception e )
				{
					// the type varies: a failure raised on an agent arrives as a DeserializedException
					failure = e;
				}

				Assert.IsNotNull( failure, $"'{id}' names a folder that is not there" );
				StringAssert.Contains( failure!.Message.ToLowerInvariant(), "no_such_folder",
					$"'{id}' should say what was missing: {failure.Message}" );
			}

			// A plain <File> is the one that cannot behave this way, and the reason is worth writing
			// down: resolving it is turning a path into a path, and nothing looks at the disk. Adding
			// a check would also change what a tool action does with a file that is not there yet -
			// "open in Notepad" on a log an application has not written is a reasonable thing to want.
			// So it is caught when the archive is built, and reported in exactly the same words.
			var one = await bed.Operator.GetVfsNodeAsync( "dump.one" );
			var result = await bed.Operator.DownloadAsync( one, timeout: Timeout );

			Assert.AreEqual( 1, result.NotCollected.Count,
				$"reported, not thrown: {string.Join( " | ", result.NotCollected )}" );
			StringAssert.Contains( result.NotCollected[0], "one.dmp" );
		}

		[TestMethod()]
		public async Task AFolderThatAppearsLaterIsCollectedWithoutAnyChange()
		{
			// the other half of the story: the node was right all along, the machine had simply never
			// crashed yet
			using var bed = await StartBed();
			await SeedTheLog( bed );

			var package = await bed.Operator.GetVfsNodeAsync( "pkg" );
			await bed.Operator.DownloadAsync( package, timeout: Timeout );

			Directory.CreateDirectory( NeverCreated( bed ) );
			await File.WriteAllTextAsync( Path.Combine( NeverCreated( bed ), "crash.dmp" ), "the dump" );

			var second = await bed.Operator.DownloadAsync( package, timeout: Timeout );

			Assert.AreEqual( 0, second.NotCollected.Count( n => n.Contains( "Crash dumps" ) ),
				$"nothing is missing any more: {string.Join( " | ", second.NotCollected )}" );

			var archive = Archive.In( bed.DownloadFolder ).OrderBy( a => new FileInfo( a ).LastWriteTimeUtc ).Last();
			Assert.IsTrue( Archive.EntriesOf( archive ).Any( e => e.EndsWith( "crash.dmp", StringComparison.OrdinalIgnoreCase ) ),
				$"and the dump is collected: {string.Join( ", ", Archive.EntriesOf( archive ) )}" );
		}
	}
}
