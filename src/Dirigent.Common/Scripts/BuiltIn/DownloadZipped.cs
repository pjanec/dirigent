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


		protected async override Task<byte[]?> Run()
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
				var clientStates = (await Dirig.GetAllClientStates()).ToDictionary( x => x.Key, y => y.Value );
				var onlineMachines = (from x in allMachines where clientStates.ContainsKey(x) && clientStates[x].Connected select x).ToList();

				await Dirig.SendAsync( new Net.UserNotificationMessage
				{
					HostClientId = Requestor,
					Category=Net.UserNotificationMessage.ECategory.Info, 
					PresentationType = Net.UserNotificationMessage.EPresentationType.BalloonTip,
					Message = $"Downloading {onlineMachines.Count} files...",
					Timeout = 1.0,
				});

				var results = Array.Empty<DownloadZippedSlave.TResult>();

				// get UNC path to the download folder
				var downloadsFolder = Tools.GetDownloadFolderPath();
			
				var vfsDownloadFolder = new FolderDef()
				{
					Path = downloadsFolder,
					MachineId = Requestor // FIXME: we need our local machine name here!
				};

				var vfsResolvedDownloadFolder = await Dirig.ResolveAsync( vfsDownloadFolder, true, false );

				// get the name of the archive file to download
				string zipFileBase = title + "_" + DateTime.Now.ToString("yyMMddHHmm");

				// start a slave script on each machine
				var slaveTasks = new List<SlaveTask>();
				foreach (var mach in onlineMachines)
				{
					var slaveScriptName = DownloadZippedSlave._Name;
					var slaveScriptArgs = new DownloadZippedSlave.TArgs()
					{
						Container = container,
						DestinationFolder = vfsResolvedDownloadFolder.Path,
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

				// tell the user it's all done
				var downloadedFiles = (from x in results orderby x.ZipFileName select x.ZipFileName).ToList();
				var clickAction = new ToolActionDef { Name = "WineXplorer", Args = $"/select,\"{Path.Combine( downloadsFolder, downloadedFiles.FirstOrDefault()??"")}\"" };

				var  infoMsg = $"Files downloaded:\n\n";
				foreach (var x in downloadedFiles) infoMsg += $"    {x}\n";
				
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
