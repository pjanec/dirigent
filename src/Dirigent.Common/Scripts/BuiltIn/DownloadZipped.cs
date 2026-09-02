using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;
using Dirigent;

namespace Dirigent.Scripts.BuiltIn
{

	/*
	* Takes a bunch of vfsNodes. Starts a slave script on each machine where the files are local.
	* Let then upload machine-specific zip files to our folder we create in Downloads folder.
	* Wait for the slave scripts to finish and show a "download finished" bubble.
	*/
	public class DownloadZipped : Script
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public static readonly string _Name = "BuiltIns/DownloadZipped.cs";

		//[MessagePack.MessagePackObject]
		public class TArgs : ScriptActionArgs
		{
			/// <summary>
			/// What to download, named by its config id. Used when VfsNode is not given, which is the
			/// case for every caller but a GUI.
			/// </summary>
			public VfsNodeSelector? Node;


			/// <summary>
			/// The machine to download to. Empty means the machine the requestor runs on, which is what
			/// a GUI wants; a CLI or REST caller has no machine of its own and may name one here.
			/// </summary>
			public string? ToMachine;


		};

		//[MessagePack.MessagePackObject]
		public class TResult
		{
			/// <summary>Full paths of the archives produced, on the machine they were downloaded to.</summary>
			public List<string> Files = new();

			/// <summary>The machine the files were downloaded to.</summary>
			public string DownloadMachine = "";

			/// <summary>The machines that took part.</summary>
			public List<string> Machines = new();

			/// <summary>
			/// What went wrong, one entry per machine that had a problem. Empty on a clean download.
			/// A download does not fail as a whole because one machine or one file did.
			/// </summary>
			public List<string> Errors = new();

			/// <summary>
			/// Things the package asked for that are not in the archive - a folder that is not on the
			/// machine, most often. Not errors: the collection did what it could, and this is what it
			/// could not.
			/// </summary>
			public List<string> NotCollected = new();
		}

		class SlaveTask
		{
			public string MachineName="";
			public Guid scriptId;
			public Task<DownloadZippedSlave.TResult>? Task;
			public DownloadZippedSlave.TResult? Result;

			/// <summary>
			/// Why this machine produced no archive at all, or null if it produced one.
			/// </summary>
			/// <remarks>
			/// The one outcome that leaves nothing behind to explain itself. A machine that collected
			/// nothing still writes its part - holding its `_incomplete.txt`, which says what it was
			/// asked for and why none of it is here - so an empty collection is self-describing. A
			/// machine whose part never arrived is not: neither its files nor its account of them
			/// exist, and only this says so.
			/// </remarks>
			public Exception? Failure;

			/// <summary>
			/// The last progress this machine announced. Kept because a machine stops announcing when
			/// it finishes - its state then carries the result instead - and forgetting what it had
			/// announced would drop its bytes out of the total.
			/// </summary>
			public DownloadZippedSlave.TProgress? LastProgress;
		}


		/// <summary>How much of the bar each phase gets. The slaves do the work, so they get most of it.</summary>
		const double _resolvedAt = 0.05;
		const double _collectedAt = 0.85;

		/// <summary>How often the slaves are asked how far they have got.</summary>
		const int _pollPeriodMs = 500;

		/// <summary>How long a cancellation waits for the machines to stop before cleaning up after them.</summary>
		const int _slaveStopTimeoutMs = 10000;

		// kept outside the try so that a cancellation can still clean up after them
		readonly List<SlaveTask> _slaveTasks = new();
		string _requestorMachine = string.Empty;
		string? _stagingFolder;
		string? _finalArchive;

