using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Runtime.Serialization;

namespace Dirigent
{
	[ProtoBuf.ProtoContract]
	[DataContract]
	public struct KillAllArgs
	{
		[ProtoBuf.ProtoMember( 1 )]
		public string MachineId; // where to kill the apps; null or empty means everywhere
	}

	public enum EShutdownMode
	{
		PowerOff = 0,
		Reboot = 1
	}

	[ProtoBuf.ProtoContract]
	[DataContract]
	public struct ShutdownArgs
	{
		[ProtoBuf.ProtoMember( 1 )]
		public EShutdownMode Mode;
	}

	[ProtoBuf.ProtoContract]
	[DataContract]
	public struct TerminateArgs
	{
		[ProtoBuf.ProtoMember( 1 )]
		public bool KillApps;  // kill all local apps before terminating

		[ProtoBuf.ProtoMember( 2 )]
		public string MachineId; // where to kill the apps; null or empty means everywhere
	}

	public enum EDownloadMode
	{
		Manual = 0,  // shows a dialog offering to restart the dirigent once the dirigent binaries have been manually overwritten
	}

	[ProtoBuf.ProtoContract]
	[DataContract]
	public struct ReinstallArgs
	{
		[ProtoBuf.ProtoMember( 1 )]
		public EDownloadMode DownloadMode;

		[ProtoBuf.ProtoMember( 2 )]
		public string Url;
	}


	[ProtoBuf.ProtoContract]
	[DataContract]
	public struct ReloadSharedConfigArgs
	{
		[ProtoBuf.ProtoMember( 1 )]
		public bool KillApps;  // kill all local apps before reloading
	}


	/// <summary>
	/// Provides the current state of apps and plans.
	/// Allows to send dirigent commands.
	/// </summary>
	public interface IDirig
	{
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

		enum EAppDefType
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
		ScriptState? GetScriptState( string Id ) { return null; }
		IEnumerable<KeyValuePair<string, ScriptState>> GetAllScriptStates() { return new List<KeyValuePair<string, ScriptState>>(); }

		ScriptDef? GetScriptDef( string Id ) { return null; }
		IEnumerable<ScriptDef> GetAllScriptDefs() { return new List<ScriptDef>(); }

		/// <summary> ident of the network client used as RequestorId </summary>
		string Name { get; } 

		void Send( Net.Message msg ) {}
	}

}
