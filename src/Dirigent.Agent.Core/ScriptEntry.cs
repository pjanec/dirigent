using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dirigent
{
	/// <summary>
	/// Script entry based of ScriptDef; loaded from SharedConfig; can be addressed via id.
	/// </summary>
	public class ScriptEntry : Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public ScriptDef Def;

		public string Id => this.Def.Id;

		public ScriptState State = new();

		// instance of the script
		private Script? _script;

		private Master _master;

		Guid TaskInstance = Guid.NewGuid();

		Task? _runTask;
		CancellationTokenSource? _runCTS;

		public ScriptEntry( ScriptDef def, Master master )
		{
			Def = def;
			_master = master;
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if( !disposing ) return;
			Remove();
		}

		public void Start( string? args )
		{
			if( _script is not null ) // already running?
				return;

			if( args is null )
				args = Def.Args;

			log.Debug( $"Launching script {Id} with args '{args}' (file: {Def.FileName})" );
			_script = ScriptFactory.Create( TaskInstance, Def.Id, Def.FileName, null, null, args, new SynchronousIDirig( _master, _master.SyncOps ) );

			// run the script's Init+Run asynchronously
			_runCTS = new CancellationTokenSource();
			_runTask = Task.Run( async () => await ScriptLifeCycle( _runCTS.Token ) );
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

