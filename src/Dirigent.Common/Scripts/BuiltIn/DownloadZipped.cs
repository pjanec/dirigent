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

				// local path on the requestor's machine, for telling the user where the files went
				var vfsLocalDownloadFolder = await Dirig.ResolveAsync(
					new FolderDef() { Path = "%DOWNLOADS%", MachineId = requestorMachine }, false, false );
				if (vfsLocalDownloadFolder is null) throw new Exception( $"Could not find the download folder of {requestorMachine}." );
				var downloadsFolder = vfsLocalDownloadFolder.Path!;

				// UNC path to the same folder, for the slaves to upload the archives to
				var vfsResolvedDownloadFolder = await Dirig.ResolveAsync(
					new FolderDef() { Path = "%DOWNLOADS%", MachineId = requestorMachine }, true, false );
				if (vfsResolvedDownloadFolder is null) throw new Exception( "Folder resolution failed." );

				// get the name of the archive file to download
				string zipFileBase = System.IO.Path.GetFileName(title) + DateTime.Now.ToString("_yyMMdd_HHmm");

				// Each machine produces its own archive. To get a single one, the machines upload
				// their archives to a staging folder next to the final one and a merging script
				// then joins them locally on the requestor's machine.
				string stagingFolderName = $"{zipFileBase}_parts";

				var slaveDestinationFolder = perMachine
						? vfsResolvedDownloadFolder.Path!
						: System.IO.Path.Combine( vfsResolvedDownloadFolder.Path!, stagingFolderName );

				// start a slave script on each machine
				var slaveTasks = new List<SlaveTask>();
				foreach (var mach in onlineMachines)
				{
					var slaveScriptName = DownloadZippedSlave._Name;
					var slaveScriptArgs = new DownloadZippedSlave.TArgs()
					{
						Container = container,
						DestinationFolder = slaveDestinationFolder,
						ZipFileBaseName = zipFileBase,
						IncludeGlobals = mach == onlineMachines.First(), // first machine will do the global files
					};

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
				if( !perMachine )
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
		/// The client name of an agent equals its machine id, but a GUI client is named by a GUID,
		/// so for GUIs we look for an agent connected from the same IP address.
		/// </remarks>
		string FindRequestorMachine( Dictionary<string, ClientState> clientStates )
		{
			if( clientStates.TryGetValue( Requestor, out var requestorState ) )
			{
				if( requestorState.Ident?.IsAgent ?? false )
					return Requestor;

				if( !string.IsNullOrEmpty( requestorState.IP ) )
				{
					foreach( var (name, state) in clientStates )
					{
						if( state.Connected && ( state.Ident?.IsAgent ?? false ) && state.IP == requestorState.IP )
							return name;
					}
				}
			}

			// no agent found for the requestor - fall back to the machine this script runs on
			log.Warn( $"DownloadZipped: could not determine the machine of the requestor '{Requestor}', using '{Dirig.Name}' instead." );
			return Dirig.Name;
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
