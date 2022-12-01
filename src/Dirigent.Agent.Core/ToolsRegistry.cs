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
	public class ToolsRegistry
	{
        private SharedContext _sharedContext;

		// all individual instances of some tool apps
		private Dictionary<Guid, LocalApp> _instances = new();

		// all tool types available
		private Dictionary<string, AppDef> _defs; // toolId => AppDef

		FileRegistry _fileReg;

		public ToolsRegistry( SharedContext shCtx, IEnumerable<AppDef> toolDefs, FileRegistry fileRegistry )
		{
			_sharedContext = shCtx;
			_defs = toolDefs.ToDictionary( x => x.Id.AppId ); // toolId is stored as the AppId
			_fileReg = fileRegistry;
		}

		public Guid StartTool( ToolRef tool, Dictionary<string,string>? vars=null )
		{
			if(! _defs.TryGetValue( tool.Id, out var toolAppDef ) )
				throw new Exception( $"Tool '{tool}' not available" );

			
			// replace the tool args with those from the ToolRef
			if( !string.IsNullOrEmpty( tool.CmdLineArgs ) )
			{
				// make a clone as are going to modify it
				toolAppDef = toolAppDef.Clone();
				toolAppDef.CmdLineArgs = tool.CmdLineArgs;
			}

			var localApp = new LocalApp( toolAppDef, _sharedContext );

			try
			{
				localApp.StartApp( vars: vars );

				// store
				var guid = Guid.NewGuid();
				_instances[guid] = localApp;

				return guid;
			}
			catch
			{
				localApp.Dispose();
				throw;
			}

		}

		public Guid StartAppBoundTool( ToolRef tool, AppDef boundTo )
		{
			var vars = new Dictionary<string,string>()
			{
				{ "APP_IDTUPLE", boundTo.Id.ToString() },
				{ "APP_MACHINEID", boundTo.Id.MachineId },
				{ "APP_APPID", boundTo.Id.AppId },
			};
			return StartTool( tool, vars );
		}
		
		public Guid StartMachineBoundTool( ToolRef tool, MachineDef boundTo )
		{
			var vars = new Dictionary<string,string>()
			{
				{ "MACHINE_ID", boundTo.Id },
				{ "MACHINE_IP",  _fileReg.GetMachineIP( boundTo.Id, $"tool {tool}" ) },
			};
			return StartTool( tool, vars );
		}

		public Guid StartFileBoundTool( ToolRef tool, VfsNodeDef boundTo )
		{
			var vars = new Dictionary<string,string>()
			{
				{ "FILE_ID", boundTo.Id },
				{ "FILE_PATH", _fileReg.GetFilePath( boundTo ) },
			};
			return StartTool( tool, vars );
		}

		public Guid StartFilePackageBoundTool( ToolRef tool, VfsNodeDef boundTo )
		{
			// FIXME: generate package content description file to a temp folder and put its full name to the vars
			var vars = new Dictionary<string,string>()
			{
				//{ "FILE_ID", boundTo.Id },
				//{ "FILE_PATH", _fileReg.GetFilePath( boundTo ) },
			};
			return StartTool( tool, vars );
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