		protected async override Task<string?> Run()
		{
			var args = Tools.Deserialize<TArgs>( Args );
			if( args is null ) throw new NullReferenceException("Args is null");

			var result = new TResult();

			try
			{
				// No number for this phase, on purpose. Looking a package up is one remote call per
				// node, in sequence, and on a system of two machines and thirty nodes that is the
				// longest part of the whole operation - while nothing about it is measurable in
				// advance. A bar frozen at 0% for half a minute reads as a hung operation; an
				// indicator that says "working, no idea how long" reads as what it is.
				await SetStatus( "Looking up the files...", null, null );

				// Three ways in, all landing on a resolved tree before the work starts:
				//  - a caller names the node by id (CLI, REST, another script)
				//  - a caller hands over the DEFINITION and asks us to resolve it, which is what a
				//    GUI does: resolving spans one remote round trip per node and belongs inside
				//    this operation, counted in its progress, rather than in front of it where
				//    nothing is watching
				//  - a caller hands over an already resolved tree (tool actions still need to
				//    resolve first for their FILE_PATH, so they arrive this way)
				var vfsNode = args.VfsNode;
				if( vfsNode is null )
				{
					if( args.Node is null )
						throw new ArgumentException( "Neither VfsNode nor Node given - nothing to download." );

					vfsNode = await Dirig.ResolveAsync( args.Node.ToFileRef(), false, true );
					if( vfsNode is null )
						throw new Exception( $"No VFS node matching {args.Node}." );
				}
				else if( args.VfsNodeNeedsResolving )
				{
					vfsNode = await Dirig.ResolveAsync( vfsNode, false, true );
					if( vfsNode is null )
						throw new Exception( $"Nothing found for {args.VfsNode.Id ?? args.VfsNode.Title}." );
				}

				// if a single file, create artificial container containing this single file
				var title = vfsNode.Title;
				var titleSource = vfsNode;
				VfsNodeDef container;
				if( vfsNode.IsContainer )
				{
					container = vfsNode;
					titleSource = vfsNode;
				}
				else
				{
					container = new VFolderDef() { Title = title, Children = new List<VfsNodeDef>() { vfsNode } };
				}
				if (string.IsNullOrEmpty( title )) title = Path.GetFileName(titleSource.Path??"");
				if (string.IsNullOrEmpty( title )) title = titleSource.Id;
				if (string.IsNullOrEmpty( title )) title = "file";
				container.Title = title;

				// What the resolution could not find. Kept before the machines add their own notes, so
				// that this is about the lookup only - a package naming a folder that does not exist
				// on one machine still collects everything else, and this is how anybody hears of it.
				CollectNotes( container, result.NotCollected );

				// collect all individual machines
				var allMachines = new HashSet<string>();
				CollectMachines( container, allMachines );

				// find machines that are online
				var clientStates = (await Dirig.GetAllClientsStateAsync()).ToDictionary( x => x.Key, y => y.Value );
				var onlineMachines = (from x in allMachines where clientStates.ContainsKey(x) && clientStates[x].Connected select x).ToList();

				if( onlineMachines.Count == 0 )
				{
					await Dirig.SendAsync( new Net.UserNotificationMessage
					{
						HostClientId = Requestor,
						Category=Net.UserNotificationMessage.ECategory.Warning,
						PresentationType = Net.UserNotificationMessage.EPresentationType.MessageBox,
						Message = $"Nothing to download - none of the machines holding the files of '{title}' is online.",
					});

					result.Errors.Add( $"None of the machines holding the files of '{title}' is online." );
					return Tools.Serialize( result );
				}

				// Everything ends up in one archive. An older config may still ask for one per
				// machine; say so rather than quietly doing something else.
				if( MentionsPerMachine( args.Args ) )
					log.Warn( "DownloadZipped: 'perMachine' is no longer supported - one archive is produced." );

				await Dirig.SendAsync( new Net.UserNotificationMessage
				{
					HostClientId = Requestor,
					Category=Net.UserNotificationMessage.ECategory.Info,
					PresentationType = Net.UserNotificationMessage.EPresentationType.BalloonTip,
					Message = $"Downloading from {onlineMachines.Count} machine(s)...",
					Timeout = 1.0,
				});

				// The files are downloaded to the download folder of the machine the requestor runs on.
				// %DOWNLOADS% gets expanded during the resolution, which happens on that very machine.
				var requestorMachine = string.IsNullOrEmpty( args.ToMachine )
						? FindRequestorMachine( clientStates )
						: args.ToMachine!;

				if( !IsConnectedAgent( clientStates, requestorMachine ) )
					throw new Exception( $"Cannot download to '{requestorMachine}': no agent of that machine is connected." );

				// local path on the requestor's machine; where the files really end up
				var vfsLocalDownloadFolder = await Dirig.ResolveAsync(
					new FolderDef() { Path = "%DOWNLOADS%", MachineId = requestorMachine }, false, false );
				if (vfsLocalDownloadFolder is null) throw new Exception( $"Could not find the download folder of {requestorMachine}." );
				var downloadsFolder = vfsLocalDownloadFolder.Path!;

				// UNC path to the same folder, for the slaves on other machines to upload to.
				// Not every deployment has a file share covering the download folder, and a slave
				// running on the machine that owns the folder needs none, so this is not fatal here;
				// only a machine that turns out to need it reports the problem.
				string? uncDownloadsFolder = null;
				try
				{
					var vfsResolvedDownloadFolder = await Dirig.ResolveAsync(
						new FolderDef() { Path = "%DOWNLOADS%", MachineId = requestorMachine }, true, false );
					uncDownloadsFolder = vfsResolvedDownloadFolder?.Path;
				}
				catch( Exception e )
				{
					log.Info( $"DownloadZipped: no UNC path to the download folder of {requestorMachine}: {e.Message}" );
				}

				// get the name of the archive file to download
				// the title is free text and ends up as a file name; a colon in it used to fail the
				// download at the write, after everything had already been collected
				string zipFileBase = FreeName( downloadsFolder,
									Tools.SanitizeFileName( System.IO.Path.GetFileName( title ) )
									+ DateTime.Now.ToString("_yyMMdd_HHmm") );

				// Each machine produces its own archive. To get a single one, the machines upload
				// their archives to a staging folder next to the final one and a merging script
				// then joins them locally on the requestor's machine.
				string stagingFolderName = $"{zipFileBase}_parts";

				// the folder each slave uploads to, as a local path and as a UNC path; a slave picks
				// whichever of them it can actually reach
				var localSlaveDestination = System.IO.Path.Combine( downloadsFolder, stagingFolderName );

				var uncSlaveDestination = string.IsNullOrEmpty( uncDownloadsFolder )
						? null
						: System.IO.Path.Combine( uncDownloadsFolder, stagingFolderName );

				// what a cancellation would have to clean up after us
				_requestorMachine = requestorMachine;
				_stagingFolder = localSlaveDestination;
				_finalArchive = System.IO.Path.Combine( downloadsFolder, $"{zipFileBase}.zip" );

				await Publish( $"Collecting from {onlineMachines.Count} machine(s)...", _resolvedAt );

				clientStates.TryGetValue( requestorMachine, out var requestorMachineState );
				var requestorIP = requestorMachineState?.IP;

				// Whether a machine can write to the download folder as a local path. Its own machine
				// obviously can. A machine reachable at the same address shares the disks as well, and
				// where no file share is defined that is the only way its slave can get the files there;
				// with a share available we stay with the share for anything but the owning machine.
				bool OwnsDownloadFolder( string mach )
					=> mach == requestorMachine
					|| ( string.IsNullOrEmpty( uncSlaveDestination )
						&& !string.IsNullOrEmpty( requestorIP )
						&& clientStates.TryGetValue( mach, out var st ) && st.IP == requestorIP );

				// machines that cannot reach the download folder at all
				var unreachable = new Dictionary<string,string>();

				// start a slave script on each machine
				// (the list itself lives on the instance, so that a cancellation can kill what it started)
				bool globalsAssigned = false;
				foreach (var mach in onlineMachines)
				{
					var destinationIsLocal = OwnsDownloadFolder( mach );

					if( !destinationIsLocal && string.IsNullOrEmpty( uncSlaveDestination ) )
					{
						unreachable[mach] = $"No file share of {requestorMachine} covers its download folder, "
										+ $"so {mach} has no way of uploading the files there.";
						continue;
					}

					var slaveScriptName = DownloadZippedSlave._Name;
					var slaveScriptArgs = new DownloadZippedSlave.TArgs()
					{
						Container = container,
						DestinationFolder = uncSlaveDestination,
						LocalDestinationFolder = localSlaveDestination,
						DestinationIsLocal = destinationIsLocal,
						ZipFileBaseName = zipFileBase,
						IncludeGlobals = !globalsAssigned, // the first machine to run does the global files
					};
					globalsAssigned = true;

					var task = Dirig.RunScriptAsync<DownloadZippedSlave.TArgs, DownloadZippedSlave.TResult>(
						mach, slaveScriptName, null, slaveScriptArgs, $"GetZippedFiles on {mach}", out var inst
					);

					var st = new SlaveTask() { MachineName=mach, scriptId = inst, Task = task! };
					_slaveTasks.Add( st );
				}

				// wait for all of them to finish
				await CollectFromSlaves();

				var downloadedFiles = ( from x in _slaveTasks
										where x.Result is not null
										orderby x.Result!.ZipFileName
										select x.Result!.ZipFileName ).ToList();

				foreach( var (mach, message) in unreachable )
					result.Errors.Add( $"{mach}: {message}" );

				foreach( var st in _slaveTasks )
				{
					result.Machines.Add( st.MachineName );

					// what that machine was asked for and does not have - not an error, see
					// DownloadZippedSlave.TResult.NotCollected
					if( st.Result is not null )
						result.NotCollected.AddRange( st.Result.NotCollected );

					// no archive from this machine, so nothing of its own to say why - see
					// SlaveTask.Failure
					if( st.Failure is not null )
						result.Errors.Add( $"{st.MachineName}: {Tools.JustFirstLine( st.Failure.Message )}" );

					if( st.Result is not null )
					{
						foreach( var e in st.Result.Exceptions )
							result.Errors.Add( $"{st.MachineName}: {e.Message}" );
					}
				}

				// join the per-machine archives into a single one, on the machine holding them
				// (nothing to join if not a single slave could be started)
				if( _slaveTasks.Count > 0 )
				{
					var parts = (from x in _slaveTasks
								 where !string.IsNullOrEmpty( x.Result?.ZipFileName )
								 orderby x.MachineName
								 select new MergeZipped.TPart()
								 {
									 FileName = x.Result!.ZipFileName,
									 MachineName = x.MachineName
								 }).ToList();

					var mergeArgs = new MergeZipped.TArgs()
					{
						StagingFolder = Path.Combine( downloadsFolder, stagingFolderName ),
						DestinationFile = Path.Combine( downloadsFolder, $"{zipFileBase}.zip" ),
						Parts = parts,

						// composed here, where the package, the machines and their addresses are known;
						// the merge only writes it
						CoverNote = await ComposeCoverNote( args.Comment, title, container.Id,
										requestorMachine, clientStates, unreachable ),

						// files from a single machine need no folder to tell them apart from the others
						PrefixWithMachine = parts.Count > 1,
					};

					await SetStatus( "Merging the collected files...", null, _collectedAt );

					var mergeTask = Dirig.RunScriptAsync<MergeZipped.TArgs, MergeZipped.TResult>(
						requestorMachine, MergeZipped._Name, null, mergeArgs,
						$"Merging the downloaded files on {requestorMachine}", out var mergeInst
					);

					// followed the same way as the slaves: repacking a large collection is minutes of
					// work, and an indicator that sits at one number through it is what a frozen one
					// looks like. It also keeps this script's own state moving, which is what tells
					// everyone else that it is alive.
					var mergeResult = await FollowMerge( mergeTask, mergeInst );

					downloadedFiles = string.IsNullOrEmpty( mergeResult?.ZipFileName )
											? new List<string>()
											: new List<string>() { mergeResult!.ZipFileName };

					if( mergeResult is not null )
					{
						foreach( var e in mergeResult.Exceptions )
							result.Errors.Add( $"{requestorMachine} (merging): {e.Message}" );
					}
				}

				// what the caller gets back; a message box is no use to a script or a CLI
				result.DownloadMachine = requestorMachine;
				result.Files = ( from x in downloadedFiles
								 select Path.IsPathRooted( x ) ? x : Path.Combine( downloadsFolder, x ) ).ToList();

				await Publish( "Downloaded.", 1.0 );

				// tell the user it's all done
				var clickAction = new ToolActionDef { Name = "WinExplorer", Args = $"/select,\"{Path.Combine( downloadsFolder, downloadedFiles.FirstOrDefault()??"")}\"" };

				await Dirig.SendAsync( new Net.UserNotificationMessage
				{
					HostClientId = Requestor,
					Category=Net.UserNotificationMessage.ECategory.Info,
					PresentationType = Net.UserNotificationMessage.EPresentationType.MessageBox,
					Message = ComposeClosingMessage(
						result.Files,
						filesCollected: _slaveTasks.Sum( x => x.Result?.FilesCollected ?? 0 ),
						machinesDelivered: _slaveTasks.Count( x => x.Result is not null ),
						notCollectedCount: result.NotCollected.Count,
						machinesWithNoArchive: ( from x in _slaveTasks
												 where x.Failure is not null
												 orderby x.MachineName, StringComparer.OrdinalIgnoreCase
												 select x.MachineName ).ToList(),
						errors: result.Errors ),
					Action = clickAction
				});

			}
			catch( OperationCanceledException )
			{
				// the user asked us to stop: take the machines and the half-collected parts with us,
				// and say nothing - they know, they asked for it
				await SetStatus( "Cancelling...", null, null );
				await CancelSlavesAndCleanUp();
				throw;
			}
			catch (Exception e)
			{
				//log.Error( $"DownloadZipped: Exception while waiting for slave scripts to finish: {e.Message}" );

				// tell the user we failed
				await Dirig.SendAsync( new Net.UserNotificationMessage
				{
					HostClientId = Requestor,
					Category=Net.UserNotificationMessage.ECategory.Error, 
					PresentationType = Net.UserNotificationMessage.EPresentationType.BalloonTip,
					Message = $"File download failed!\n\n"+e.Message,
				});

				result.Errors.Add( e.Message );

				// One machine or one missing file must not turn the whole download into a failed
				// script - what the other machines delivered is still worth having. Producing
				// nothing at all is another matter: reporting that as a finished script leaves a
				// progress indicator showing success, and a balloon is easy to miss.
				if( result.Files.Count == 0 )
					throw;
			}

			// the same for a download that ran to the end and produced nothing: the errors say why,
			// and the status has to agree with them
			if( result.Files.Count == 0 && result.Errors.Count > 0 )
				throw new Exception( "Nothing was downloaded. " + string.Join( " ", result.Errors ) );

			return Tools.Serialize( result );
		}

