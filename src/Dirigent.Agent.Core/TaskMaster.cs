using System;
using System.Collections.Generic;

namespace Dirigent
{
	/// <summary>
	/// Represetns an instance of an active distributed task in the system.
	/// This is its part running on the master.
	/// </summary>
	/// <remarks>
	/// Tasks are used to perform operations on multiple agents at once.
	/// Same task instance can be running on multiple agents.
	/// Task logic consists of controller part and worker part.
	/// Controller part is running on master while the worker part is running on the clients (all, some, none).
	/// Controller part commands other clients, keeps track of the task execution on workers and checks for task completion.
	/// Workers communicate with their controller via Request/Response messages (for example thay provides task state update for its part of the job).
	/// Controller aggregates worker states, determines whole task status, removes the task instance when done.
	/// </remarks>
	public class DTaskMaster : Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public Guid Guid { get; private set; }

		public string Id = String.Empty;

		public DTaskState State = new();

		private Script? _controllerScript;


		private Master _master;

		public DTaskMaster( Master master )
		{
			Guid = Guid.NewGuid();
			_master = master;
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if (!disposing) return;

			// dispose managed resources
			_controllerScript?.Dispose();
		}

		static Script LoadFromDef( DTaskDef def, string? args, Master master )
		{
			// add the local variables from appdef
			var internalVars = new Dictionary<string, string>();
			foreach( var kv in def.LocalVarsToSet )
			{
				internalVars[kv.Key] = kv.Value;
			}

			var scriptPath = Tools.ExpandEnvAndInternalVars( def.FileName, internalVars );
			scriptPath = PathUtils.BuildAbsolutePath( def.FileName, master.RootForRelativePaths );
													
			log.Debug( $"Launching script {def.Id} with args '{args}' (file: {scriptPath})" );

			var script = ScriptCreator.CreateFromFile( def.Id, scriptPath, args, master );

			return script;
		}
		
		
		// from TaskDef
		public void Start( DTaskDef def, string? args )
		{
			var script = LoadFromDef( def, args, _master );
			Start( def.Id, script, args );
		}
		

		/// <summary>
		/// Starts the controller part
		/// </summary>
		/// <param name="script">Freshly created, no yet Init-ed script</param>
		public void Start( string id, Script script, string? args )
		{
			Id = id;

			_controllerScript = script;
			_controllerScript.OnRemoved += HandleScriptRemoved;	 // called on removal from from Tickers collection

			_master.Tickers.Install( _controllerScript );

			_controllerScript.Init();
		}

		// Kills the controller part as well as anything still running on the clients
		public void Kill()
		{
			if( _controllerScript is null ) // not running anumore?
				return;

			_master.Tickers.RemoveByInstance( _controllerScript ); // this calls script.OnRemoved => HandleScriptRemoved

			// tell clients to clean up after this task instance
			_master.Send( new Net.KillTaskWorkersMessage( string.Empty, Guid ) );
		}

		// called when the controller script is removed from the Tickers collection
		void HandleScriptRemoved()
		{
			if( _controllerScript is null ) return;

			_controllerScript.OnRemoved -= HandleScriptRemoved;
			
			_controllerScript.Dispose();
			
			_controllerScript = null;
		}

		public void Tick()
		{
			State.StatusText = _controllerScript != null ? _controllerScript.StatusText : "None";
		}
	}
}

