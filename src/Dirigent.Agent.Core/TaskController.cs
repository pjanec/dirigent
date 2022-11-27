using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
	public class DTaskController : Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public Guid Guid { get; private set; }

		public string Id = String.Empty;

		public DTaskState State = new();

		private Script? _script; // controller part

		Guid TaskInstance = Guid.NewGuid();

		private Master _master;

		Task? _runTask;
		CancellationTokenSource? _runCTS;
					
		public DTaskController( Master master )
		{
			Guid = Guid.NewGuid();
			_master = master;
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if (!disposing) return;

			// dispose managed resources
			_script?.Dispose();
		}

		static Script LoadFromDef( Guid taskInstance, DTaskDef def, string? args, Master master )
		{
			// add the local variables from appdef
			var internalVars = new Dictionary<string, string>();
			foreach( var kv in def.LocalVarsToSet )
			{
				internalVars[kv.Key] = kv.Value;
			}

			//var scriptPath = Tools.ExpandEnvAndInternalVars( def.FileName, internalVars );
			//scriptPath = PathUtils.BuildAbsolutePath( def.FileName, master.RootForRelativePaths );
													
			//log.Debug( $"Launching script {def.Id} with args '{args}' (file: {scriptPath})" );

			var script = ScriptFactory.Create( taskInstance, def.Id, def.ScriptName, def.ScriptFolder, null, args, new SynchronousIDirig( master, master.SyncOps ) );
						
			return script;
		}
		
		
		// from TaskDef
		public void Start( DTaskDef def, string? args )
		{
			var script = LoadFromDef( TaskInstance, def, args, _master );
			Start( def.Id, script, args );
		}
		

		/// <summary>
		/// Starts the controller part
		/// </summary>
		/// <param name="script">Freshly created, no yet Init-ed script</param>
		public void Start( string id, Script script, string? args )
		{
			Id = id;

			_script = script;

			// run the script's Init+Run asynchronously
			_runCTS = new CancellationTokenSource();
			_runTask = Task.Run( async () => await ScriptLifeCycle( _runCTS.Token ) );
		}

		// Kills the controller part as well as anything still running on the clients
		public void Kill()
		{
			if( _script is null ) return;

			State.StatusText = "Cancelling";

			// cancel and wait for task to finish
			_runCTS?.Cancel();

			if( _runTask != null )
			{
				_runTask.Wait();

				if( _runTask.IsCanceled )
				{
					State.StatusText = "Cancelled";
				}
			}

			Remove();

			// tell clients to clean up after this task instance
			_master.Send( new Net.KillTaskWorkersMessage( string.Empty, Guid ) );
		}

		void Remove()
		{
			if( _script is null ) return;
			
			_script.Dispose();
			
			_script = null;
		}

		async Task ScriptLifeCycle( CancellationToken ct )
		{
			// note: we wait for termination of this task in Tick(), then we call Done() from Tick

			if( _script == null ) return;
			
			try
			{
				await _script.CallInit();
				await _script.CallRun( ct );
			}
			catch( TaskCanceledException )
			{
				// ignore...
			}

		}

		public void Tick()
		{
			if( _script != null )
			{
				_script.Tick();

				if( _script.HasFinished )
				{
					Remove();
				}
				else
				// check for Run finished in order to call Done
				if( _runTask != null )
				{
					if( _runTask.IsCompleted )
					{
						Remove();
					}
				}
			}

			State.StatusText = _script != null ? _script.StatusText : "None";
		}
	}
}