		/// <summary>
		/// Waits for the slaves, publishing how far they have got between the checks.
		/// </summary>
		/// <remarks>
		/// The slaves report their own progress in bytes; this weighs them by the amount each one
		/// announced, so a machine holding a 60 GB log does not count the same as one holding 2 MB.
		/// </remarks>
		async Task CollectFromSlaves()
		{
			var all = Task.WhenAll( from x in _slaveTasks select x.Task );

			while( true )
			{
				// whichever comes first: everything done, or time to report
				var finished = await Task.WhenAny( all, Task.Delay( _pollPeriodMs, CancellationToken ) );
				if( finished == all ) break;

				// WhenAny hands back the cancelled delay rather than throwing, so the cancellation
				// has to be looked at here - otherwise the wait simply carries on to the end
				CancellationToken.ThrowIfCancellationRequested();

				await ReportCollectionProgress();
			}

			// asked of each machine separately: awaiting them together hands back the first failure
			// and nothing else, so a single machine that could not write its archive - an unreachable
			// share, a full disk - used to end the whole download and throw away what every other
			// machine had already collected.
			foreach( var st in _slaveTasks )
				( st.Result, st.Failure ) = await Outcome( st.Task! );
		}

		/// <summary>How many problems the closing message names before it stops naming them.</summary>
		const int _maxErrorsShown = 5;

