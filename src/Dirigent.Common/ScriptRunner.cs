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

		Task? _runTask;
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
			if( _script == null )
				return new ScriptState();

			lock( _script )
			{
				return new ScriptState(
					_status,
					(_script as IScript).StatusText,
					(_script as IScript).StatusData
				);
			}
		}

		public void Start( string scriptName, string? sourceCode, byte[]? args, string title )
		{
			// one runner can run max one script at a time
			if( _script is not null ) // already started?
				throw new Exception( $"Script {title} [{ScriptInstance}] already started." );

			_status = EScriptStatus.Starting;
			SendStatus( new ScriptState(_status) );

			var script = _scriptFactory.Create<Script>( ScriptInstance, title, scriptName, null, sourceCode, args, new SynchronousIDirig( _ctrl, _syncOps ) );

			Start( script, args );
		}

		

		/// <summary>
		/// Starts the controller part
		/// </summary>
		/// <param name="script">Freshly created, no yet Init-ed script</param>
		public void Start( Script script, byte[]? args )
		{
			if( _script is not null ) // already started?
				throw new Exception( $"Script {ScriptInstance} already started." );

			script.Instance = ScriptInstance;
			_script = script;

			log.Debug( $"Starting script \"{_script.Title}\"{_script.Origin} [{_script.Instance}]" );

			_status = EScriptStatus.Running;
			SendStatus( new ScriptState(_status) );

			// run the script's Init+Run asynchronously
			_runCTS = new CancellationTokenSource();
			_runTask = Task.Run( async () => await ScriptLifeCycle( _runCTS.Token ) );
		}

		// cancel the script execution
		public void Stop()
		{
			if( _script is null ) return;

			log.Debug( $"Cancelling script \"{_script.Title}\"{_script.Origin} [{_script.Instance}]" );

			
			// note: the script can be still running there, possibly overwriting the status when it finished etc, needs locking
			var state = new ScriptState();
			lock( _script )
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

		void Remove()
		{
			if( _script is null ) return;
			
			_script.Dispose();
			
			_script = null;

			// we keep the last state (here either Finished, Failed, or Cancelled)
			//State.Status = EScriptStatus.Unknown;
		}

		async Task ScriptLifeCycle( CancellationToken ct )
		{
			// note: we wait for termination of this task in Tick(), then we call Done() from Tick

			if( _script == null ) return;
			
			try
			{
				//await _script.CallInit();
				var result = await _script.CallRun( ct );

				var state = new ScriptState();
				lock( _script )
				{
					_status = EScriptStatus.Finished;
					state.Status = _status;
					state.Data = result;
				}
				await SendStatusAsync( state );
			}
			catch( TaskCanceledException )
			{
				var state = new ScriptState();
				lock( _script )
				{
					_status = EScriptStatus.Cancelled;
					state.Status = _status;
				}
				await SendStatusAsync( state );
			}
			catch( Exception ex )
			{
				var state = new ScriptState();
				lock( _script )
				{
					_status = EScriptStatus.Failed;
					state.Status = _status;
					state.Data = Tools.Serialize( new ScriptException( ex ) );
				}
				await SendStatusAsync( state );
			}

		}

		public void SendStatus( ScriptState state )
		{
			if( _script == null ) return;
			_ctrl.Send( new Net.ScriptStateMessage(
				ScriptInstance,
				state
			));
		}

		public Task SendStatusAsync( ScriptState state )
		{
			if( _script == null ) return Task.CompletedTask;
			return _script.Dirig.SendAsync( new Net.ScriptStateMessage(
				ScriptInstance,
				state
			));
		}
		

		ScriptState _lastSentState = new();
		
		public void Tick()
		{
			if( _script != null )
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

			if( _script != null )
			{
				// check for Run finished in order to dispose the instance
				if( _runTask != null )
				{
					if( _runTask.IsCanceled )
					{
						Remove();
					}
					else
					if( _runTask.IsCompleted )
					{
						Remove();
					}
				}
			}
		}
	}
}

