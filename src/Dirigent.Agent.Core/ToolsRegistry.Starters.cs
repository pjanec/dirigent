using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dirigent
{
	public partial class ToolsRegistry
	{
		public Task StartAction( string? requestorId, ActionDef action, Dictionary<string,string>? vars=null, ExpandedVfsNodeDef? vfsNode=null,
						Func<Task?>? onActionFinishedAsync=null
			)
		{
			if (action is ToolAppActionDef toolAction)
			{
				var inst = GetToolAppInstance( requestorId, toolAction, vars );
				StartInstance( inst, onActionFinishedAsync );
			}
			else if (action is ScriptActionDef scriptAction)
			{
				// FIXME:
				//   1. GetScriptInstance
				//   2. StartInstance
				StartScript( requestorId, scriptAction, vars, vfsNode );
			}
			else
			{
				throw new Exception( $"Unknown action type: {action.GetType().Name}" );
			}
			return Task.CompletedTask;
		}

		IToolInstance GetToolAppInstance( string? requestorId, ToolAppActionDef tool, Dictionary<string,string>? vars=null )
		{
			if(! _appDefs.TryGetValue( tool.Name, out var toolAppDef ) )
				throw new Exception( $"Tool '{tool}' not available" );

			
			// replace the tool args with those from the ToolActionDef
			if( !string.IsNullOrEmpty( tool.Args ) )
			{
				// make a clone as are going to modify it
				toolAppDef = toolAppDef.Clone();
				toolAppDef.CmdLineArgs = tool.Args;
				if( !string.IsNullOrEmpty( tool.StartupDir ) )
				{
					toolAppDef.StartupDir = tool.StartupDir;
				}
			}

			IToolInstance toolInst = new ToolAppInstance( _sharedContext, toolAppDef, vars );

			return toolInst;
		}
		
		// starts script on the node defined by the action or (if not specified) then on the requestor (falls back to master if neither specified)
		void StartScript( string? requestorId, ScriptActionDef scriptDef, Dictionary<string,string>? vars=null, VfsNodeDef? vfsNodeDef=null, string? tempFileName=null, Action? onFileChanged=null )
		{
			//var argsString = vars != null ? Tools.ExpandEnvAndInternalVars( script.Args, vars ) : script.Args;
			var argsString = scriptDef.Args; // we don't expand the vars here, we pass them to the script as a dictionary so they can be expanded on the hosting machine
			var args = new ScriptActionArgs
			{
				Args = argsString,
				Vars = vars,
				VfsNode = vfsNodeDef,
			};

			_reflScriptReg.RunScriptNoWait( scriptDef.HostId ?? requestorId ?? "", scriptDef.Name, null, args, scriptDef.Title );
		}


		void StartInstance( IToolInstance toolInst, Func<Task?>? onActionFinishedAsync )
		{
			try
			{
				toolInst.Start();

				// store
				var guid = Guid.NewGuid();
				_toolInstances[guid] = new ToolInstanceRecord( toolInst, onActionFinishedAsync );
			}
			catch
			{
				toolInst.Dispose();
				throw;
			}
		}


		public void StartAppBoundAction( string? requestorId, ActionDef action, AppDef boundTo )
		{
			var vars = new Dictionary<string,string>()
			{
				{ "MACHINE_ID", boundTo.Id.MachineId },
				{ "MACHINE_IP", _machineRegistry.GetMachineIP( boundTo.Id.MachineId ) },
				{ "APP_IDTUPLE", boundTo.Id.ToString() },
				{ "APP_ID", boundTo.Id.AppId },
				{ "APP_PID", (_reflStates.GetAppState(boundTo.Id)?.PID ?? -1).ToString() },
				// Note: app workdir variable also available?
			};
			StartAction( requestorId, action, vars );
		}
		
		public void StartMachineBoundAction( string requestorId, ActionDef action, string localMachineId )
		{
			var vars = new Dictionary<string,string>()
			{
				{ "MACHINE_ID", localMachineId },
				{ "MACHINE_IP", _machineRegistry.GetMachineIP( localMachineId ) },
			};
			StartAction( requestorId, action, vars );
		}

		public void StartMachineBoundAction( string requestorId, ActionDef action, MachineDef boundTo )
		{
			var vars = new Dictionary<string,string>()
			{
				{ "MACHINE_ID", boundTo.Id },
				{ "MACHINE_IP", _machineRegistry.GetMachineIP( boundTo.Id ) },
			};
			StartAction( requestorId, action, vars );
		}

		void ShowError( string clientId, string msg, Exception ex )
		{
			_reflStates.Send( new Net.UserNotificationMessage
			{
				HostClientId = clientId,
				Category=Net.UserNotificationMessage.ECategory.Error, 
				PresentationType = Net.UserNotificationMessage.EPresentationType.MessageBox,
				Message = $"{msg}\n\n[{ex.GetType().Name}]\n\n{ex.Message}\n\n{ex.StackTrace}",
			});
		}

		public async Task StartFileBoundAction( string requestorId, ActionDef action, ExpandedVfsNodeDef boundTo )
		{
			if (boundTo.Path is null)
			{
				throw new Exception( $"Cannot start action on a file node with no path. {action}" );
			}
			
			_pathPerspectivizer.PerspectivizePath( boundTo );

			// if the path is SSH
			//   - download the file to a temp location (await async)
			//   - start the action with the temp file
			//   - on file change event upload the file back
			//   - on action finished delete the temp file
			// if the path in local or UNC, start the tool with this path directly


			string localPath = boundTo.Path;
			Func<Task?>? onActionFinishedAsync = null;
			if( PathPerspectivizer.IsSshPath( boundTo.Path ) )
			{
				var sshPath = boundTo.Path;

				// download
				var sshFileHandler = new SshFileHandler( sshPath );
				await sshFileHandler.DownloadAsync( CancellationToken.None );
				localPath = sshFileHandler.LocalPath!;

				Func<CancellationToken, Task> onFileChanged = async (CancellationToken ct) =>
				{
					try	{ await sshFileHandler.UploadAsync( ct ); }
					catch( Exception ex ) {	ShowError(requestorId, $"File upload failed. {sshPath}", ex); }
				};

				var changeMonitor = new FileChangeMonitor( localPath, onFileChanged );

				onActionFinishedAsync = async () => // note: this delegate captures both the changeMonitor and the sshFileHandler, keeping them alive until the action finishes
				{
					await changeMonitor.ForceCheckAsync( CancellationToken.None ); // if the file has changed, this fires the change handler to upload the file
					
					changeMonitor.Dispose(); // stops checking for change
					sshFileHandler.Dispose(); // deletes the temp file
				};
				
			}

			var vars = new Dictionary<string,string>()
			{
				{ "FILE_ID", boundTo.Id },
				{ "FILE_PATH", localPath },	// full path to the file
				{ "FILE_DIR", System.IO.Path.GetDirectoryName( localPath )??"" }, // just the directory name (no file name)
				{ "FILE_NAME", System.IO.Path.GetFileName( localPath )??"" }, // just the file name with extension, no directory
			};
			
			await StartAction( requestorId, action, vars, boundTo, onActionFinishedAsync  );
		}
		

		public Task StartFilePackageBoundAction( string requestorId, ActionDef action, ExpandedVfsNodeDef boundTo )
		{
			// this gets called also for physical folders (then the vsfNode.Path is non-empty)
			
			var vars = new Dictionary<string,string>();

			if( !string.IsNullOrEmpty( boundTo.Path ) )
			{
				vars["FILE_PATH"] = _pathPerspectivizer.MakeUNCIfNotLocal( boundTo.Path!, boundTo.MachineId );
			}
			else
			{
				List<string> list = new();
				MakeFileList( list, boundTo );
				// space separated quoted paths
				vars["FILE_PATH"] = string.Join( " ", list.Select( s => $"\"{s}\"" ) );
			}

			return StartAction( requestorId, action, vars, boundTo );
		}

		// puts all files to a plain list
		void MakeFileList( List<string> list, VfsNodeDef folder )
		{
			foreach( var node in folder.Children )
			{
				if( node.IsContainer )
				{
					MakeFileList( list, node );
				}
				else
				{
					var fname = _pathPerspectivizer.MakeUNCIfNotLocal( node.Path!, node.MachineId );
					list.Add( fname );
				}
			}
		}

	}
}