		/// <summary>
		/// What the operator is told when the download is done.
		/// </summary>
		/// <remarks>
		/// Counts, not lists. What a collection could not deliver is mostly not a fault at all - a
		/// crash dump folder on a machine that has never crashed, a log an application has not written
		/// yet - and an incident package over forty machines names hundreds of such things. Listing
		/// them one sentence each produced a dialog nobody reads, which is worse than one nobody reads
		/// carefully: the real problems were in there too, indistinguishable from the ordinary
		/// absences surrounding them.
		///
		/// Nothing is lost by shortening it, because the archive is the record and always was: every
		/// absence is in the `_incomplete.txt` of the machine it belongs to, and every failure is in
		/// `_comment.txt`, which also names the machines that delivered nothing. The dialog's job is
		/// to say whether the archive is worth opening and whether anything wants looking at.
		/// </remarks>
		public static string ComposeClosingMessage(
				IReadOnlyList<string> archives,
				int filesCollected,
				int machinesDelivered,
				int notCollectedCount,
				IReadOnlyList<string> machinesWithNoArchive,
				IReadOnlyList<string> errors )
		{
			var text = new StringBuilder();

			if( archives.Count > 0 )
			{
				text.AppendLine( "Files downloaded:" );
				text.AppendLine();
				foreach( var x in archives ) text.AppendLine( $"    {x}" );
				text.AppendLine();
				text.AppendLine( $"{filesCollected} file(s) from {machinesDelivered} machine(s)." );
			}
			else
			{
				text.AppendLine( "No files downloaded." );
			}

			// Said plainly rather than as an error, and counted rather than listed: the archive is
			// fine, it just holds less than the package names, and the detail is inside it.
			if( notCollectedCount > 0 )
			{
				text.AppendLine( $"{notCollectedCount} item(s) named by the package had nothing to"
						+ ( archives.Count > 0
							? " collect - see _incomplete.txt in the archive."
							: " collect." ) );
			}

			// The one absence that does not explain itself, so it is named however many there are:
			// a machine with no archive has no _incomplete.txt either, and what it was holding is
			// not recorded anywhere.
			if( machinesWithNoArchive.Count > 0 )
			{
				text.AppendLine();
				text.AppendLine( $"Nothing at all from {string.Join( ", ", machinesWithNoArchive )}"
						+ " - not even a record of what was there." );
			}

			if( errors.Count > 0 )
			{
				text.AppendLine();
				text.AppendLine( errors.Count == 1
						? "One problem:"
						: $"{errors.Count} problems"
							+ ( archives.Count > 0 ? " - all of them in _comment.txt in the archive:" : ":" ) );

				foreach( var e in errors.Take( _maxErrorsShown ) )
					text.AppendLine( $"    {Tools.JustFirstLine( e )}" );

				if( errors.Count > _maxErrorsShown )
					text.AppendLine( $"    ...and {errors.Count - _maxErrorsShown} more." );
			}

			return text.ToString();
		}

