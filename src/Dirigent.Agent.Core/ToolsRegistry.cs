using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent
{

	/// <summary>
	/// Handles tool app instances on a client (usually a GUI as tools are invoked interactively by the users from dirigent's UI)
	/// </summary>
	/// <remarks>
	/// Tools are defined in LocalConfig. They can be bound to (started with reference to) an app, machine or file/package.
	/// Each tool app can be started multiple times, once for each resource the tool is bound to.
	/// </remarks>
	public class ToolsRegistry : Disposable
	{
        private SharedContext _sharedContext;

		// all individual instances of some tool apps
		private Dictionary<Guid, LocalApp> _instances = new();

		// all tool types available
		private Dictionary<string, AppDef> _defs; // toolId => AppDef

		FileRegistry _fileReg;
		ReflectedScriptRegistry _reflScriptReg;
		ReflectedStateRepo _reflStates;

		public ToolsRegistry( SharedContext shCtx, IEnumerable<AppDef> toolDefs, ReflectedStateRepo reflStates )
		{
			_sharedContext = shCtx;
			_defs = new( toolDefs.ToDictionary( x => x.Id.AppId ), StringComparer.OrdinalIgnoreCase); // toolId is stored as the AppId
			_fileReg = reflStates.FileReg;
			_reflScriptReg = reflStates.ScriptReg;
			_reflStates = reflStates;
			_reflStates.Client.MessageReceived += OnMessage;
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if (!disposing) return;
			_reflStates.Client.MessageReceived -= OnMessage;
		}

		void OnMessage( Net.Message msg )
		{
			switch( msg )
			{
				case Net.RunActionMessage m:
				{
					if( m.HostClientId == _reflStates.Client.Ident.Name ) // for us?
					{
						StartAction( m.Requestor, m.Def!, m.Vars );
					}
					break;
				}
			}
		}

		public string? GetToolIcon( string toolName )
		{
			if( _defs.TryGetValue( toolName, out var toolAppDef ) )
			{
				return toolAppDef.Icon;
			}
			return null;
		}
		
		public void StartAction( string? requestorId, ActionDef action, Dictionary<string,string>? vars=null, VfsNodeDef? vfsNode=null )
		{
			if (action is ToolActionDef toolAction)
			{
				StartTool( requestorId, toolAction, vars );
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

		public void StartTool( string? requestorId, ToolActionDef tool, Dictionary<string,string>? vars=null )
		{
			if(! _defs.TryGetValue( tool.Name, out var toolAppDef ) )
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

			var localApp = new LocalApp( toolAppDef, _sharedContext, null );

			try
			{
				localApp.StartApp( vars: vars );

				// store
				var guid = Guid.NewGuid();
				_instances[guid] = localApp;
			}
			catch
			{
				localApp.Dispose();
				throw;
			}

		}

		public void StartScript( string? requestorId, ScriptActionDef script, Dictionary<string,string>? vars=null, VfsNodeDef? vfsNodeDef=null )
		{
			//var argsString = vars != null ? Tools.ExpandEnvAndInternalVars( script.Args, vars ) : script.Args;
			var argsString = script.Args; // we don't expand the vars here, we pass them to the script as a dictionary so they can be expanded on the hosting machine
			var args = new ScriptActionArgs
			{
				Args = argsString,
				Vars = vars,
				VfsNode = vfsNodeDef,
			};

			_reflScriptReg.RunScriptNoWait( script.HostId ?? "", script.Name, null, args, script.Title );
		}

		public void StartAppBoundAction( string? requestorId, ActionDef action, AppDef boundTo )
		{
			var vars = new Dictionary<string,string>()
			{
				{ "MACHINE_ID", boundTo.Id.MachineId },
				{ "MACHINE_IP",  _fileReg.GetMachineIP( boundTo.Id.MachineId ) },
				{ "APP_IDTUPLE", boundTo.Id.ToString() },
				{ "APP_ID", boundTo.Id.AppId },
				{ "APP_PID", (_reflStates.GetAppState(boundTo.Id)?.PID ?? -1).ToString() },
				// TODO: resolve app workdir etc. on app's-local computer?
			};
			StartAction( requestorId, action, vars );
		}
		
		public void StartMachineBoundAction( string requestorId, ActionDef action, string localMachineId )
		{
			var vars = new Dictionary<string,string>()
			{
				{ "MACHINE_ID", localMachineId },
				{ "MACHINE_IP",  _fileReg.GetMachineIP( localMachineId ) },
			};
			StartAction( requestorId, action, vars );
		}

		public void StartMachineBoundAction( string requestorId, ActionDef action, MachineDef boundTo )
		{
			var vars = new Dictionary<string,string>()
			{
				{ "MACHINE_ID", boundTo.Id },
				{ "MACHINE_IP",  _fileReg.GetMachineIP( boundTo.Id ) },
			};
			StartAction( requestorId, action, vars );
		}

		public void StartFileBoundAction( string requestorId, ActionDef action, VfsNodeDef boundTo )
		{
			var vars = new Dictionary<string,string>()
			{
				{ "FILE_ID", boundTo.Id },
				{ "FILE_PATH", _fileReg.MakeUNCIfNotLocal( boundTo.Path!, boundTo.MachineId, $"{boundTo}" ) },
			};
			StartAction( requestorId, action, vars, boundTo );
		}

		public void StartFilePackageBoundAction( string requestorId, ActionDef action, VfsNodeDef boundTo )
		{
			// this gets called also for physical folders (then the vsfNode.Path is non-empty)
			
			var vars = new Dictionary<string,string>();

			if( !string.IsNullOrEmpty( boundTo.Path ) )
			{
				vars["FILE_PATH"] = _fileReg.MakeUNCIfNotLocal( boundTo.Path!, boundTo.MachineId, $"{boundTo}" );
			}
			StartAction( requestorId, action, vars, boundTo );
		}

		public void Tick()
		{
			var toRemove = new List<Guid>();

			foreach( var (guid, la) in _instances )
			{
				la.Tick();

				// remove those tool instances not running any more
				if( !la.AppState.Running )
				{
					toRemove.Add( guid );
				}
			}

			// remove those tool local apps not running any more (houskeeping)
			foreach( var guid in toRemove )
			{
				var li = _instances[guid];
				li.Dispose();
				_instances.Remove( guid );
			}
		}

		public void Clear()
		{
			_instances.Clear();
		}
	}
}
