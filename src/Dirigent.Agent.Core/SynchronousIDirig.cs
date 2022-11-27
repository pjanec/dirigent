using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent
{
	// Awaitable synchronous executor of IDirig calls (blocks until the operation is executed during Dirigent's Tick)
	// To be used form async context.
	public class SynchronousIDirig : IDirig
	{
		IDirig _ctrl;
		SynchronousOpProcessor _syncOps;

		public SynchronousIDirig( IDirig ctrl, SynchronousOpProcessor syncOpProc )
		{
			_ctrl = ctrl;
			_syncOps = syncOpProc;
		}

		public bool ShouldWaitForSync { get; set; } = true;

		protected async Task<T?> GuardedFunc<T>( Func<T?> func )
		{
			if( ShouldWaitForSync ) 
			{
				var op = _syncOps.AddSynchronousOp( () => { return func();} );
				await op.WaitAsync();
				if (op.Exception != null)
				{
					//HttpContext.Response.StatusCode = httpStatusCode;
					//HttpContext.Response.StatusDescription = op.Exception.Message;
					return default(T);
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
					return;
				}
			}
			else // call synchronously, no waiting for dirigent's Tick (assuming we are already caling this from Tick)
			{
				act();
			}
		}

		public async Task<ClientState?> GetClientState( string Id ) => await GuardedFunc( () => _ctrl.GetClientState(Id) );
		public async Task<IEnumerable<KeyValuePair<string, ClientState>>?> GetAllClientStates() => await GuardedFunc( () => _ctrl.GetAllClientStates().ToList() );
		public async Task<AppState?> GetAppState( AppIdTuple Id ) => await GuardedFunc( () => _ctrl.GetAppState(Id) );
		public async Task<IEnumerable<KeyValuePair<AppIdTuple, AppState>>?> GetAllAppStates() => await GuardedFunc( () => _ctrl.GetAllAppStates().ToList() );
		public async Task<AppDef?> GetAppDef( AppIdTuple Id ) => await GuardedFunc( () => _ctrl.GetAppDef(Id) );
		public async Task<IEnumerable<KeyValuePair<AppIdTuple, AppDef>>?> GetAllAppDefs() => await GuardedFunc( () => _ctrl.GetAllAppDefs().ToList() );
		public async Task<PlanState?> GetPlanState( string Id ) => await GuardedFunc( () => _ctrl.GetPlanState( Id ) );
		public async Task<IEnumerable<KeyValuePair<string, PlanState>>?> GetAllPlanStates() => await GuardedFunc( () => _ctrl.GetAllPlanStates().ToList() );
		public async Task<PlanDef?> GetPlanDef( string Id ) => await GuardedFunc( () => _ctrl.GetPlanDef( Id ) );
		public async Task<IEnumerable<PlanDef>?> GetAllPlanDefs() => await GuardedFunc( () => _ctrl.GetAllPlanDefs().ToList() );
		public async Task<ScriptState?> GetScriptState( string Id ) => await GuardedFunc( () => _ctrl.GetScriptState( Id ) );
		public async Task<IEnumerable<KeyValuePair<string, ScriptState>>?> GetAllScriptStates() => await GuardedFunc( () => _ctrl.GetAllScriptStates().ToList() );
		public async Task<ScriptDef?> GetScriptDef( string Id ) => await GuardedFunc( () => _ctrl.GetScriptDef( Id ) );
		public async Task<IEnumerable<ScriptDef>?> GetAllScriptDefs() => await GuardedFunc( () => _ctrl.GetAllScriptDefs().ToList() );
		public async Task<FileDef?> GetFileDef( Guid guid ) => await GuardedFunc( () => _ctrl.GetFileDef( guid ) );
		public async Task<IEnumerable<FileDef>?> GetAllFileDefs() => await GuardedFunc( () => _ctrl.GetAllFileDefs().ToList() );
		public async Task<FilePackage?> GetFilePackage( Guid guid ) => await GuardedFunc( () => _ctrl.GetFilePackage( guid ) );
		public async Task<IEnumerable<FilePackage>?> GetAllFilePackages() => await GuardedFunc( () => _ctrl.GetAllFilePackages().ToList() );
		public string Name => _ctrl.Name;
		public async Task Send( Net.Message msg ) => await GuardedAct( () => _ctrl.Send( msg ) );
	}
}