		/// <summary>
		/// What one slave came back with: its result, or the reason there is none.
		/// </summary>
		/// <remarks>
		/// A cancellation is nobody's private failure - the operator stopped the whole download - so
		/// it is left to propagate.
		/// </remarks>
		public static async Task<(DownloadZippedSlave.TResult? Result, Exception? Failure)> Outcome(
				Task<DownloadZippedSlave.TResult> task )
		{
			try
			{
				return ( await task, null );
			}
			catch( OperationCanceledException )
			{
				throw;
			}
			catch( Exception e )
			{
				return ( null, e );
			}
		}

		/// <summary>
		/// Waits for the merge, passing its progress on as the last stretch of ours.
		/// </summary>
		async Task<MergeZipped.TResult?> FollowMerge( Task<MergeZipped.TResult?> mergeTask, Guid mergeInstance )
		{
			while( true )
			{
				var finished = await Task.WhenAny( mergeTask, Task.Delay( _pollPeriodMs, CancellationToken ) );
				if( finished == mergeTask ) break;

				// WhenAny hands back the cancelled delay rather than throwing
				CancellationToken.ThrowIfCancellationRequested();

				var state = await Dirig.GetScriptStateAsync( mergeInstance );
				var fraction = state?.Progress ?? 0.0;

				await Publish(
					string.IsNullOrEmpty( state?.Text ) ? "Merging the collected files..." : state!.Text,
					_collectedAt + ( 1.0 - _collectedAt ) * fraction );
			}

			return await mergeTask;
		}

		/// <summary>
		/// Asks every slave how far it has got and publishes the total.
		/// </summary>
		async Task ReportCollectionProgress()
		{
			long done = 0;
			long total = 0;
			int finishedMachines = 0;
			int announced = 0;
			string? currentFile = null;

			foreach( var st in _slaveTasks )
			{
				bool finished = st.Task?.IsCompleted ?? false;
				if( finished ) finishedMachines++;

				var state = await Dirig.GetScriptStateAsync( st.scriptId );

				// A machine announces its progress only while it works; once it is done its state
				// carries the result instead, which deserializes into an empty progress rather than
				// into nothing - so what it announced has to be remembered, or its bytes vanish from
				// the total and the fraction falls back down the bar.
				var reported = Tools.Deserialize<DownloadZippedSlave.TProgress>( state?.Data );
				if( reported is not null && reported.BytesTotal > 0 ) st.LastProgress = reported;

				if( st.LastProgress is null ) continue;
				announced++;

				// a machine that has finished has done all of what it announced
				done += finished ? st.LastProgress.BytesTotal : st.LastProgress.BytesDone;
				total += st.LastProgress.BytesTotal;

				if( currentFile is null && !finished && !string.IsNullOrEmpty( st.LastProgress.CurrentFile ) )
					currentFile = st.LastProgress.CurrentFile;
			}

			// The byte fraction is only trustworthy once every machine has said how much it has to
			// collect: a machine announcing late would enlarge the total and pull the fraction back
			// down. Until then, count the machines - coarse, but it cannot go backwards.
			bool everyoneAnnounced = announced == _slaveTasks.Count && total > 0;

			double fraction = everyoneAnnounced
								? Math.Min( 1.0, (double) done / total )
								: ( _slaveTasks.Count > 0 ? (double) finishedMachines / _slaveTasks.Count : 1.0 );

			var text = $"Collecting from {_slaveTasks.Count} machine(s)"
					+ ( total > 0 ? $" - {FileTail.FormatSize( done )} of {FileTail.FormatSize( total )}" : "" )
					+ ( currentFile is not null ? $" - {currentFile}" : "" );

			await Publish( text, _resolvedAt + ( _collectedAt - _resolvedAt ) * fraction );
		}

		/// <summary>The last fraction published, so that the bar cannot go backwards.</summary>
		double _lastPublished;

