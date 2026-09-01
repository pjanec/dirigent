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
		/// Gets a snapshot of current running script status (changes asynchronously as the sript runs).
		/// This does not include the result of the last run, just the current status.
		/// </summary>
		public ScriptState RunningState => GetStateLocked();

		
		/// <summary>
		/// Cached state, updated in Tick or when script finishes.
		/// Includes the result of the last run if the script is no longer running.
		/// </summary>
		public ScriptState CachedState => _lastSentState;

		private ScriptState _lastSentState = new();

		private Script? _script;

		public Guid ScriptInstance { get; private set;}

		private readonly IDirig _ctrl;

		Task? _runTask; // if not null, the script is still running
		CancellationTokenSource? _runCTS;

		readonly ScriptFactory _scriptFactory;

		readonly SynchronousOpProcessor _syncOps;

		string _scriptRootFolder;

					
		public ScriptRunner( IDirig master, Guid instance, ScriptFactory factory, SynchronousOpProcessor syncOps, string scriptRootFolder )
		{
			ScriptInstance = instance == Guid.Empty ? Guid.NewGuid() : instance;
			_ctrl = master;
			_scriptFactory = factory;
			_syncOps = syncOps;
			_scriptRootFolder = scriptRootFolder;
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
					(_script as IScript).StatusData,
					(_script as IScript).StatusProgress
				);
			}
		}

		public void Start( string scriptName, string? sourceCode, string? args, string title, string? requestorId )
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

		async Task ScriptLifeCycle( CancellationToken ct, string scriptName, string? sourceCode, string? args, string title, string? requestorId  )
		{
			// note: we wait for termination of this task in Tick(), then we call Done() from Tick
			try
			{
				log.Debug( $"Instantiating script \"{title}\" {scriptName} [{ScriptInstance}]" );
				
				_script = _scriptFactory.Create<Script>( ScriptInstance, title, scriptName, _scriptRootFolder, sourceCode, args, new SynchronousIDirig( _ctrl, _syncOps ), requestorId );

				ct.ThrowIfCancellationRequested();

				_script.Instance = ScriptInstance;
				_script.CancellationToken = ct;

				log.Debug( $"Running script \"{title}\" {scriptName} [{ScriptInstance}]" );

				_status = EScriptStatus.Running;
				SendStatus( new ScriptState(_status) );

				//await _script.CallInit();
				var result = await _script.CallRun();

				var state = new ScriptState();
				lock( _runTask! )
				{
					_status = EScriptStatus.Finished;
					state.Status = _status;
					state.Data = result;

					// whatever the script last said about its progress, having finished it is done -
					// so a progress indicator shows a full bar rather than freezing wherever it got to
					state.Progress = 1.0;
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

		/// <summary>
		/// Stops this runner from telling anybody anything else.
		/// </summary>
		/// <remarks>
		/// For a runner whose script instance has been handed to a replacement: the cancellation of
		/// the old one completes on its own thread, whenever the script gets round to noticing, and
		/// the Cancelled it would then publish carries the instance id that now belongs to the new
		/// script. Everything watching is keyed by that id, so the dead run's verdict would land on
		/// the live one - a script shown as cancelled while it is running.
		/// </remarks>
		public void Abandon()
		{
			_abandoned = true;
		}

		private volatile bool _abandoned;

		public void SendStatus( ScriptState state )
		{
			if( _abandoned ) return;

			_lastSentState = state.Clone();

			// note: the following should not block, must be thread safe
			_ctrl.Send( new Net.ScriptStateMessage(
				ScriptInstance,
				state
			));
		}

		ScriptState _lastStateSentFromTick = new();
		DateTime _lastStateSentAt = DateTime.Now;

		/// <summary>
		/// How long a running script may stay silent before it says the same thing again.
		/// </summary>
		/// <remarks>
		/// A state is otherwise only sent when it changes, so a script in a long silent phase looks
		/// exactly like one whose host has died - and nothing watching it can tell the difference.
		/// Repeating it now and then is what makes "not heard from in a while" mean something.
		/// </remarks>
		static readonly TimeSpan _heartbeatPeriod = TimeSpan.FromSeconds( 10 );

		public void Tick()
		{
			if( _runTask != null )
			{
				var state = GetStateLocked();

				// while the script is running, we send the Text and Data as set by the script
				if( state.Status == EScriptStatus.Running )
				{
					if( state != _lastStateSentFromTick
						|| DateTime.Now - _lastStateSentAt > _heartbeatPeriod )
					{
						SendStatus( state );

						_lastStateSentFromTick = state;
						_lastStateSentAt = DateTime.Now;
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

