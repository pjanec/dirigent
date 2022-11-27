using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dirigent
{
	/// <summary>
	/// Represetns an instance of an active distributed task in the system.
	/// This is its part running on the agent (the worker).
	/// </summary>
	/// <remarks>
	/// Worker gets instantiated based on request from the controller.
	/// </remarks>
	public class DTaskWorker: Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public Guid Guid { get; private set; }

		public string Id = String.Empty; // for debug display purposes
		
		public Guid TaskInstance { get; private set; }

		public DTaskState State = new();

		private Script? _script; // worker part

		private Agent _agent;

		Task? _runTask;
		CancellationTokenSource? _runCTS;
					
		public DTaskWorker( Agent agent, Guid taskInstance, string taskId, string? args )
		{
			Guid = Guid.NewGuid();
			_agent = agent;
			TaskInstance = taskInstance;
			Id = taskId;
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if (!disposing) return;

			// dispose managed resources
			_script?.Dispose();
		}

		
		public void Start( string scriptCode, string? args, string? scriptOrigin )
		{
			var script = ScriptFactory.CreateFromString( TaskInstance, Id, scriptCode, args, new SynchronousIDirig( _agent, _agent.SyncOps ), scriptOrigin	 );
			Start( script, args );
		}
		

		/// <summary>
		/// Starts the controller part
		/// </summary>
		/// <param name="script">Freshly created, no yet Init-ed script</param>
		public void Start( Script script, string? args )
		{
			_script = script;

			// run the script's Init+Run asynchronously
			_runCTS = new CancellationTokenSource();
			_runTask = Task.Run( async () => await ScriptLifeCycle( _runCTS.Token ) );
		}

		// Kills the worker script
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

		void Remove()
		{
			if( _script is null ) return;

			// this calls Done() if not yet called
			_script.Dispose();
			
			_script = null;
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