		/// <summary>
		/// Publishes progress that never decreases.
		/// </summary>
		/// <remarks>
		/// The pieces this is assembled from - machines announcing their sizes at different moments,
		/// a machine that stops reporting when it finishes - can each make an honest number smaller
		/// than the one before it. A bar that goes backwards reads as a fault in the operation, which
		/// is worse than a bar that is briefly pessimistic, so the last value is the floor.
		/// </remarks>
		async Task Publish( string text, double progress )
		{
			_lastPublished = Math.Max( _lastPublished, progress );
			await SetStatus( text, null, _lastPublished );
		}

		/// <summary>
		/// Stops the slaves and removes what they have produced so far.
		/// </summary>
		/// <remarks>
		/// Killing only this script would leave every slave compressing happily to the end. The
		/// staging folder is removed by a merge with no parts to merge - it clears the folder
		/// whatever the outcome, and produces nothing when given an empty list.
		/// </remarks>
		async Task CancelSlavesAndCleanUp()
		{
			foreach( var st in _slaveTasks )
			{
				try { await Dirig.SendAsync( new Net.KillScriptMessage( Requestor, st.scriptId ) ); }
				catch( Exception e ) { log.Warn( $"Could not stop the collection on {st.MachineName}: {e.Message}" ); }
			}

			if( string.IsNullOrEmpty( _stagingFolder ) || string.IsNullOrEmpty( _requestorMachine ) )
				return;

			// Give them a moment to really stop before the folder is taken away. A slave still
			// writing would fail the deletion and then recreate the folder for its own cleanup,
			// leaving an empty one behind. Their tasks end cancelled, which WhenAny does not throw on.
			var stopping = Task.WhenAll( from x in _slaveTasks select x.Task );
			await Task.WhenAny( stopping, Task.Delay( _slaveStopTimeoutMs ) );

			try
			{
				await Dirig.RunScriptAsync<MergeZipped.TArgs, MergeZipped.TResult>(
					_requestorMachine, MergeZipped._Name, null,
					new MergeZipped.TArgs()
					{
						StagingFolder = _stagingFolder,
						DestinationFile = _finalArchive,
						Parts = new List<MergeZipped.TPart>(), // nothing to merge: just take the folder away
					},
					$"Cleaning up the cancelled download on {_requestorMachine}", out var _
				);
			}
			catch( Exception e )
			{
				log.Warn( $"Could not remove the staging folder {_stagingFolder}: {e.Message}" );
			}
		}

		/// <summary>
		/// The note that goes into the archive: what was collected, from where, when and why.
		/// </summary>
		/// <remarks>
		/// Written even when the operator said nothing, because the header alone answers the
		/// questions an archive raises months later - which machines, at which addresses, from which
		/// package, and when. The addresses are the ones the master sees on the connections, which
		/// is what Dirigent actually knows; where the config disagrees, both are given.
		/// </remarks>
		/// <param name="unreachable">
		/// Machines that were online but could not deliver anything, and why. They are named in the
		/// note rather than only in the dialog: a machine that contributed nothing leaves no trace in
		/// the archive, so without this an incomplete collection is indistinguishable from a complete
		/// one by the time anybody opens it.
		/// </param>
		async Task<string> ComposeCoverNote( string? comment, string title, string packageId,
				string requestorMachine, Dictionary<string, ClientState> clientStates,
				IReadOnlyDictionary<string, string> unreachable )
		{
			var machineDefs = ( await Dirig.GetAllMachinesDefAsync() ).ToDictionary( x => x.Id, x => x );

			string Describe( string machine ) => DescribeMachine( machine, clientStates, machineDefs );

			// the machines that actually collected, not the ones that were merely online
			var machines = ( from st in _slaveTasks select st.MachineName ).ToList();

			var text = new StringBuilder();

			// One machine per line, in name order. A system of fifty machines, each with a couple of
			// addresses, is unreadable as one line - and the order a HashSet yields them in is not
			// one anybody can look something up in. The continuation lines align under the first.
			const string label = "Machines  : ";
			var perMachineLine = Environment.NewLine + new string( ' ', label.Length );

			var described = machines
					.OrderBy( m => m, StringComparer.OrdinalIgnoreCase )
					.Select( Describe );

			text.AppendLine( $"Collected : {DateTime.Now:yyyy-MM-dd HH:mm:ss}" );
			text.AppendLine( $"Package   : {title}" + ( string.IsNullOrEmpty( packageId ) ? "" : $"   [{packageId}]" ) );
			text.AppendLine( label + string.Join( perMachineLine, described ) );
			text.AppendLine( $"Downloaded to: {Describe( requestorMachine )}" );
			text.AppendLine( $"Dirigent  : {System.Reflection.Assembly.GetExecutingAssembly().GetName().Version}" );

			// and what is not in here, before anything else about the contents
			var missing = DescribeMissing( unreachable, Describe );
			if( missing is not null ) text.Append( missing );

			var failures = DescribeFailures();
			if( failures is not null ) text.Append( failures );

			// What the archive covers, when a Clear or a Mark drew the line: the difference between
			// "these are the logs" and "these are the logs of one run" is the first thing whoever opens
			// the archive needs to know, and it belongs at the top rather than in each entry's header.
			var marked = MarkWindow();
			if( marked is not null ) text.AppendLine( marked );

			text.AppendLine();
			text.AppendLine( string.IsNullOrWhiteSpace( comment ) ? "(no comment)" : comment.Trim() );

			return text.ToString();
		}

