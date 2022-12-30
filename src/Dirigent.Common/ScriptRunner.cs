using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Dirigent
{
	/// <summary>
	/// Start/Kill harness for an instance of a local script.
	/// Sends script state changes to the master (who then broadcasts them to the clients).
	/// One runner can run max one script instance at a time.
	/// </summary>
	public class ScriptRunner : Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		// Current status of the script. Set by the Runner (can't be not from script, script sets just status Text/Data)
		EScriptStatus _status;

		/// <summary>
		/// Gets a snapshot of script status
		/// </summary>
		public ScriptState State => GetStateLocked();

		private Script? _script;

		public Guid ScriptInstance { get; private set;}

		private readonly IDirig _ctrl;

		Task? _runTask; // if not null, the script is still running
		CancellationTokenSource? _runCTS;

		readonly ScriptFactory _scriptFactory;

		readonly SynchronousOpProcessor _syncOps;

					
		public ScriptRunner( IDirig master, Guid instance, ScriptFactory factory, SynchronousOpProcessor syncOps )
		{
			ScriptInstance = instance == Guid.Empty ? Guid.NewGuid() : instance;
			_ctrl = master;
			_scriptFactory = factory;
			_syncOps = syncOps;
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if (!disposing) return;

			// this intiates the cancellation if the script is running
			Stop();
			
			// dispose managed resources
			_script?.Dispose();
		}


		// this has to be locked as the status can change either from Tick or from async ScriptLifeCycle
		ScriptState GetStateLocked()
		{
			if( _runTask is null || _script is null )
				return new ScriptState();

			lock( _runTask )
			{
				return new ScriptState(
					_status,
					(_script as IScript).StatusText,
					(_script as IScript).StatusData
				);
			}
		}

		public void Start( string scriptName, string? sourceCode, byte[]? args, string title, string? requestorId )
		{
			// one runner can run max one script at a time
			if( _runTask is not null ) // already started?
				throw new Exception( $"Script {title} [{ScriptInstance}] already started." );

			_status = EScriptStatus.Starting;
			SendStatus( new ScriptState(_status) );

			// run the script's Init+Run asynchronously
			_runCTS = new CancellationTokenSource();
			_runTask = Task.Run( async () => await ScriptLifeCycle( _runCTS.Token, scriptName, sourceCode, args, title, requestorId ) );
		}

		

		// cancel the script execution
		public void Stop()
		{
			if( _runTask is null ) return;

			log.Debug( $"Cancelling script [{ScriptInstance}]" );

			
			// note: the script can be still running there, possibly overwriting the status when it finished etc, needs locking
			var state = new ScriptState();
			lock( _runTask )
			{
				_status = EScriptStatus.Cancelling;
				state.Status = _status;
			}
			SendStatus( state );

			// cancel and wait for task to finish
			_runCTS?.Cancel();

			// note: we can not wait here as it would block the main thread if the script was just awaiting a SyncOp
			// instead we just let it go and check the IsCancelled in tick()
			//	_runTask.Wait();
		}

		async Task ScriptLifeCycle( CancellationToken ct, string scriptName, string? sourceCode, byte[]? args, string title, string? requestorId  )
		{
			// note: we wait for termination of this task in Tick(), then we call Done() from Tick
			try
			{
				log.Debug( $"Instantiating script \"{title}\" {scriptName} [{ScriptInstance}]" );
				
				_script = _scriptFactory.Create<Script>( ScriptInstance, title, scriptName, null, sourceCode, args, new SynchronousIDirig( _ctrl, _syncOps ), requestorId );

				ct.ThrowIfCancellationRequested();

				_script.Instance = ScriptInstance;

				log.Debug( $"Running script \"{title}\" {scriptName} [{ScriptInstance}]" );

				_status = EScriptStatus.Running;
				SendStatus( new ScriptState(_status) );

				//await _script.CallInit();
				var result = await _script.CallRun( ct );

				var state = new ScriptState();
				lock( _runTask! )
				{
					_status = EScriptStatus.Finished;
					state.Status = _status;
					state.Data = result;
				}
				SendStatus( state );
			}
			catch( TaskCanceledException ) // thrown by one of the awaits if cancellation is detected
			{
				var state = new ScriptState();
				lock( _runTask! )
				{
					_status = EScriptStatus.Cancelled;
					state.Status = _status;
				}
				SendStatus( state );
			}
			catch( OperationCanceledException )	 // thrown by the script if it detected the cancellation
			{
				var state = new ScriptState();
				lock( _runTask! )
				{
					_status = EScriptStatus.Cancelled;
					state.Status = _status;
				}
				SendStatus( state );
			}
			catch( Exception ex )
			{
				var state = new ScriptState();
				lock( _runTask! )
				{
					_status = EScriptStatus.Failed;
					state.Status = _status;
					state.Data = Tools.Serialize( new SerializedException( ex ) );
				}
				SendStatus( state );
			}

		}

		public void SendStatus( ScriptState state )
		{
			// note: the following should not block, must be thread safe
			_ctrl.Send( new Net.ScriptStateMessage(
				ScriptInstance,
				state
			));
		}

		ScriptState _lastSentState = new();
		
		public void Tick()
		{
			if( _runTask != null )
			{
				var state = GetStateLocked();

				// while the script is running, we send the Text and Data as set by the script
				if( state.Status == EScriptStatus.Running )
				{
					if( state != _lastSentState )
					{
						SendStatus( state );

						_lastSentState = state;
					}
				}
			}

			if( _runTask != null )
			{
				if( _runTask.IsCanceled )
				{
					ClearTask();
				}
				else
				if( _runTask.IsCompleted )
				{
					ClearTask();
				}
			}
		}

		void ClearTask()
		{
			if( _script is not null )
			{
				_script.Dispose();
			
				_script = null;
			}
			
			_runTask = null;

			// we keep the last state (here either Finished, Failed, or Cancelled)
			//State.Status = EScriptStatus.Unknown;
		}

	}

}

