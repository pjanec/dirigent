using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using System.Threading;

namespace Dirigent
{
	//[MessagePack.MessagePackObject]
	public struct KillAllArgs
	{
		//[MessagePack.Key( 1 )]
		public string MachineId; // where to kill the apps; null or empty means everywhere
	}

	public enum EShutdownMode
	{
		PowerOff = 0,
		Reboot = 1
	}

	//[MessagePack.MessagePackObject]
	public struct ShutdownArgs
	{
		//[MessagePack.Key( 1 )]
		public EShutdownMode Mode;
	}

	//[MessagePack.MessagePackObject]
	public struct TerminateArgs
	{
		//[MessagePack.Key( 1 )]
		public bool KillApps;  // kill all local apps before terminating
	}

	public enum EDownloadMode
	{
		Manual = 0,  // shows a dialog offering to restart the dirigent once the dirigent binaries have been manually overwritten
	}

	//[MessagePack.MessagePackObject]
	public struct ReinstallArgs
	{
		//[MessagePack.Key( 1 )]
		public EDownloadMode DownloadMode;

		//[MessagePack.Key( 2 )]
		public string Url;
	}


	//[MessagePack.MessagePackObject]
	public struct ReloadSharedConfigArgs
	{
		//[MessagePack.Key( 1 )]
		public bool KillApps;  // kill all local apps before reloading
	}

	public enum EAppDefType
	{
		/// <summary>
		/// the appDef applied when recently starting the app
		/// </summary>
		Effective,

		/// <summary>
		/// The appDef to be applied
		/// </summary>
		Upcoming,
	}


	/// <summary>
	/// Provides the current state of apps and plans.
	/// Allows to send dirigent commands.
	/// </summary>
	public interface IDirig
	{
		/// <summary> ident of the network client used as RequestorId </summary>
		string Name { get; } 

		/// <summary>
		/// Send is guaranteed to be thread/task safe, is always executed immediately, non-blocking
		/// </summary>
		/// <param name="msg"></param>
		void Send( Net.Message msg ) {}

		/// <summary>
		/// Returns the current state of an client as reported by the client at regular intervals
		/// </summary>
		/// <param name="Id">name of the client (machine name for agents, stringized GUID for GUIs)</param>
		/// <returns>null if no state for such client is known (client never connected)</returns>
		ClientState? GetClientState( string Id ) { return null; }
		IEnumerable<KeyValuePair<string, ClientState>> GetAllClientStates() { return new List<KeyValuePair<string, ClientState>>(); }

		/// <summary>
		/// Returns the current execution state of an application as provided by apps' respective agent
		/// </summary>
		/// <param name="Id"></param>
		/// <returns>null if no state for such application not known</returns>
		AppState? GetAppState( AppIdTuple Id ) { return null; }
		IEnumerable<KeyValuePair<AppIdTuple, AppState>> GetAllAppStates() { return new List<KeyValuePair<AppIdTuple, AppState>>(); }

		/// <summary>
		/// Gets the effective AppDef applied when last time starting the application.
		/// </summary>
		/// <param name="Id"></param>
		/// <returns></returns>
		AppDef? GetAppDef( AppIdTuple Id ) { return null; }
		IEnumerable<KeyValuePair<AppIdTuple, AppDef>> GetAllAppDefs() { return new List<KeyValuePair<AppIdTuple, AppDef>>(); }

		/// <summary>
		/// Gets the current plan execution state
		/// </summary>
		/// <param name="Id"></param>
		/// <returns></returns>
		PlanState? GetPlanState( string Id ) { return null; }
		IEnumerable<KeyValuePair<string, PlanState>> GetAllPlanStates() { return new List<KeyValuePair<string, PlanState>>(); }

		PlanDef? GetPlanDef( string Id ) { return null; }
		IEnumerable<PlanDef> GetAllPlanDefs() { return new List<PlanDef>(); }


		/// <summary>
		/// Gets the current script execution state
		/// </summary>
		/// <param name="Id"></param>
		/// <returns></returns>
		ScriptState? GetScriptState( Guid Id ) { return null; }
		IEnumerable<KeyValuePair<Guid, ScriptState>> GetAllScriptStates() { return new List<KeyValuePair<Guid, ScriptState>>(); }

		ScriptDef? GetScriptDef( Guid Id ) { return null; }
		IEnumerable<ScriptDef> GetAllScriptDefs() { return new List<ScriptDef>(); }

		VfsNodeDef? GetVfsNodeDef( Guid guid ) { return null; }
		IEnumerable<VfsNodeDef> GetAllVfsNodeDefs() { return new List<VfsNodeDef>(); }

		Task<TResult?> RunScriptAsync<TArgs, TResult>( string clientId, string scriptName, string? sourceCode, TArgs? args, string title, out Guid scriptInstance );
		Task<VfsNodeDef?> ResolveAsync( VfsNodeDef nodeDef, bool forceUNC, bool includeContent );

	}


	//public interface IDirigAsync
	//{
	//	/// <summary> ident of the network client used as RequestorId </summary>
	//	string Name { get; } 

	//	Task SendAsync( Net.Message msg ) => Task.CompletedTask;

	//	/// <summary>
	//	/// Returns the current state of an client as reported by the client at regular intervals
	//	/// </summary>
	//	/// <param name="Id">name of the client (machine name for agents, stringized GUID for GUIs)</param>
	//	/// <returns>null if no state for such client is known (client never connected)</returns>
	//	Task<ClientState?> GetClientStateAsync( string Id ) => Task.FromResult<ClientState?>(null);
	//	Task<IEnumerable<KeyValuePair<string, ClientState>>> GetAllClientStates() => Task.FromResult<IEnumerable<KeyValuePair<string, ClientState>>>(new List<KeyValuePair<string, ClientState>>());