		/// <summary>
		/// The given name, or the first numbered variant of it that is not taken in the folder.
		/// </summary>
		/// <remarks>
		/// The name carries the time to the minute, so two downloads of the same package within one
		/// minute used to collide - and the second one failed at the very end, after everything had
		/// been collected, on writing the archive. Downloading again after a transfer went wrong is
		/// precisely when that happens.
		///
		/// The staging folder is checked as well: a download in progress holds one, and taking its
		/// name would put two collections into the same folder.
		/// </remarks>
		static string FreeName( string folder, string baseName )
		{
			bool Taken( string name )
				=> File.Exists( Path.Combine( folder, name + ".zip" ) )
				|| Directory.Exists( Path.Combine( folder, name + "_parts" ) );

			if( !Taken( baseName ) ) return baseName;

			for( int i = 2; i < 1000; i++ )
			{
				var candidate = $"{baseName}_{i}";
				if( !Taken( candidate ) ) return candidate;
			}

			return baseName; // give up and let the write fail with its own message
		}

		/// <summary>
		/// The part of the note that names the machines whose files are not in the archive at all,
		/// or null when every machine delivered.
		/// </summary>
		/// <remarks>
		/// Deliberately loud and deliberately near the top. The dialog that reported this is long gone
		/// by the time somebody opens the archive, and a machine that never ran a collection writes no
		/// _incomplete.txt either - so this line is the only thing standing between a reader and the
		/// assumption that what they are looking at is the whole system.
		/// </remarks>
		public static string? DescribeMissing( IReadOnlyDictionary<string, string> unreachable,
				Func<string, string> describe )
		{
			if( unreachable.Count == 0 ) return null;

			var text = new StringBuilder();
			text.AppendLine();
			text.AppendLine( "*** NOT COLLECTED - these machines were online but could not deliver"
							+ " their files, so nothing of theirs is in this archive: ***" );

			foreach( var machine in unreachable.Keys.OrderBy( m => m, StringComparer.OrdinalIgnoreCase ) )
			{
				text.AppendLine( $"    {describe( machine )}" );
				text.AppendLine( $"        {unreachable[machine]}" );
			}

			return text.ToString();
		}

		/// <summary>
		/// The part of the note listing what went wrong on the machines that did collect, or null if
		/// nothing did.
		/// </summary>
		/// <remarks>
		/// A file that could not be read is reported by its machine and shown in the dialog; the
		/// archive itself said nothing about it, which is the same problem one size smaller.
		/// </remarks>
		string? DescribeFailures()
		{
			var failed = ( from st in _slaveTasks
						   where st.Failure is not null
							  || ( st.Result is not null && st.Result.Exceptions.Count > 0 )
						   orderby st.MachineName, StringComparer.OrdinalIgnoreCase
						   select st ).ToList();

			if( failed.Count == 0 ) return null;

			var text = new StringBuilder();
			text.AppendLine();
			text.AppendLine( "Problems during collection:" );

			foreach( var st in failed )
			{
				// A machine that produced no archive at all is stated as such and stated loudly.
				// Its files are missing and so is its own account of them - it wrote no
				// _incomplete.txt either - so this line is the only record that it was asked.
				if( st.Failure is not null )
				{
					text.AppendLine( $"    *** {st.MachineName}: nothing from this machine is in the"
							+ $" archive, and it left no record of what it held ***" );
					text.AppendLine( $"        {Tools.JustFirstLine( st.Failure.Message )}" );
				}

				if( st.Result is null ) continue;

				foreach( var e in st.Result.Exceptions )
					text.AppendLine( $"    {st.MachineName}: {Tools.JustFirstLine( e.Message )}" );
			}

			return text.ToString();
		}

		/// <summary>
		/// The line saying that this archive holds one run rather than the whole history, or null if
		/// no mark applied to any of the collected files.
		/// </summary>
		string? MarkWindow()
		{
			var marks = ( from st in _slaveTasks
						  where st.Result is not null && st.Result.MarkedFileCount > 0
						  select st.Result! ).ToList();

			if( marks.Count == 0 ) return null;

			var files = marks.Sum( x => x.MarkedFileCount );
			var earliest = marks.Where( x => x.EarliestMark.HasValue ).Min( x => x.EarliestMark );

			// Named after what somebody actually clicked. A Clear cannot empty a log that is being
			// written to, so it draws the line instead - and an archive that then talks about a "mark"
			// reads as though the Clear had not run.
			var by = marks.Select( x => x.MarkedBy ).Where( x => !string.IsNullOrEmpty( x ) ).Distinct().ToList();
			var drawnBy = by.Count == 1 ? by[0] : "Clear or Mark";

			return earliest.HasValue
					? $"Since     : {earliest:yyyy-MM-dd HH:mm:ss} - {files} file(s) hold only what was"
						+ $" written after the {drawnBy} of that time; the rest are complete."
					: $"Since     : {files} file(s) hold only what was written after the {drawnBy}.";
		}

