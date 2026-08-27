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
		};

		//[MessagePack.MessagePackObject]
		public class TResult
		{
		}

		class SlaveTask
		{
			public string MachineName="";
			public Guid scriptId;
			public Task<DownloadZippedSlave.TResult>? Task;
			public DownloadZippedSlave.TResult? Result;
		}


		protected async override Task<string?> Run()
		{
			var args = Tools.Deserialize<TArgs>( Args );
			if( args is null ) throw new NullReferenceException("Args is null");
			if( args.VfsNode is null ) throw new NullReferenceException("Args.VfsNode is null");

			try
			{
				// if a single file, create artificial container containing this single file
				var title = args.VfsNode.Title;
				var titleSource = args.VfsNode;
				VfsNodeDef container;
				if( args.VfsNode.IsContainer )
				{
					container = args.VfsNode;
					titleSource = args.VfsNode;
				}
				else
				{
					container = new VFolderDef() { Title = title, Children = new List<VfsNodeDef>() { args.VfsNode } };
				}
				if (string.IsNullOrEmpty( title )) title = Path.GetFileName(titleSource.Path??"");
				if (string.IsNullOrEmpty( title )) title = titleSource.Id;
				if (string.IsNullOrEmpty( title )) title = "file";
				container.Title = title;

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
					return Tools.Serialize( new TResult {} );
				}

				// by default all the files end up in one single archive;
				// "perMachine" in the action arguments keeps one archive per machine instead
				bool perMachine = WantsPerMachine( args.Args );

				await Dirig.SendAsync( new Net.UserNotificationMessage
				{
					HostClientId = Requestor,
					Category=Net.UserNotificationMessage.ECategory.Info,
					PresentationType = Net.UserNotificationMessage.EPresentationType.BalloonTip,
					Message = $"Downloading from {onlineMachines.Count} machine(s)...",
					Timeout = 1.0,
				});

				var results = Array.Empty<DownloadZippedSlave.TResult>();

				// The files are downloaded to the download folder of the machine the requestor runs on.
				// %DOWNLOADS% gets expanded during the resolution, which happens on that very machine.
				var requestorMachine = FindRequestorMachine( clientStates );

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
				string zipFileBase = System.IO.Path.GetFileName(title) + DateTime.Now.ToString("_yyMMdd_HHmm");

				// Each machine produces its own archive. To get a single one, the machines upload
				// their archives to a staging folder next to the final one and a merging script
				// then joins them locally on the requestor's machine.
				string stagingFolderName = $"{zipFileBase}_parts";

				// the folder each slave uploads to, as a local path and as a UNC path; a slave picks
				// whichever of them it can actually reach
				var localSlaveDestination = perMachine
						? downloadsFolder
						: System.IO.Path.Combine( downloadsFolder, stagingFolderName );

				var uncSlaveDestination = string.IsNullOrEmpty( uncDownloadsFolder )
						? null
						: ( perMachine
							? uncDownloadsFolder
							: System.IO.Path.Combine( uncDownloadsFolder, stagingFolderName ) );

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
				var slaveTasks = new List<SlaveTask>();
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
					slaveTasks.Add( st );
				}

				// wait for all of them to finish
				results = await Task.WhenAll( (from x in slaveTasks select x.Task) );
				for (int i = 0; i < results.Length; i++) slaveTasks[i].Result = results[i];

				var downloadedFiles = (from x in results where x is not null orderby x.ZipFileName select x.ZipFileName).ToList();

				bool hadErrors = false;
				var errorMsg = $"Errors encountered during download:\n\n";
				foreach( var (mach, message) in unreachable )
				{
					hadErrors = true;
					errorMsg += $"{mach}:\n    {message}\n\n";
				}
				foreach( var st in slaveTasks )
				{
					if( st.Result!=null && st.Result.Exceptions.Count > 0 )
					{
						hadErrors = true;
						errorMsg += $"{st.MachineName}:";
						foreach (var e in st.Result.Exceptions) errorMsg += $"\n    {e.Message}";
						errorMsg += "\n\n";
					}
				}

				// join the per-machine archives into a single one, on the machine holding them
				// (nothing to join if not a single slave could be started)
				if( !perMachine && slaveTasks.Count > 0 )
				{
					var parts = (from x in slaveTasks
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

						// files from a single machine need no folder to tell them apart from the others
						PrefixWithMachine = parts.Count > 1,
					};

					var mergeResult = await Dirig.RunScriptAsync<MergeZipped.TArgs, MergeZipped.TResult>(
						requestorMachine, MergeZipped._Name, null, mergeArgs,
						$"Merging the downloaded files on {requestorMachine}", out var mergeInst
					);

					downloadedFiles = string.IsNullOrEmpty( mergeResult?.ZipFileName )
											? new List<string>()
											: new List<string>() { mergeResult!.ZipFileName };

					if( mergeResult is not null && mergeResult.Exceptions.Count > 0 )
					{
						hadErrors = true;
						errorMsg += $"{requestorMachine} (merging):";
						foreach (var e in mergeResult.Exceptions) errorMsg += $"\n    {e.Message}";
						errorMsg += "\n\n";
					}
				}

				// tell the user it's all done
				var clickAction = new ToolActionDef { Name = "WinExplorer", Args = $"/select,\"{Path.Combine( downloadsFolder, downloadedFiles.FirstOrDefault()??"")}\"" };

				var infoMsg = downloadedFiles.Count > 0 ? $"Files downloaded:\n\n" : $"No files downloaded.\n";
				foreach (var x in downloadedFiles) infoMsg += $"    {x}\n";

				await Dirig.SendAsync( new Net.UserNotificationMessage
				{
					HostClientId = Requestor,
					Category=Net.UserNotificationMessage.ECategory.Info, 
					PresentationType = Net.UserNotificationMessage.EPresentationType.MessageBox,
					Message = infoMsg + (hadErrors ? $"\n\n{errorMsg}" : ""),
					Action = clickAction
				});

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
			}

			// all done!
			var result = new TResult {};
			return Tools.Serialize(result);
		}

		/// <summary>
		/// Whether the action asked for one archive per machine instead of a single merged one.
		/// </summary>
		static bool WantsPerMachine( string? actionArgs )
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

			// no agent found for the requestor - fall back to the machine this script runs on
			log.Warn( $"DownloadZipped: could not determine the machine of the requestor '{Requestor}', using '{Dirig.Name}' instead." );
			return Dirig.Name;
		}

		static bool IsConnectedAgent( Dictionary<string, ClientState> clientStates, string machineId )
			=> clientStates.TryGetValue( machineId, out var state )
				&& state.Connected
				&& ( state.Ident?.IsAgent ?? false );

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
