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
	public partial class ToolsRegistry : Disposable
	{
        private SharedContext _sharedContext;
		public SharedContext SharedContext => _sharedContext;
		
		// all individual instances of some tool apps
		private Dictionary<Guid, IActionInstance> _actionInstances = new();

		// all tool types available
		private Dictionary<string, AppDef> _appDefs; // toolId => AppDef

		MachineRegistry _machineRegistry;
		PathPerspectivizer _pathPerspectivizer;
		ReflectedScriptRegistry _reflScriptReg;
		ReflectedStateRepo _reflStates;

		public ToolsRegistry( SharedContext shCtx, IEnumerable<AppDef> toolAppDefs, ReflectedStateRepo reflStates )
		{
			_sharedContext = shCtx;
			_appDefs = new( toolAppDefs.ToDictionary( x => x.Id.AppId ), StringComparer.OrdinalIgnoreCase); // toolId is stored as the AppId
			_reflStates = reflStates;
			_reflScriptReg = _reflStates.ScriptReg;
			_machineRegistry = _reflStates.MachineRegistry;
			_pathPerspectivizer = _reflStates.PathPerspectivizer;
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

		public AppDef? GetAppDef( string toolName )
		{
			if (_appDefs.TryGetValue( toolName, out var def ))
			{
				return def;
			}
			return null;
		}

		public string? GetToolIcon( string toolName )
		{
			if( _appDefs.TryGetValue( toolName, out var toolAppDef ) )
			{
				return toolAppDef.Icon;
			}
			return null;
		}
		
		// for StartXXX methods, see ToolsRegistry.Starters.cs

		public void Tick()
		{
			var toRemove = new List<Guid>();

			foreach( var (guid, toolInst) in _actionInstances )
			{
				toolInst.Tick();

				// remove those tool instances not running any more
				if( !toolInst.Running )
				{
					toRemove.Add( guid );
				}
			}

			// remove those tool local apps not running any more (houskeeping)
			foreach( var guid in toRemove )
			{
				var li = _actionInstances[guid];
				li.Dispose();
				_actionInstances.Remove( guid );
			}
		}

		public void Clear()
		{
			_actionInstances.Clear();
		}
	}
}
