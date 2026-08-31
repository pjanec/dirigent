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

		public ToolsRegistry( SharedContext shCtx, Dictionary<string, AppDef> toolDefs, ReflectedStateRepo reflStates )
		{
			_sharedContext = shCtx;
			_defs = toolDefs;
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
		
		/// <returns>
		/// The instance of the script that was started, so that whoever asked for the action can
		/// follow it - a progress indicator, for one. Empty for a tool, which is a process rather
		/// than something with a state to watch.
		/// </returns>
		public Guid StartAction( string? requestorId, ActionDef action, Dictionary<string,string>? vars=null, VfsNodeDef? vfsNode=null )
		{
			if (action is ToolActionDef toolAction)
			{
				StartTool( requestorId, toolAction, vars );
				return Guid.Empty;
			}
			else if (action is ScriptActionDef scriptAction)
			{
				return StartScript( requestorId, scriptAction, vars, vfsNode );
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

			var localApp = new LocalApp( toolAppDef, _sharedContext, null, null );

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

		public Guid StartScript( string? requestorId, ScriptActionDef script, Dictionary<string,string>? vars=null, VfsNodeDef? vfsNodeDef=null )
		{
			//var argsString = vars != null ? Tools.ExpandEnvAndInternalVars( script.Args, vars ) : script.Args;
			var argsString = script.Args; // we don't expand the vars here, we pass them to the script as a dictionary so they can be expanded on the hosting machine
			var args = new ScriptActionArgs
			{
				Args = argsString,
				Vars = vars,
				VfsNode = vfsNodeDef,
			};

			return _reflScriptReg.RunScriptNoWait( script.HostId ?? "", script.Name, null, args, script.Title );
		}

		public Guid StartAppBoundAction( string? requestorId, ActionDef action, AppDef boundTo )
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
			return StartAction( requestorId, action, vars );
		}
		
		public Guid StartMachineBoundAction( string requestorId, ActionDef action, string localMachineId )
		{
			var vars = new Dictionary<string,string>()
			{
				{ "MACHINE_ID", localMachineId },
				{ "MACHINE_IP",  _fileReg.GetMachineIP( localMachineId ) },
			};
			return StartAction( requestorId, action, vars );
		}

		public Guid StartMachineBoundAction( string requestorId, ActionDef action, MachineDef boundTo )
		{
			var vars = new Dictionary<string,string>()
			{
				{ "MACHINE_ID", boundTo.Id },
				{ "MACHINE_IP",  _fileReg.GetMachineIP( boundTo.Id ) },
			};
			return StartAction( requestorId, action, vars );
		}

		/// <summary>
		/// Starts a script action on a node that has NOT been resolved yet, leaving the resolving to
		/// the script as part of its own work.
		/// </summary>
		/// <remarks>
		/// For a package spanning many apps on several machines the resolve is one remote round trip
		/// per node - 8.4 s for 16 nodes, measured - and doing it before the script starts means
		/// there is no operation for the status bar to show until it is over. Letting the top level
		/// script resolve gives one operation covering resolve, collect and merge.
		///
		/// No FILE_PATH is passed: it cannot be known before resolution. Only scripts that resolve
		/// their own node may be started this way; tool actions must keep the resolved form.
		/// </remarks>
		/// <param name="comment">
		/// What the operator said about why they are collecting, if they were asked. Handed over here
		/// because only the script can put it into the archive it produces.
		/// </param>
		public Guid StartSelfResolvingScriptAction( string requestorId, ScriptActionDef script,
				VfsNodeDef unresolved, string? comment = null )
		{
			var args = new ScriptActionArgs
			{
				Args = script.Args,
				Vars = null,
				VfsNode = unresolved,
				VfsNodeNeedsResolving = true,
				Comment = comment,
			};

			return _reflScriptReg.RunScriptNoWait( script.HostId ?? "", script.Name, null, args, script.Title );
		}

		public Guid StartFileBoundAction( string requestorId, ActionDef action, VfsNodeDef boundTo )
		{
			var vars = new Dictionary<string,string>()
			{
				{ "FILE_ID", boundTo.Id },
				{ "FILE_PATH", _fileReg.MakeUNCIfNotLocal( boundTo.Path!, boundTo.MachineId, $"{boundTo}" ) },
			};
			return StartAction( requestorId, action, vars, boundTo );
		}

		public Guid StartFilePackageBoundAction( string requestorId, ActionDef action, VfsNodeDef boundTo )
		{
			// this gets called also for physical folders (then the vsfNode.Path is non-empty)
			
			var vars = new Dictionary<string,string>();

			if( !string.IsNullOrEmpty( boundTo.Path ) )
			{
				vars["FILE_PATH"] = _fileReg.MakeUNCIfNotLocal( boundTo.Path!, boundTo.MachineId, $"{boundTo}" );
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
					var fname = _fileReg.MakeUNCIfNotLocal( node.Path!, node.MachineId, $"{node}" );
					list.Add( fname );
				}
			}
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
