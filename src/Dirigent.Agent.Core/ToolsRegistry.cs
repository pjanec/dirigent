using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent
{
	/// <summary>
	/// Arguments passed to the script called as a result of user clicking the script-based action menu item.
	/// </summary>
	[MessagePack.MessagePackObject]
	public class TScriptActionArgs
	{
		/// <summary>
		/// Generic string arguments as defined by the ScriptActionDef.Args.
		/// </summary>
		[MessagePack.Key( 1 )]
		public string? Args;
		
		/// <summary>
		/// Variables associated with the item (file, app, etc.)
		/// </summary>
		[MessagePack.Key( 2 )]
		public Dictionary<string, string>? Vars;
	}



	/// <summary>
	/// Handles tool app instances on a client (usually a GUI as tools are invoked interactively by the users from dirigent's UI)
	/// </summary>
	/// <remarks>
	/// Tools are defined in LocalConfig. They can be bound to (started with reference to) an app, machine or file/package.
	/// Each tool app can be started multiple times, once for each resource the tool is bound to.
	/// </remarks>
	public class ToolsRegistry
	{
        private SharedContext _sharedContext;

		// all individual instances of some tool apps
		private Dictionary<Guid, LocalApp> _instances = new();

		// all tool types available
		private Dictionary<string, AppDef> _defs; // toolId => AppDef

		FileRegistry _fileReg;
		ReflectedScriptRegistry _reflScriptReg;

		public ToolsRegistry( SharedContext shCtx, IEnumerable<AppDef> toolDefs, FileRegistry fileRegistry, ReflectedScriptRegistry reflScriptReg )
		{
			_sharedContext = shCtx;
			_defs = toolDefs.ToDictionary( x => x.Id.AppId ); // toolId is stored as the AppId
			_fileReg = fileRegistry;
			_reflScriptReg = reflScriptReg;
		}

		public void StartAction( ActionDef action, Dictionary<string,string>? vars=null )
		{
			if (action is ToolActionDef toolAction)
			{
				StartTool( toolAction, vars );
			}
			else if (action is ScriptActionDef scriptAction)
			{
				StartScript( scriptAction, vars );
			}
			else
			{
				throw new Exception( $"Unknown action type: {action.GetType().Name}" );
			}
		}

		public void StartTool( ToolActionDef tool, Dictionary<string,string>? vars=null )
		{
			if(! _defs.TryGetValue( tool.Name, out var toolAppDef ) )
				throw new Exception( $"Tool '{tool}' not available" );

			
			// replace the tool args with those from the ToolActionDef
			if( !string.IsNullOrEmpty( tool.Args ) )
			{
				// make a clone as are going to modify it
				toolAppDef = toolAppDef.Clone();
				toolAppDef.CmdLineArgs = tool.Args;
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

		public void StartScript( ScriptActionDef script, Dictionary<string,string>? vars=null )
		{
			//var argsString = vars != null ? Tools.ExpandEnvAndInternalVars( script.Args, vars ) : script.Args;
			var argsString = script.Args; // we don't expand the vars here, we pass them to the script as a dictionary so they can be expanded there, on the hosting machine
			var args = new TScriptActionArgs
			{
				Args = argsString,
				Vars = vars
			};

			_reflScriptReg.RunScriptNoWait( script.HostId ?? "", script.Name, null, args, script.Title );
		}

		public void StartAppBoundAction( ActionDef action, AppDef boundTo )
		{
			var vars = new Dictionary<string,string>()
			{
				{ "APP_IDTUPLE", boundTo.Id.ToString() },
				{ "MACHINE_ID", boundTo.Id.MachineId },
				{ "MACHINE_IP",  _fileReg.GetMachineIP( boundTo.Id.MachineId, $"action {action}" ) },
				{ "APP_ID", boundTo.Id.AppId },
				// TODO: resolve app workdir etc. on app's-local computer?
			};
			StartAction( action, vars );
		}
		
		public void StartMachineBoundAction( ActionDef action, string localMachineId )
		{
			var vars = new Dictionary<string,string>()
			{
				{ "MACHINE_ID", localMachineId },
				{ "MACHINE_IP",  _fileReg.GetMachineIP( localMachineId, $"action {action}" ) },
			};
			StartAction( action, vars );
		}

		public void StartMachineBoundAction( ActionDef action, MachineDef boundTo )
		{
			var vars = new Dictionary<string,string>()
			{
				{ "MACHINE_ID", boundTo.Id },
				{ "MACHINE_IP",  _fileReg.GetMachineIP( boundTo.Id, $"action {action}" ) },
			};
			StartAction( action, vars );
		}

		public void StartFileBoundAction( ActionDef action, VfsNodeDef boundTo )
		{
			var vars = new Dictionary<string,string>()
			{
				{ "FILE_ID", boundTo.Id },
				{ "FILE_PATH", _fileReg.GetFilePath( boundTo ) },
			};
			StartAction( action, vars );
		}

		public void StartFilePackageBoundAction( ActionDef action, VfsNodeDef boundTo )
		{
			// FIXME: generate package content description file to a temp folder and put its full name to the vars
			var vars = new Dictionary<string,string>()
			{
				//{ "FILE_ID", boundTo.Id },
				//{ "FILE_PATH", _fileReg.GetFilePath( boundTo ) },
			};
			StartAction( action, vars );
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
