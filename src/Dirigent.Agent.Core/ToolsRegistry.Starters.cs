using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent
{
	public partial class ToolsRegistry
	{
		public void StartAction( string? requestorId, ActionDef action, Dictionary<string,string>? vars=null, ExpandedVfsNodeDef? vfsNode=null,
						string? tempFileName=null, Action? onFileChanged=null  // FIXME: this is only needed for file-based action
			)
		{
			if (action is ToolAppActionDef toolAction)
			{
				var inst = GetToolAppInstance( requestorId, toolAction, vars );
				StartInstance( inst, tempFileName, onFileChanged );
			}
			else if (action is ScriptActionDef scriptAction)
			{
				StartScript( requestorId, scriptAction, vars, vfsNode );
			}
			else
			{
				throw new Exception( $"Unknown action type: {action.GetType().Name}" );
			}
		}

		IActionInstance GetToolAppInstance( string? requestorId, ToolAppActionDef tool, Dictionary<string,string>? vars=null )
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

			IActionInstance toolInst = new ToolAppInstance( _sharedContext, toolAppDef, vars );

			return toolInst;
		}
		
		//void StartToolApp( string? requestorId, ToolAppActionDef tool, Dictionary<string,string>? vars=null, string? tempFileName=null, Action? onFileChanged=null )
		//{
		//	var inst = GetToolAppInstance( requestorId, tool, vars );
		//	StartInstance( inst, tempFileName, onFileChanged );
		//}

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


		void StartInstance( IActionInstance toolInst, string? tempFileName=null, Action? onFileChanged=null )
		{
			if( !string.IsNullOrEmpty(tempFileName) )
			{
				toolInst = new ActionInstanceTempFileDecorator(
					toolInst,
					tempFileName,
					onFileChanged
				);
			}
			
			try
			{
				toolInst.Start();

				// store
				var guid = Guid.NewGuid();
				_actionInstances[guid] = toolInst;
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

		public void StartFileBoundAction( string requestorId, ActionDef action, ExpandedVfsNodeDef boundTo )
		{
			_pathPerspectivizer.PerspectivizePath( boundTo );
			// TODO:
			// if the path is SSH
			//   - download the file to a temp location
			//   - start the action with the temp file, on file change event upload the file back
			// if the path in local or UNC, start the tool with this path directly

			var vars = new Dictionary<string,string>()
			{
				{ "FILE_ID", boundTo.Id },
				{ "FILE_PATH", boundTo.Path }, // THIS NEEDS TO BE LOCAL/UNC PATH 
			};
			StartAction( requestorId, action, vars, boundTo );
		}

		public void StartFilePackageBoundAction( string requestorId, ActionDef action, ExpandedVfsNodeDef boundTo )
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

			StartAction( requestorId, action, vars, boundTo );
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