		/// <summary>
		/// A machine and the addresses that identify it.
		/// </summary>
		/// <remarks>
		/// What the machine reports about itself is the reliable source: the config usually declares
		/// no address at all, and what the master observes is where the connection came from, which is
		/// loopback for anything running beside it. All the reported addresses are given, since a
		/// machine has several for good reasons and the one that matters depends on the question being
		/// asked - with the configured address first, when the config named one.
		///
		/// A configured address the machine does not actually have is called out. It is a real
		/// misconfiguration, and it otherwise shows up much later as a file share that cannot be
		/// reached.
		/// </remarks>
		static string DescribeMachine( string machine, Dictionary<string, ClientState> clientStates,
				Dictionary<string, MachineDef> machineDefs )
		{
			machineDefs.TryGetValue( machine, out var def );
			clientStates.TryGetValue( machine, out var state );

			// A loopback address tells nobody where a machine is, and every machine has one - so a
			// config declaring 127.0.0.1, which is how a single-machine system is usually written,
			// is neither worth printing nor a disagreement with what the machine reports.
			var configured = Locating( def?.IP );
			var connectedFrom = Locating( state?.IP );
			var reported = state?.Ident?.LocalAddresses ?? new List<string>();

			// the addresses go in brackets: several machines each with several addresses is otherwise
			// one long comma-separated line in which nothing shows where a machine ends
			if( reported.Count == 0 )
			{
				// an older client, or one that could not read its interfaces
				if( configured is not null )
					return connectedFrom is not null && connectedFrom != configured
							? $"{machine} [{configured}] (connected from {connectedFrom})"
							: $"{machine} [{configured}]";

				return connectedFrom is not null ? $"{machine} (connected from {connectedFrom})" : machine;
			}

			// the configured address first, so the one the config builds paths from leads the list
			var ordered = reported.OrderByDescending( a => a == configured ).ToList();
			var text = $"{machine} [{string.Join( ", ", ordered )}]";

			if( configured is not null && !reported.Contains( configured ) )
				text += $" (the config says {configured}, which this machine does not have)";

			return text;
		}

		/// <summary>
		/// The address if it locates the machine, null if it does not - empty, or loopback, which
		/// every machine has and which points back at whoever is asking.
		/// </summary>
		static string? Locating( string? ip )
			=> string.IsNullOrEmpty( ip ) || IsLoopback( ip! ) ? null : ip;

		static bool IsLoopback( string ip )
			=> System.Net.IPAddress.TryParse( ip, out var parsed ) && System.Net.IPAddress.IsLoopback( parsed );

		/// <summary>
		/// Whether the action still asks for the withdrawn one-archive-per-machine mode.
		/// </summary>
		static bool MentionsPerMachine( string? actionArgs )
			=> !string.IsNullOrEmpty( actionArgs )
				&& actionArgs.IndexOf( "perMachine", StringComparison.OrdinalIgnoreCase ) >= 0;

		/// <summary>
		/// Finds the machine the requestor of the download runs on.
		/// </summary>
		/// <remarks>
		/// The client name of an agent equals its machine id. A GUI names itself
		/// "{machineId}_gui_{guid}", which says where it runs even where several machines answer at
		/// the same address; the address is the fallback for a client named in some other way.
		/// </remarks>
		string FindRequestorMachine( Dictionary<string, ClientState> clientStates )
		{
			clientStates.TryGetValue( Requestor, out var requestorState );

			if( requestorState?.Ident?.IsAgent ?? false )
				return Requestor;

			// the machine id a GUI carries in its name
			var guiTag = Requestor.LastIndexOf( "_gui_", StringComparison.Ordinal );
			if( guiTag > 0 )
			{
				var machineId = Requestor.Substring( 0, guiTag );
				if( IsConnectedAgent( clientStates, machineId ) )
					return machineId;
			}

			// an agent connected from the same address as the requestor
			if( !string.IsNullOrEmpty( requestorState?.IP ) )
			{
				foreach( var (name, state) in clientStates )
				{
					if( state.Connected && ( state.Ident?.IsAgent ?? false ) && state.IP == requestorState.IP )
						return name;
				}
			}

			// Nothing identifies the requestor - a CLI or REST caller has no machine of its own - so
			// the files go to the machine this script runs on. Never an empty id: the resolver reads
			// that as "global" and hands the path back with its variables unexpanded.
			var ourMachine = string.IsNullOrEmpty( Dirig.Name ) ? Dirig.MachineId : Dirig.Name;

			if( string.IsNullOrEmpty( ourMachine ) )
				throw new Exception(
					$"Could not tell what machine to download to. The requestor '{Requestor}' is not an "
					+ $"agent and names no machine, and this host does not know its own. Name one with "
					+ $"the ToMachine argument." );

			log.Info( $"DownloadZipped: the requestor '{Requestor}' is on no known machine; downloading to '{ourMachine}'." );
			return ourMachine;
		}

		static bool IsConnectedAgent( Dictionary<string, ClientState> clientStates, string machineId )
			=> clientStates.TryGetValue( machineId, out var state )
				&& state.Connected
				&& ( state.Ident?.IsAgent ?? false );

		/// <summary>Everything the tree has to say about what it could not deliver.</summary>
		static void CollectNotes( VfsNodeDef node, List<string> notes )
		{
			if( node.Notes is not null ) notes.AddRange( node.Notes );

			foreach( var child in node.Children )
				CollectNotes( child, notes );
		}

		void CollectMachines( VfsNodeDef container, HashSet<string> allMachines )
		{
			foreach( var child in container.Children )
			{
				if (!string.IsNullOrEmpty( child.MachineId ))
				{
					allMachines.Add( child.MachineId );
				}
				CollectMachines( child, allMachines );
			}
		}
		

	}

}
