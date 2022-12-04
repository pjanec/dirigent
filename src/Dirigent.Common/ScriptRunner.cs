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

		public ScriptState State = new();

		private Script? _script;

		public Guid ScriptInstance { get; private set;}

		private IDirig _ctrl;

		Task? _runTask;
		CancellationTokenSource? _runCTS;

		ScriptFactory _scriptFactory;

		SynchronousOpProcessor _syncOps;

					
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

		public void Start( string scriptName, string? sourceCode, byte[]? args, string title )
		{
			// one runner can run max one script at a time
			if( _script is not null ) // already started?
				throw new Exception( $"Script {title} [{ScriptInstance}] already started." );

			State.Status = EScriptStatus.Starting;
			SendStatus();

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

			State.Status = EScriptStatus.Running;
			SendStatus();

			// run the script's Init+Run asynchronously
			_runCTS = new CancellationTokenSource();
			_runTask = Task.Run( async () => await ScriptLifeCycle( _runCTS.Token ) );
		}

		// cancel the script execution
		public void Stop()
		{
			if( _script is null ) return;

			State.Status = EScriptStatus.Cancelling;
			SendStatus();

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
				State.Status = EScriptStatus.Finished;
				State.Data = Tools.ProtoSerialize( result );
				await SendStatusAsync();
			}
			catch( TaskCanceledException )
			{
				State.Status = EScriptStatus.Cancelled;
				//SendStatus();
				await SendStatusAsync();
			}
			catch( Exception ex )
			{
				State.Status = EScriptStatus.Failed;
				State.Data = Tools.ProtoSerialize( new ScriptException( ex ) );
				await SendStatusAsync();
			}

		}

		public void SendStatus()
		{
			if( _script == null ) return;
			_ctrl.Send( new Net.ScriptStateMessage(
				ScriptInstance,
				new ScriptState( State.Status, State.Text, State.Data )
			));
		}

		public Task SendStatusAsync()
		{
			if( _script == null ) return Task.CompletedTask;
			return _script.Dirig.SendAsync( new Net.ScriptStateMessage(
				ScriptInstance,
				new ScriptState( State.Status, State.Text, State.Data )
			));
		}
		

		ScriptState _lastSentState = new();
		
		public void Tick()
		{
			if( _script != null )
			{
				// get status from script and send if changed
				var currStatusText = (_script as IScript).StatusText;	// FIXME: possible race condition, script is async, needs locking!
				var currStatusData = (_script as IScript).StatusData;	// FIXME: possible race condition, script is async, needs locking!

				if (currStatusText != _lastSentState.Text
						||
				    currStatusData != _lastSentState.Data	// reference equality should be enough as serializer always produces new instance
				)
				{
					State.Text = currStatusText;
					State.Data = currStatusData;
					
					SendStatus();
					
					_lastSentState.Status = State.Status;
					_lastSentState.Text = State.Text;
					_lastSentState.Data = State.Data;
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

