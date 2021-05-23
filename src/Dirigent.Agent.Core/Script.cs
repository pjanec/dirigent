namespace Dirigent
{
	/// <summary>
	/// Script entry based of ScriptDef; loaded from SharedConfig; can be addressed via id.
	/// </summary>
	public class Script
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public ScriptDef Def;

		public string Id => this.Def.Id;

		public ScriptState State = new();

		// instance of the script
		private UserScript? _script;


		private Master _master;

		public Script( ScriptDef def, Master master )
		{
			Def = def;
			_master = master;
		}

		public void Start( string? args )
		{
			if( _script is not null ) // already running?
				return;


			if( args is null )
				args = Def.Args;

			log.Debug( $"Launching script {Id} with args '{args}' (file: {Def.FileName})" );

			_script = UserScript.CreateFromFile( Def.Id, Def.FileName, args, _master );
			_script.OnRemoved += HandleScriptRemoved;

			_master.Tickers.Install( _script );
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
			_script = null;
		}

		public void Tick()
		{
			State.StatusText = _script != null ? _script.StatusText : "None";
		}



	}
}