	//	/// <summary>
	//	/// Returns the current execution state of an application as provided by apps' respective agent
	//	/// </summary>
	//	/// <param name="Id"></param>
	//	/// <returns>null if no state for such application not known</returns>
	//	Task<AppState?> GetAppStateAsync( AppIdTuple Id ) { return Task.FromResult<AppState?>(null); }
	//	Task<IEnumerable<KeyValuePair<AppIdTuple, AppState>>> GetAllAppStatesAsync() => Task.FromResult<IEnumerable<KeyValuePair<AppIdTuple, AppState>>>( new List<KeyValuePair<AppIdTuple, AppState>>());

	//	/// <summary>
	//	/// Gets the effective AppDef applied when last time starting the application.
	//	/// </summary>
	//	/// <param name="Id"></param>
	//	/// <returns></returns>
	//	Task<AppDef?> GetAppDefAsync( AppIdTuple Id ) { return null; }
	//	Task<IEnumerable<KeyValuePair<AppIdTuple, AppDef>>> GetAllAppDefsAsync() => Task.FromResult<IEnumerable<KeyValuePair<AppIdTuple, AppDef>>>(new List<KeyValuePair<AppIdTuple, AppDef>>());

	//	/// <summary>
	//	/// Gets the current plan execution state
	//	/// </summary>
	//	/// <param name="Id"></param>
	//	/// <returns></returns>
	//	Task<PlanState?> GetPlanState( string Id ) { return null; }
	//	Task<IEnumerable<KeyValuePair<string, PlanState>>> GetAllPlanStatesAsync() => Task.FromResult<IEnumerable<KeyValuePair<string, PlanState>>>( new List<KeyValuePair<string, PlanState>>() );

	//	Task<PlanDef?> GetPlanDef( string Id ) => Task.FromResult<PlanDef?>( null );
	//	Task<IEnumerable<PlanDef>> GetAllPlanDefsAsync() => Task.FromResult<IEnumerable<PlanDef>>( new List<PlanDef>() );


	//	/// <summary>
	//	/// Gets the current script execution state
	//	/// </summary>
	//	/// <param name="Id"></param>
	//	/// <returns></returns>
	//	Task<ScriptState?> GetScriptStateAsync( Guid Id ) => Task.FromResult<ScriptState?>( null );
	//	Task<IEnumerable<KeyValuePair<Guid, ScriptState>>> GetAllScriptStatesAsync() => Task.FromResult<IEnumerable<KeyValuePair<Guid, ScriptState>>>( new List<KeyValuePair<Guid, ScriptState>>() );

	//	Task<ScriptDef?> GetScriptDefAsync( Guid Id ) => Task.FromResult<ScriptDef?>( null );
	//	Task<IEnumerable<ScriptDef>> GetAllScriptDefsAsync() => Task.FromResult<IEnumerable<ScriptDef>>( new List<ScriptDef>() );

	//	Task<VfsNodeDef?> GetVfsNodeDefAsync( Guid guid ) => Task.FromResult<VfsNodeDef?>( null );
	//	Task<IEnumerable<VfsNodeDef>> GetAllVfsNodeDefsAsync() => Task.FromResult<IEnumerable<VfsNodeDef>>( new List<VfsNodeDef>() );

	//	//public MachineDef? GetMachineDef( string Id ) { return null; }

	//	/// <summary>
	//	/// Runs a script on given machine and waits for the result.
	//	/// 
	//	/// </summary>
	//	/// <typeparam name="TArgs"></typeparam>
	//	/// <typeparam name="TResult"></typeparam>
	//	/// <param name="scriptName"></param>
	//	/// <param name="args"></param>
	//	/// <returns></returns>
	//	Task<TResult> RunScriptWaitAsync<TArgs, TResult>( string scriptName, string machineId, TArgs args ) => throw new NotImplementedException();
	//}

	public interface IDirigAsync
	{
		string Name { get; } 
		Task SendAsync( Net.Message msg );
		Task<ClientState?> GetClientStateAsync( string Id );
		Task<IEnumerable<KeyValuePair<string, ClientState>>> GetAllClientStates();
		Task<AppState?> GetAppStateAsync( AppIdTuple Id );
		Task<IEnumerable<KeyValuePair<AppIdTuple, AppState>>> GetAllAppStatesAsync();
		Task<AppDef?> GetAppDefAsync( AppIdTuple Id );
		Task<IEnumerable<KeyValuePair<AppIdTuple, AppDef>>> GetAllAppDefsAsync();
		Task<PlanState?> GetPlanStateAsync( string Id );
		Task<IEnumerable<KeyValuePair<string, PlanState>>> GetAllPlanStatesAsync();
		Task<PlanDef?> GetPlanDefAsync( string Id );
		Task<IEnumerable<PlanDef>> GetAllPlanDefsAsync();
		Task<ScriptState?> GetScriptStateAsync( Guid Id );
		Task<IEnumerable<KeyValuePair<Guid, ScriptState>>> GetAllScriptStatesAsync();
		Task<ScriptDef?> GetScriptDefAsync( Guid Id );
		Task<IEnumerable<ScriptDef>> GetAllScriptDefsAsync();
		Task<VfsNodeDef?> GetVfsNodeDefAsync( Guid guid );
		Task<IEnumerable<VfsNodeDef>> GetAllVfsNodeDefsAsync();
		Task<TResult?> RunScriptAsync<TArgs, TResult>( string clientId, string scriptName, string? sourceCode, TArgs? args, string title, out Guid scriptInstance );
		Task<VfsNodeDef?> ResolveAsync( VfsNodeDef nodeDef, bool forceUNC, bool includeContent );
	}
}
