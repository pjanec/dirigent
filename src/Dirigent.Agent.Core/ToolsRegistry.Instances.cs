using System;
using System.Collections.Generic;

namespace Dirigent
{

	public partial class ToolsRegistry
	{
		/// <summary>
		/// App or script that can be started and then watched for temination
		/// </summary>
		interface IToolInstance : IDisposable
		{
			bool Running { get; }
			void Start();
			void Tick();
		}

		class ToolAppInstance : Disposable, IToolInstance
		{
			protected Launcher _launcher;
			public bool Running => _launcher.Running;

			public ToolAppInstance( SharedContext sharedContext, AppDef appDef, Dictionary<string,string>? vars )
			{
				_launcher = new Launcher( appDef, sharedContext, vars );
			}

			protected override void Dispose( bool disposing )
			{
				base.Dispose( disposing );
				if (!disposing) return;
				_launcher?.Dispose();
			}

			public void Start()
			{
				_launcher.Launch();
			}

			public void Tick()
			{
			}
		}

		//class ScriptActionInstance : Disposable, IActionInstance
		//{
		//	Script _script;

		//	string? _requestorId;
		//	ScriptActionDef _sad;
		//	Dictionary<string,string>? _vars;
		//	VfsNodeDef? _vfsNodeDef;
			
		//	public ScriptActionInstance( string? requestorId, ScriptActionDef scriptActionDef, Dictionary<string,string>? vars=null, VfsNodeDef? vfsNodeDef=null, string? tempFileName=null, Action? onFileChanged=null )
		//	{
		//		_sad = scriptActionDef;
		//		_vars = vars;
		//		_vfsNodeDef = vfsNodeDef;
		//		_requestorId = requestorId;
				
		//	}

		//	public bool Running => _script.Running;

		//	public void Start()
		//	{
		//		//var argsString = vars != null ? Tools.ExpandEnvAndInternalVars( script.Args, vars ) : script.Args;
		//		var argsString = _sad.Args; // we don't expand the vars here, we pass them to the script as a dictionary so they can be expanded on the hosting machine
		//		var args = new ScriptActionArgs
		//		{
		//			Args = argsString,
		//			Vars = _vars,
		//			VfsNode = _vfsNodeDef,
		//		};

		//		_script = _reflScriptReg.StartScriptWithWatcher( _sad.HostId ?? _requestorId ?? "", _sad.Name, null, args, _sad.Title );
		//	}

		//	public void Tick()
		//	{
		//		_script.Tick();
		//	}

		//	protected override void Dispose( bool disposing )
		//	{
		//		base.Dispose( disposing );
		//		if (!disposing) return;
		//		_script?.Dispose();
		//	}
		//}
	}
}
