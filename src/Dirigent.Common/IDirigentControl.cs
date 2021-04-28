using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Runtime.Serialization;

namespace Dirigent.Common
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
		public bool KillApps;  // kill all local apps before terminating
	}


	/// <summary>
	/// Provides the current state of apps and plans.
	/// Allows to send dirigent commands.
	/// </summary>
	public interface IDirig
	{
		AppState? GetAppState( AppIdTuple Id ) { return null; }
		IEnumerable<KeyValuePair<AppIdTuple, AppState>> GetAllAppStates() { return new List<KeyValuePair<AppIdTuple, AppState>>(); }

		AppDef? GetAppDef( AppIdTuple Id ) { return null; }
		IEnumerable<KeyValuePair<AppIdTuple, AppDef>> GetAllAppDefs() { return new List<KeyValuePair<AppIdTuple, AppDef>>(); }

		PlanState? GetPlanState( string Id ) { return null; }
		IEnumerable<KeyValuePair<string, PlanState>> GetAllPlanStates() { return new List<KeyValuePair<string, PlanState>>(); }

		PlanDef? GetPlanDef( string Id ) { return null; }
		IEnumerable<PlanDef> GetAllPlanDefs() { return new List<PlanDef>(); }

		void Send( Net.Message msg ) {}
	}

}
