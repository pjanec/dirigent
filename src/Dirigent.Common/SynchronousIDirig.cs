using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dirigent
{
	// Awaitable synchronous executor of IDirig calls (blocks until the operation is executed during Dirigent's Tick)
	// To be used form async context.
	public class SynchronousIDirig : IDirigAsync
	{
		IDirig _ctrl;
		SynchronousOpProcessor _syncOps;

		public SynchronousIDirig( IDirig ctrl, SynchronousOpProcessor syncOpProc )
		{
			_ctrl = ctrl;
			_syncOps = syncOpProc;
		}

		/// <summary>
		/// True is we want to dispatch the calls to dirigent's main thread tick
		/// </summary>
		public bool ShouldWaitForSync { get; set; } = true;

		protected async Task<T?> GuardedFunc<T>( Func<T> func )
		{
			if( ShouldWaitForSync ) 
			{
				var op = _syncOps.AddSynchronousOp( () => { return func();} );
				await op.WaitAsync();
				if (op.Exception != null)
				{
					//HttpContext.Response.StatusCode = httpStatusCode;
					//HttpContext.Response.StatusDescription = op.Exception.Message;
					//return default(T);
					throw op.Exception;
				}
				return (T?) op.Result;
			}
			else // call synchronously, no waiting for dirigent's Tick (assuming we are already caling this from Tick)
			{
				return func();
			}
		}

		protected async Task GuardedAct( Action act )
		{
			if( ShouldWaitForSync ) 
			{
				var op = _syncOps.AddSynchronousOp( act );
				await op.WaitAsync();
				if (op.Exception != null)
				{
					//HttpContext.Response.StatusCode = httpStatusCode;
					//HttpContext.Response.StatusDescription = op.Exception.Message;
					//return;
					throw op.Exception;
				}
			}
			else // call synchronously, no waiting for dirigent's Tick (assuming we are already caling this from Tick)
			{
				act();
			}
		}

#pragma warning disable CS8603 // Possible null reference return.
		public string Name => _ctrl.Name;
		public       Task SendAsync( Net.Message msg ) { _ctrl.Send( msg ); return Task.CompletedTask; }
		public async Task<ClientState?> GetClientStateAsync( string Id ) => await GuardedFunc( () => _ctrl.GetClientState(Id) );
		public async Task<IEnumerable<KeyValuePair<string, ClientState>>> GetAllClientStatesAsync() => await GuardedFunc( () => _ctrl.GetAllClientStates().ToList() );
		public async Task<AppState?> GetAppStateAsync( AppIdTuple Id ) => await GuardedFunc( () => _ctrl.GetAppState(Id) );
		public async Task<IEnumerable<KeyValuePair<AppIdTuple, AppState>>> GetAllAppStatesAsync() => await GuardedFunc( () => _ctrl.GetAllAppStates().ToList() );
		public async Task<AppDef?> GetAppDeAsyncf( AppIdTuple Id ) => await GuardedFunc( () => _ctrl.GetAppDef(Id) );
		public async Task<IEnumerable<KeyValuePair<AppIdTuple, AppDef>>> GetAllAppDefsAsync() => await GuardedFunc( () => _ctrl.GetAllAppDefs().ToList() );
		public async Task<PlanState?> GetPlanStateAsync( string Id ) => await GuardedFunc( () => _ctrl.GetPlanState( Id ) );
		public async Task<IEnumerable<KeyValuePair<string, PlanState>>?> GetAllPlanStates() => await GuardedFunc( () => _ctrl.GetAllPlanStates().ToList() );
		public async Task<PlanDef?> GetPlanDefAsync( string Id ) => await GuardedFunc( () => _ctrl.GetPlanDef( Id ) );
		public async Task<IEnumerable<PlanDef>> GetAllPlanDefsAsync() => await GuardedFunc( () => _ctrl.GetAllPlanDefs().ToList() );
		public async Task<ScriptState?> GetScriptStateAsync( Guid Id ) => await GuardedFunc( () => _ctrl.GetScriptState( Id ) );
		public async Task<IEnumerable<KeyValuePair<Guid, ScriptState>>> GetAllScriptStatesAsync() => await GuardedFunc( () => _ctrl.GetAllScriptStates().ToList() );
		public async Task<ScriptDef?> GetScriptDefAsync( Guid Id ) => await GuardedFunc( () => _ctrl.GetScriptDef( Id ) );
		public async Task<IEnumerable<ScriptDef>> GetAllScriptDefsAsync() => await GuardedFunc( () => _ctrl.GetAllScriptDefs().ToList() );
		public async Task<VfsNodeDef?> GetFileDefAsync( Guid guid ) => await GuardedFunc( () => _ctrl.GetVfsNodeDef( guid ) );
		public async Task<IEnumerable<VfsNodeDef>> GetAllVfsNodeDefsAsync() => await GuardedFunc( () => _ctrl.GetAllVfsNodeDefs().ToList() );
		public async Task<IEnumerable<KeyValuePair<string, ClientState>>> GetAllClientStates() => await GuardedFunc( () => _ctrl.GetAllClientStates().ToList() );
		public async Task<AppDef?> GetAppDefAsync( AppIdTuple Id ) => await GuardedFunc( () => _ctrl.GetAppDef( Id ) );
		public async Task<PlanState?> GetPlanState( string Id ) => await GuardedFunc( () => _ctrl.GetPlanState( Id ) );
		public async Task<IEnumerable<KeyValuePair<string, PlanState>>> GetAllPlanStatesAsync() => await GuardedFunc( () => _ctrl.GetAllPlanStates().ToList() );
		public async Task<PlanDef?> GetPlanDef( string Id ) => await GuardedFunc( () => _ctrl.GetPlanDef( Id ) );
		public async Task<VfsNodeDef?> GetVfsNodeDefAsync( Guid guid ) => await GuardedFunc( () => _ctrl.GetVfsNodeDef( guid ) );
		public		 Task<TResult?> RunScriptAsync<TArgs, TResult>( string clientId, string scriptName, string? sourceCode, TArgs? args, string title, out Guid scriptInstance )
							=> _ctrl.RunScriptAsync<TArgs, TResult>( clientId, scriptName, sourceCode, args, title, out scriptInstance );
		public		 Task<VfsNodeDef?> ExpandPathsAsync( VfsNodeDef nodeDef, bool includeContent ) => _ctrl.ExpandPathsAsync( nodeDef, includeContent );
		public		 Task PerspectivizePathAsync( VfsNodeDef vfsNode, EPathType to ) => _ctrl.PerspectivizePathAsync( vfsNode, to );

#pragma warning restore CS8603 // Possible null reference return.
	}
}
