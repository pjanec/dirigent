using System.Collections.Generic;

namespace Dirigent
{
	/// <summary>
	/// Script entry based of ScriptDef; loaded from SharedConfig; can be addressed via id.
	/// </summary>
	public class ScriptEntry
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public ScriptDef Def;

		public string Id => this.Def.Id;

		public ScriptState State = new();

		// instance of the script
		private Script? _script;

		Dictionary<string, string> _internalVars;

		private Master _master;

		public ScriptEntry( ScriptDef def, Master master )
		{
			Def = def;
			_master = master;

			_internalVars = BuildVars( def, _master.InternalVars );
		}

		public void Start( string? args )
		{
			if( _script is not null ) // already running?
				return;


			if( args is null )
				args = Def.Args;

			var scriptPath = Tools.ExpandEnvAndInternalVars( Def.FileName, _internalVars );
			scriptPath = PathUtils.BuildAbsolutePath( Def.FileName, _master.RootForRelativePaths );

			log.Debug( $"Launching script {Id} with args '{args}' (file: {scriptPath})" );

			_script = ScriptFactory.CreateFromFile( Def.Id, scriptPath, args, _master );
			_script.OnRemoved += HandleScriptRemoved;

			_master.Tickers.Install( _script );

			_script.Init();
		}

		public void Kill()
		{
			if( _script is null ) // not running?
				return;

			_master.Tickers.RemoveByInstance( _script );


		}

		void HandleScriptRemoved()
		{
			if( _script is null ) return;

			_script.OnRemoved -= HandleScriptRemoved;

			_script.Dispose();
			
			_script = null;
		}

		public void Tick()
		{
			State.StatusText = _script != null ? _script.StatusText : "None";
		}


		Dictionary<string, string> BuildVars( ScriptDef scriptDef, Dictionary<string, string> internalVars )
		{
			// start wit externally defined (global) internal vars
			var res = new Dictionary<string, string>( internalVars );

			// add the local variables from appdef
			foreach( var kv in scriptDef.LocalVarsToSet )
			{
				res[kv.Key] = kv.Value;
			}

			return res;
		}

	}
}

