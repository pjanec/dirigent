using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;

namespace Dirigent
{
	/// <summary>
	/// Interface used for dynamic instantiation of scripts
	/// </summary>
	public interface IScript : IDisposable
	{
		//string StatusText { get; set; }
		//ScriptCtrl Ctrl { get; set; }
		//string Args { get; set; }
		//void Init();
		//void Done();
		void Tick();
		bool HasFinished { get; set; }
	}

	/// <summary>
	/// Script for executing tasks.
	/// Either built-in (in Dirigent), or dynamically copiled C#, or interpretted powershell
	/// </summary>
	public class Script : Disposable, IScript
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );
		
		public string Id { get; set; } = string.Empty;

		/// <summary>
		/// What task this script belongs to
		/// </summary>
		public Guid TaskInstance { get; set; }

		public bool HasFinished { get; set; }

		public string StatusText { get; set; } = string.Empty;

		public string Name { get; set; } = string.Empty;

		public string Args { get; set; } = string.Empty;

		protected ConcurrentQueue<Net.TaskRequestMessage> _requests = new ConcurrentQueue<Net.TaskRequestMessage>();

		// initialized during installation
		#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		public SynchronousIDirig Ctrl { get; set; }
		#pragma warning restore CS8618

		bool _doneCalled = false;

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if( !disposing ) return;

			if( !_doneCalled )
			{
				// I am afraid we can't wait for Done to finish here - this would block the Tick
				// so any synchronous call from within Done would block.
				// This means Done needs to be called in synchronous context only, from main thread...
				// And that means we should notmake api calls via synchronous ops as that would block.
				// We need to disable syncops when we get to Done, and call API synchronously from that time on.
				Ctrl.ShouldWaitForSync = false; // disable sync ops, call the api from this thread directly
				CallDone();
			}
		}

		public virtual void Tick()
		{
		}

		public void AddRequest( Net.TaskRequestMessage req )
		{
			_requests.Enqueue( req );
		}

		public async Task CallInit() => await Init();
		public async Task CallRun( CancellationToken ct ) => await Run(ct);
		public void CallDone() { _doneCalled=true;  Done(); }

		/// <summary>Called once when script gets instantiated, before first tick </summary>
		protected virtual Task Init()
		{
			return Task.CompletedTask;
		}

		/// <summary>
		/// Called before the script gets destroyed, either if Run() finished or is cancelled.
		/// </summary>
		/// <remarks>
		/// Called from main thread, with SyncOps disables (IDirigent api calls from script are called directly)
		/// Blocks the main thread, avoid doing anything time consuming here!
		/// </remarks>
		/// 
		protected virtual void Done()
		{
		}

		// to be overridden by the script
		protected async virtual Task Run( CancellationToken ct )
		{
			await WaitUntilCancelled( ct );
		}

		protected async Task WaitUntilCancelled( CancellationToken ct )
		{
			// if the script does not override this method,
			// we simply wait until the script is cancelled
			while( true )
			{
				await Task.Delay( 100, ct );
			}
		}


		protected async virtual Task OnRequest( string type, string args, CancellationToken ct  )
		{
			// if the script does not override this method,
			// we simply wait until the script is cancelled
			while( !ct.IsCancellationRequested )
			{
				await Task.Delay(100);
			}
		}

		protected void SendRequest( string type, string? args )
		{
			Ctrl.Send( new Net.TaskRequestMessage( TaskInstance, type, args ) ).Wait();
		}

		protected void SendResponse( Guid requestId, string type, string? args )
		{
			Ctrl.Send( new Net.TaskResponseMessage( TaskInstance, requestId, type, args ) ).Wait();
		}

		protected void StartApp( string id, string? planName, string? vars=null )
		{
			Ctrl.Send( new Net.StartAppMessage( string.Empty, new AppIdTuple(id), planName, flags:0, vars:Tools.ParseEnvVarList(vars) ) ).Wait();
		}

		protected void RestartApp( string id, string? vars=null )
		{
			Ctrl.Send( new Net.RestartAppMessage( string.Empty, new AppIdTuple(id), vars:Tools.ParseEnvVarList(vars) ) ).Wait();
		}

		protected void KillApp( string id )
		{
			Ctrl.Send( new Net.KillAppMessage( string.Empty, new AppIdTuple(id) ) ).Wait();
		}

		protected AppState? GetAppState( string id )
		{
			var task = Ctrl.GetAppState( new AppIdTuple( id ) );
			task.Wait();
			return task.Result;
		}

		// plans

		protected void StartPlan( string id, string? vars=null )
		{
			Ctrl.Send( new Net.StartPlanMessage( string.Empty, id, vars:Tools.ParseEnvVarList(vars) ) ).Wait();
		}

		protected void RestartPlan( string id, string? vars=null )
		{
			Ctrl.Send( new Net.RestartPlanMessage( string.Empty, id, vars:Tools.ParseEnvVarList(vars) ) ).Wait();
		}

		protected void KillPlan( string id )
		{
			Ctrl.Send( new Net.KillPlanMessage( string.Empty, id ) ).Wait();
		}

		protected PlanState? GetPlanState( string id )
		{
			var task = Ctrl.GetPlanState( id );
			task.Wait();
			return task.Result;
		}

		// clients

		protected ClientState? GetClientState( string id )
		{
			var task = Ctrl.GetClientState( id );
			task.Wait();
			return task.Result;
		}

		// scritps

		protected void StartScript( string idWithArgs )
		{
			(var id, var args) = Tools.ParseScriptIdArgs( idWithArgs );

			Ctrl.Send( new Net.StartScriptMessage( string.Empty, id, args ) ).Wait();
		}

		protected void KillScript( string id )
		{
			Ctrl.Send( new Net.KillScriptMessage( string.Empty, id ) ).Wait();
		}

	}

	//public class CoroScript : Script
	//{
	//	Coroutine? _run;
	//	Coroutine? _onRequest;

	//	// initialized during installation
	//	#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
	//	public IDirig Ctrl { get; set; }
	//	#pragma warning restore CS8618

	//	bool tickCoroutine( ref Coroutine? coro )
	//	{
	//		if( coro != null )
	//		{
	//			coro.Tick();
	//			if( !coro.IsFinished )
	//			{
	//				return true;
	//			}
	//			coro.Dispose();
	//			coro = null;
	//		}
	//		return false;
	//	}


	//	protected override void Dispose( bool disposing )
	//	{
	//		base.Dispose( disposing );
	//		if( !disposing ) return;
	//		_run?.Dispose(); _run = null;
	//		_onRequest?.Dispose(); _onRequest = null;
	//	}

	//	public override void Tick()
	//	{
	//		base.Tick();

	//		if( _run == null )
	//		{
	//			_run = new Coroutine( Run() );
	//		}

	//		if( !tickCoroutine( ref _run ) )
	//			HasFinished = true;

	//		// process next waiting request if the previous is finished
	//		if( _onRequest == null )
	//		{
	//			if( _requests.Count > 0 )
	//			{
	//				if( _requests.TryDequeue( out var req ) )
	//				{
	//					_onRequest = new Coroutine( OnRequest( req.RequestId, req.Type, req.Args ) );
	//				}
	//			}
	//		}

	//		tickCoroutine( ref _onRequest );
	//	}

	//	public virtual System.Collections.IEnumerable Run()
	//	{
	//		yield return null;
	//	}


	//	public virtual System.Collections.IEnumerable OnRequest( Guid id, string type, string? args )
	//	{
	//		yield return null;
	//	}

	//}
}
