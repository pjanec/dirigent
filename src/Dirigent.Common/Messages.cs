using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Runtime.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace Dirigent.Net
{
	public static class TypeMapRegistry
	{
		static public Dictionary<uint, System.Type> TypeMap = new Dictionary<uint, Type>()
		{
			{ 100, typeof( Message ) },
			{ 101, typeof( RemoteOperationErrorMessage ) },
			{ 102, typeof( AppsStateMessage ) },
			{ 103, typeof( PlansStateMessage ) },
			{ 104, typeof( StartAppMessage ) },
			{ 105, typeof( KillAppMessage ) },
			{ 106, typeof( RestartAppMessage ) },
			{ 107, typeof( SetAppEnabledMessage ) },
			{ 108, typeof( SelectPlanMessage) },
			{ 109, typeof( StartPlanMessage ) },
			{ 110, typeof( StopPlanMessage ) },
			{ 111, typeof( KillPlanMessage ) },
			{ 112, typeof( RestartPlanMessage ) },
			//{ 113, typeof( CurrentPlanMessage ) },
			{ 114, typeof( PlanDefsMessage ) },
			{ 115, typeof( SetVarsMessage ) },
			{ 116, typeof( KillAllMessage ) },
			{ 117, typeof( ShutdownMessage ) },
			{ 118, typeof( ReinstallMessage ) },
			{ 119, typeof( TerminateMessage ) },
			{ 120, typeof( ReloadSharedConfigMessage ) },
			{ 121, typeof( ClientIdent ) },
			{ 122, typeof( AppDefsMessage ) },
			{ 123, typeof( CLIRequestMessage ) },
			{ 124, typeof( CLIResponseMessage ) },
			{ 125, typeof( ResetMessage ) },
			{ 126, typeof( ClientStateMessage ) },
			{ 127, typeof( StartScriptMessage ) },
			{ 128, typeof( KillScriptMessage ) },
			{ 129, typeof( ScriptDefsMessage ) },
			{ 130, typeof( ScriptStateMessage ) },
			{ 131, typeof( ApplyPlanMessage ) },
			{ 132, typeof( SetWindowStyleMessage ) },
			{ 133, typeof( VfsNodesMessage ) },
			{ 134, typeof( MachineDefsMessage ) },
			{ 135, typeof( StartTaskMessage ) },
			{ 136, typeof( KillTaskMessage ) },
			{ 137, typeof( TaskDefsMessage ) },
			{ 138, typeof( TaskStateMessage ) },
			{ 139, typeof( TaskRequestMessage ) },
			{ 140, typeof( TaskResponseMessage ) },
			{ 141, typeof( StartTaskWorkerMessage ) },
			{ 142, typeof( KillTaskWorkersMessage ) },
			// WARNING: add newly added messages also to the list below!!
		};
	}

	/// <summary>
	/// Base class for all messages.
	/// </summary>
	[ProtoBuf.ProtoContract]
	[ProtoBuf.ProtoInclude( 101, typeof( RemoteOperationErrorMessage ) )]
	[ProtoBuf.ProtoInclude( 102, typeof( AppsStateMessage ) )]
	[ProtoBuf.ProtoInclude( 103, typeof( PlansStateMessage ) )]
	[ProtoBuf.ProtoInclude( 104, typeof( StartAppMessage ) )]
	[ProtoBuf.ProtoInclude( 105, typeof( KillAppMessage ) )]
	[ProtoBuf.ProtoInclude( 106, typeof( RestartAppMessage ) )]
	[ProtoBuf.ProtoInclude( 107, typeof( SetAppEnabledMessage ) )]
    [ProtoBuf.ProtoInclude( 108, typeof(SelectPlanMessage))]
	[ProtoBuf.ProtoInclude( 109, typeof( StartPlanMessage ) )]
	[ProtoBuf.ProtoInclude( 110, typeof( StopPlanMessage ) )]
	[ProtoBuf.ProtoInclude( 111, typeof( KillPlanMessage ) )]
	[ProtoBuf.ProtoInclude( 112, typeof( RestartPlanMessage ) )]
  //[ProtoBuf.ProtoInclude( 113, typeof( CurrentPlanMessage ) )]
	[ProtoBuf.ProtoInclude( 114, typeof( PlanDefsMessage ) )]
	[ProtoBuf.ProtoInclude( 115, typeof( SetVarsMessage ) )]
	[ProtoBuf.ProtoInclude( 116, typeof( KillAllMessage ) )]
	[ProtoBuf.ProtoInclude( 117, typeof( ShutdownMessage ) )]
	[ProtoBuf.ProtoInclude( 118, typeof( ReinstallMessage ) )]
	[ProtoBuf.ProtoInclude( 119, typeof( TerminateMessage ) )]
	[ProtoBuf.ProtoInclude( 120, typeof( ReloadSharedConfigMessage ) )]
	[ProtoBuf.ProtoInclude( 121, typeof( ClientIdent ) )]
	[ProtoBuf.ProtoInclude( 122, typeof( AppDefsMessage ) )]
	[ProtoBuf.ProtoInclude( 123, typeof( CLIRequestMessage ) )]
	[ProtoBuf.ProtoInclude( 124, typeof( CLIResponseMessage ) )]
	[ProtoBuf.ProtoInclude( 125, typeof( ResetMessage ) )]
	[ProtoBuf.ProtoInclude( 126, typeof( ClientStateMessage ) )]
	[ProtoBuf.ProtoInclude( 127, typeof( StartScriptMessage ) )]
	[ProtoBuf.ProtoInclude( 128, typeof( KillScriptMessage ) )]
	[ProtoBuf.ProtoInclude( 129, typeof( ScriptDefsMessage ) )]
	[ProtoBuf.ProtoInclude( 130, typeof( ScriptStateMessage ) )]
	[ProtoBuf.ProtoInclude( 131, typeof( ApplyPlanMessage ) )]
	[ProtoBuf.ProtoInclude( 132, typeof( SetWindowStyleMessage ) )]
	[ProtoBuf.ProtoInclude( 133, typeof( VfsNodesMessage ) )]
	[ProtoBuf.ProtoInclude( 134, typeof( MachineDefsMessage ) )]
	[ProtoBuf.ProtoInclude( 135, typeof( StartTaskMessage ) )]
	[ProtoBuf.ProtoInclude( 136, typeof( KillTaskMessage ) )]
	[ProtoBuf.ProtoInclude( 137, typeof( TaskDefsMessage ) )]
	[ProtoBuf.ProtoInclude( 138, typeof( TaskStateMessage ) )]
	[ProtoBuf.ProtoInclude( 139, typeof( TaskRequestMessage ) )]
	[ProtoBuf.ProtoInclude( 140, typeof( TaskResponseMessage ) )]
	[ProtoBuf.ProtoInclude( 141, typeof( StartTaskWorkerMessage ) )]
	[ProtoBuf.ProtoInclude( 142, typeof( KillTaskWorkersMessage ) )]

	public class Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		public string Sender { get; set; } = string.Empty;  // machine name for agents, guid for GUIs, empty for master

		public static void RegisterProtobufTypeMaps()
		{
			//ProtoBuf.Meta.RuntimeTypeModel.Default.Add( typeof( ILaunchPlan ), true ).AddSubType( 50, typeof( LaunchPlan ) );
		}

		// do not dump it on console
		public virtual bool IsFrequent { get { return false; } }
	}

	/// <summary>
	/// Agent tells others there was an error processing some operation. Master resends this to GUIs.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class RemoteOperationErrorMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		public string Requestor = string.Empty;

		[ProtoBuf.ProtoMember( 2 )]
		public string Message = string.Empty; // Error description

		[ProtoBuf.ProtoMember( 3 )]
		public Dictionary<string, string>? Attributes; // additional attribute pairs (name, value)

		public RemoteOperationErrorMessage() {}
		public RemoteOperationErrorMessage( string requestor, string msg, Dictionary<string, string>? attribs = null )
		{
			this.Requestor = requestor;
			this.Message = msg;
			this.Attributes = attribs;
		}
	}

	/// <summary>
	/// Agent tells the master what is the status if his apps. Master resends this to GUIs.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class AppsStateMessage : Message
	{
		public override bool IsFrequent { get { return true; } }

		[ProtoBuf.ProtoMember( 1 )]
		[MaybeNull]
		public Dictionary<AppIdTuple, AppState> AppsState;

		// time on sender when sending this message
		[ProtoBuf.ProtoMember( 2 )]
		public DateTime TimeStamp;

		public AppsStateMessage() {}
		public AppsStateMessage( Dictionary<AppIdTuple, AppState> appsState, DateTime timeStamp )
		{
			this.AppsState = new Dictionary<AppIdTuple, AppState>( appsState );
			this.TimeStamp = timeStamp;
		}

	}

	/// <summary>
	/// Master tells GUIs what is the status of the plans.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class PlansStateMessage : Message
	{
		public override bool IsFrequent { get { return true; } }

		[ProtoBuf.ProtoMember( 1 )]
		[MaybeNull]
		public Dictionary<string, PlanState> PlansState;

		public PlansStateMessage() {}
		public PlansStateMessage( Dictionary<string, PlanState> plansState )
		{
			this.PlansState = new Dictionary<string, PlanState>( plansState );
		}
	}

	/// <summary>
	/// Master's internal state of applications in the plan.
	/// Not in use.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class PlanAppsStateMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[MaybeNull]
		public string PlanName;

		[ProtoBuf.ProtoMember( 2 )]
		[MaybeNull]
		public Dictionary<AppIdTuple, PlanAppState> AppsState;

		public PlanAppsStateMessage() {}
		public PlanAppsStateMessage( string planName, Dictionary<AppIdTuple, PlanAppState> appsState )
		{
			this.PlanName = planName;
			this.AppsState = new Dictionary<AppIdTuple, PlanAppState>( appsState );
		}
	}

	[Flags]
	public enum EMsgRecipCateg
	{
		Agent       = 1 << 0,
		Gui         = 1 << 1,
		All         = Agent + Gui,
	}


	[Flags]
	public enum StartAppFlags
	{
		SetPlanApplied = 1 << 1,	 // Sets the AppState.PlanApplied flag
	}

	/// <summary>
	/// Someone asking the Master to start a concrete app. Resent by Master to the app's agent.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class StartAppMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		public AppIdTuple Id;

		/// <summary>
		/// From what plan the app def should be taken.
		///    null = last plan applied to this app
		///    empty = no plan (use default app def if exist)
		///    non-empty = plan name to take the appdef from
		/// This is used when the command is sent from gui to master.
		/// Must be always null if sent from master to agent (agent does not know about plans anyway)
		/// </summary>
		[ProtoBuf.ProtoMember( 2 )]
		public string? PlanName;

		[ProtoBuf.ProtoMember( 3 )]
		public StartAppFlags Flags;

		/// <summary>Env vars to be set for a process; also set as local vars for use in macro expansion</summary>
		[ProtoBuf.ProtoMember( 4 )]
		public Dictionary<string,string>? Vars;

		/// <summary>Do we want to set/change the variables for a processs?</summary>
		/// <remarks>This is necessary as protobuf will send empty dictionary as null</remarks>
		[ProtoBuf.ProtoMember( 5 )]
		public bool UseVars;

		public StartAppMessage() {}
		/// <param name="vars">if null, variables will NOT be changed from last use</param>
		public StartAppMessage( string requestorId, AppIdTuple id, string? planName, StartAppFlags flags=0, Dictionary<string,string>? vars=null )
		{
			this.Sender = requestorId;
			this.Id = id;
			this.PlanName = planName;
			this.Flags = flags;
			this.Vars = vars;
			this.UseVars = vars is not null;
		}

		public override string ToString()
		{
			return string.Format( "StartApp {0} plan {1}, flags={2}, vars={3}", Id.ToString(), PlanName, Flags.ToString(), Tools.EnvVarListToString(Vars) );
		}

	}

	[Flags]
	public enum KillAppFlags
	{
		ResetAppState = 1 << 0, // should we reset app state flags like if the app was never attempted to start
		//ForgetAppDef = 1 << 1, // agent should forget the definition of the app to stop sending its status as the appdefs are going to be replaced
	}

	/// <summary>
	/// Someone asking the Master to kill a concrete app. Resent by Master to the app's agent.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class KillAppMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		public AppIdTuple Id;

		[ProtoBuf.ProtoMember( 2 )]
		public KillAppFlags Flags;



		public KillAppMessage() {}
		public KillAppMessage( string requestorId, AppIdTuple id, KillAppFlags flags=0 )
		{
			this.Sender = requestorId;
			this.Id = id;
			this.Flags = flags;
		}

		public override string ToString()
		{
			return string.Format( "KillApp {0} {1}", Id.ToString(), Flags.ToString() );
		}

	}

	/// <summary>
	/// Someone asking the Master to restart a concrete app. Resent by Master to the app's agent.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class RestartAppMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		public AppIdTuple Id;

		/// <summary>Env vars to be set for a process; also set as local vars for use in macro expansion</summary>
		[ProtoBuf.ProtoMember( 2 )]
		public Dictionary<string,string>? Vars;

		/// <summary>Do we want to set/change the variables for a processs?</summary>
		/// <remarks>This is necessary as protobuf will send empty dictionary as null</remarks>
		[ProtoBuf.ProtoMember( 3 )]
		public bool UseVars;


		public RestartAppMessage() {}
		/// <param name="vars">if null, variables will NOT be changed from last use</param>
		public RestartAppMessage( string requestorId, AppIdTuple id, Dictionary<string,string>? vars=null )
		{
			this.Sender = requestorId;
			this.Id = id;
			this.Vars = vars;
			this.UseVars = vars is not null;
		}
		public override string ToString()
		{
			return string.Format( "RestartApp {0}", Id.ToString() );
		}

	}

	/// <summary>
	/// Someone asking the Master to temporarily remove an app from the plan execustion (if enabled=false).
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class SetAppEnabledMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		public string? PlanName;

		[ProtoBuf.ProtoMember( 2 )]
		public AppIdTuple Id;

		[ProtoBuf.ProtoMember( 3 )]
		public bool Enabled;

		public SetAppEnabledMessage() {}
		public SetAppEnabledMessage( string requestorId, string planName, AppIdTuple id, bool enabled )
		{
			this.Sender = requestorId;
			this.PlanName = planName;
			this.Id = id;
			this.Enabled = enabled;
		}

		public override string ToString()
		{
			return string.Format( $"SetAppEnabled {Id.ToString(PlanName)}, {Enabled}"  );
		}

	}

	/// <summary>
	/// GUI is telling the Master about what plan is currently selected there.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class SelectPlanMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		public string PlanName = string.Empty;

		public SelectPlanMessage() { }
		public SelectPlanMessage( string requestorId, String planName )
		{
			this.Sender = requestorId;
			this.PlanName = planName;
		}

		public override string ToString()
		{
			return string.Format( "SelectPlan {0}", PlanName );
		}

	}

	/// <summary>
	/// Someone asking the Master to start a plan.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class StartPlanMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		public String PlanName = string.Empty;

		/// <summary>Env vars to be set for each process in the plan; also set as local vars for use in macro expansion</summary>
		[ProtoBuf.ProtoMember( 2 )]
		public Dictionary<string,string>? Vars;

		/// <summary>Do we want to set/change the variables for a processs?</summary>
		/// <remarks>This is necessary as protobuf will send empty dictionary as null</remarks>
		[ProtoBuf.ProtoMember( 3 )]
		public bool UseVars;

		public StartPlanMessage() {}

		/// <param name="vars">if null, variables will NOT be changed from last use</param>
		public StartPlanMessage( string requestorId, String planName, Dictionary<string,string>? vars=null )
		{
			this.Sender = requestorId;
			this.PlanName = planName;
			this.Vars = vars;
			this.UseVars = vars is not null;
		}

		public override string ToString() { return string.Format( "StartPlan {0}", PlanName ); }
	}

	/// <summary>
	/// Someone asking the Master to stop starting next apps from the plan (this does not kill the apps!)
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class StopPlanMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		public string PlanName = string.Empty;

		public StopPlanMessage() {}
		public StopPlanMessage( string requestorId, String planName )
		{
			this.Sender = requestorId;
			this.PlanName = planName;
		}

		public override string ToString() { return string.Format( "StopPlan {0}", PlanName ); }
	}

	/// <summary>
	/// Someone asking the Master to kill all apps in the plan.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class KillPlanMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		public string PlanName = string.Empty;

		public KillPlanMessage() {}
		public KillPlanMessage( string requestorId, String planName )
		{
			this.Sender = requestorId;
			this.PlanName = planName;
		}

		public override string ToString() { return string.Format( "KillPlan {0}", PlanName ); }
	}

	/// <summary>
	/// Someone asking the Master to restart all apps in the plan.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class RestartPlanMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		public string PlanName = string.Empty;

		/// <summary>Env vars to be set for each process in the plan; also set as local vars for use in macro expansion</summary>
		[ProtoBuf.ProtoMember( 2 )]
		public Dictionary<string,string>? Vars;

		/// <summary>Do we want to set/change the variables for a processs?</summary>
		/// <remarks>This is necessary as protobuf will send empty dictionary as null</remarks>
		[ProtoBuf.ProtoMember( 3 )]
		public bool UseVars;

		public RestartPlanMessage() {}
		/// <param name="vars">if null, variables will NOT be changed from last use</param>
		public RestartPlanMessage( string requestorId, String planName, Dictionary<string,string>? vars=null )
		{
			this.Sender = requestorId;
			this.PlanName = planName;
			this.Vars = vars;
			this.UseVars = vars is not null;
		}

		public override string ToString() { return string.Format( "RestartPlan {0}", PlanName ); }
	}

	///// <summary>
	///// Master tells new client about the current launch plan
	///// </summary>
	//[ProtoBuf.ProtoContract]
	//public class CurrentPlanMessage : Message
	//{
	//	[ProtoBuf.ProtoMember( 1 )]
	//	public string PlanName = string.Empty;

	//	public CurrentPlanMessage() {}
	//	public CurrentPlanMessage( String planName )
	//	{
	//		this.PlanName = planName;
	//	}

	//	public override string ToString()
	//	{
	//		return string.Format( "CurrentPlan {0}", PlanName );
	//	}
	//}

	/// <summary>
	/// Master tells new client about existing plans
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class PlanDefsMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[MaybeNull]
		public List<PlanDef> PlanDefs;

		/// <summary>
		/// Whether the recipient shall descard any extra items not contained in this message (false) or just add/update existing (true)
		/// </summary>
		[ProtoBuf.ProtoMember( 2 )]
		public bool Incremental;

		public PlanDefsMessage() {}
		public PlanDefsMessage( IEnumerable<PlanDef> planDefs, bool incremental )
		{
			this.PlanDefs = new List<PlanDef>(planDefs);
			this.Incremental = incremental;
		}

		public override string ToString()
		{
			if( PlanDefs is null ) return "PlanDefs = null";
			return $"PlanDefs [{string.Join(", ", from x in PlanDefs select x.Name)}], increm={Incremental}";
		}
	}

	/// <summary>
	/// Someone asking the Master to set env vars for newly started processed. Master resends to all agents.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class SetVarsMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		public string Vars = string.Empty;

		public SetVarsMessage() {}
		public SetVarsMessage( string requestorId, string vars )
		{
			this.Sender = requestorId;
			this.Vars = vars;
		}

		public override string ToString()
		{
			return string.Format( "SetVars {0}", Vars );
		}

	}


	/// <summary>
	/// Someone asking the Master to kill all apps and plans. Master kills the plans, resending KillApp to all agents.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class KillAllMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		public KillAllArgs Args;

		public KillAllMessage() {}
		public KillAllMessage( string requestorId, KillAllArgs args )
		{
			this.Sender = requestorId;
			this.Args = args;
		}

		public override string ToString()
		{
			return $"KillAll {Args.MachineId}";
		}

	}

	/// <summary>
	/// Someone asking the Master to terminate the agents/guis. Master resends to agents/guis.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class TerminateMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		public TerminateArgs Args;

		public TerminateMessage() {}
		public TerminateMessage( string requestorId, TerminateArgs args )
		{
			this.Sender = requestorId;
			this.Args = args;
		}

		public override string ToString()
		{
			return string.Format( "Terminate killApps={0} machineId={1}", Args.KillApps, Args.MachineId );
		}

	}

	/// <summary>
	/// Someone asking the Master to shutdown/reboot the computers. Master resends to agents.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class ShutdownMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		public ShutdownArgs Args;

		public ShutdownMessage() {}
		public ShutdownMessage( string requestorId, ShutdownArgs args )
		{
			this.Sender = requestorId;
			this.Args = args;
		}

		public override string ToString()
		{
			return string.Format( "Shutdown mode={0}", Args.Mode.ToString() );
		}

	}

	/// <summary>
	/// Someone asking the Master to reinstall the Dirigent. Master resends to all.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class ReinstallMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		public ReinstallArgs Args;

		public ReinstallMessage() {}
		public ReinstallMessage( string requestorId, ReinstallArgs args )
		{
			this.Sender = requestorId;
			this.Args = args;
		}

		public override string ToString()
		{
			return string.Format( "Reinstall dwonloadMode={0}, url={1}", Args.DownloadMode.ToString(), Args.Url );
		}

	}

	/// <summary>
	/// Someone asking the Master to reload the shared config.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class ReloadSharedConfigMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		public ReloadSharedConfigArgs Args;

		public ReloadSharedConfigMessage() {}
		public ReloadSharedConfigMessage( string requestorId, ReloadSharedConfigArgs args )
		{
			this.Sender = requestorId;
			this.Args = args;
		}

		public override string ToString()
		{
			return string.Format( "ReloadSharedConfig killApps={0}", Args.KillApps );
		}

	}

	/// <summary>
	/// Client (Agent or GUI) identifies itself. Must be the first message sent when connected to the Master.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class ClientIdent : Message
	{
		///<summary>Client name. For agents, this equals the MachineId. For Guis this is a stringized GUID</summary>
		public string Name
		{
			get { return Sender; }
			set { Sender = value; }
		}

		[ProtoBuf.ProtoMember( 2 )]
		public EMsgRecipCateg SubscribedTo;

		public ClientIdent() {}

		public ClientIdent( string name, EMsgRecipCateg subscription )
		{
			Name = name;
			SubscribedTo = subscription;
		}

		public override string ToString()
		{
			return $"ClientInfo Name {Sender}, Type {(int)SubscribedTo}";
		}

		public bool IsGui => (SubscribedTo & EMsgRecipCateg.Gui) != 0;
		public bool IsAgent => (SubscribedTo & EMsgRecipCateg.Agent) != 0;

	}

	/// <summary>
	/// Master tells the agent what app defs to use. Agent will overwrite the already known apps and add the new apps.
	/// The app defs are applied when next time starting the app, the currently runnign app is not affected.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class AppDefsMessage : Message
	{

		[ProtoBuf.ProtoMember( 1 )]
		[MaybeNull] // when constructed without arguments by protobuf
		public List<AppDef> AppDefs;

		/// <summary>
		/// Whether the recipient shall descard any extra items not contained in this message (false) or just add/update existing (true)
		/// </summary>
		[ProtoBuf.ProtoMember( 2 )]
		public bool Incremental;

		public AppDefsMessage() {}
		public AppDefsMessage( IEnumerable<AppDef> appDefs, bool incremental )
		{
			AppDefs = new List<AppDef>(appDefs);
			Incremental = incremental;
		}

		public override string ToString()
		{
			if( AppDefs is null ) return "AppDefs = null";
			return $"AppDefs [{string.Join(", ", from x in AppDefs select x.Id.ToString(x.PlanName))}], increm={Incremental}";
		}

	}

	/// <summary>
	/// Someone asking the Master to execute given Command Line Interface command
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class CLIRequestMessage : Message
	{
		[ProtoBuf.ProtoMember( 2 )]
		public string Text = string.Empty;

		public CLIRequestMessage()
		{}

		public CLIRequestMessage( string text )
		{
			Text = text;
		}

		public override string ToString()
		{
			return $"CLI Request: {Text}";
		}
	}

	/// <summary>
	/// Master sends back to the sender the response from the just executed CLI command.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class CLIResponseMessage : Message
	{
		[ProtoBuf.ProtoMember( 2 )]
		public string Text = string.Empty;

		public CLIResponseMessage()
		{}

		public CLIResponseMessage( string text )
		{
			Text = text;
		}

		public override string ToString()
		{
			return $"CLI Response: {Text}";
		}
	}

	/// <summary>
	/// Master tells the client to forget every info - all apps etc.
	/// Sent when loading the shared config, before the new app defs are sent.
	/// </summary>
	/// <remarks>
	/// This will not kill the apps; so if the running ones are no longer part of the new app defs,
	/// they will stay running and it will not be possible to kill them via dirigent.
	/// </remarks>
	[ProtoBuf.ProtoContract]
	public class ResetMessage : Message
	{
		public ResetMessage() {}

		public override string ToString()
		{
			return $"Reset";
		}
	}

	/// <summary>
	/// Client is updating its state to master (at regular intervals)
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class ClientStateMessage : Message
	{
		public override bool IsFrequent { get { return true; } }

		// time on sender when sending this message
		[ProtoBuf.ProtoMember( 1 )]
		public DateTime TimeStamp;

		[ProtoBuf.ProtoMember( 2 )]
		[MaybeNull]
		public ClientState State;

		public ClientStateMessage() {}
		public ClientStateMessage( DateTime timeStamp, ClientState state )
		{
		    this.TimeStamp = timeStamp;
			this.State = state;
		}

	}

	/// <summary>
	/// Someone asking the Master to start a script
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class StartScriptMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		public string? Id;

		//[ProtoBuf.ProtoMember( 2 )]
		//public string? FileName;

		[ProtoBuf.ProtoMember( 3 )]
		public string? Args;


		public StartScriptMessage() {}

		//public StartScriptMessage( string requestorId, string id, string? fileName, string?args )
		//{
		//	this.Sender = requestorId;
		//	this.Id = id;
		//	this.FileName = fileName;
		//	this.Args = args;
		//}

		public StartScriptMessage( string requestorId, string id, string? args )
		{
			this.Sender = requestorId;
			this.Id = id;
			this.Args = args;
		}

		public override string ToString()
		{
			return string.Format( $"StartScriptMessage {Id}" );
		}

	}

	/// <summary>
	/// Someone asking the Master to kill a running script.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class KillScriptMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[MaybeNull]
		public string Id;


		public KillScriptMessage() {}
		public KillScriptMessage( string requestorId, string id )
		{
			this.Sender = requestorId;
			this.Id = id;
		}

		public override string ToString()
		{
			return string.Format( "KillScript {0}", Id );
		}

	}

	/// <summary>
	/// Master tells new client about existing scripts
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class ScriptDefsMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[MaybeNull]
		public List<ScriptDef> ScriptDefs;

		/// <summary>
		/// Whether the recipient shall descard any extra items not contained in this message (false) or just add/update existing (true)
		/// </summary>
		[ProtoBuf.ProtoMember( 2 )]
		public bool Incremental;

		public ScriptDefsMessage() {}
		public ScriptDefsMessage( IEnumerable<ScriptDef> scriptDefs, bool incremental )
		{
			this.ScriptDefs = new List<ScriptDef>(scriptDefs);
			this.Incremental = incremental;
		}

		public override string ToString()
		{
			if( ScriptDefs is null ) return "ScriptDefs = null";
			return $"ScriptDefs [{string.Join(", ", from x in ScriptDefs select x.Id)}], increm={Incremental}";
		}
	}

	/// <summary>
	/// Master tells clients aboout the status of the scripts.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class ScriptStateMessage : Message
	{
		public override bool IsFrequent { get { return true; } }

		[ProtoBuf.ProtoMember( 1 )]
		[MaybeNull]
		public Dictionary<string, ScriptState> ScriptsState;

		public ScriptStateMessage() {}
		public ScriptStateMessage( Dictionary<string, ScriptState> scriptsState )
		{
			this.ScriptsState = new Dictionary<string, ScriptState>( scriptsState );
		}
	}

	/// <summary>
	/// Someone is asking the master to update the app defs to the ones in given plan.
	/// Plan is applied either to given app from the plan (if specified), otherwise to all the apps in the plan.
	/// The app defs are applied when next time starting the app, the currently runnign app is not affected.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class ApplyPlanMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		public string PlanName = string.Empty;

		// if empty, the plan is applied to all the apps from the plan
		[ProtoBuf.ProtoMember( 2 )]
		public AppIdTuple AppIdTuple;

		public ApplyPlanMessage() { }
		public ApplyPlanMessage( String planName, AppIdTuple appIdTuple )
		{
			this.PlanName = planName;
			this.AppIdTuple = appIdTuple;
		}

		public override string ToString()
		{
			return string.Format( "ApplyPlan {0} {1}", PlanName, AppIdTuple );
		}

	}

	/// <summary>
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class SetWindowStyleMessage : Message
	{
		// if empty, the plan is applied to all the apps from the plan
		[ProtoBuf.ProtoMember( 1 )]
		public AppIdTuple AppIdTuple;

		[ProtoBuf.ProtoMember( 2 )]
		public EWindowStyle WindowStyle = EWindowStyle.NotSet;

		public SetWindowStyleMessage() { }
		public SetWindowStyleMessage( AppIdTuple appIdTuple, EWindowStyle windowStyle )
		{
			this.AppIdTuple = appIdTuple;
			this.WindowStyle = windowStyle;
		}

		public override string ToString()
		{
			return string.Format( "SeWindowStyle of {0} to {1}", AppIdTuple, WindowStyle );
		}

	}

	/// <summary>
	/// Master tells new client about existing VfsNodes
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class VfsNodesMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		public List<VfsNodeDef> VfsNodes = new List<VfsNodeDef>();

		public VfsNodesMessage() { }
		public VfsNodesMessage( IEnumerable<VfsNodeDef> vfsNodes )
		{
			this.VfsNodes = new List<VfsNodeDef>( vfsNodes );
		}
		
		public override string ToString()
		{
			return $"VfsNodes ({VfsNodes.Count})";
		}
	}

	/// <summary>
	/// Master tells new client about existing machine definitions
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class MachineDefsMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		public List<MachineDef> Machines = new List<MachineDef>();


		public MachineDefsMessage() { }
		public MachineDefsMessage( IEnumerable<MachineDef> mdefs )
		{
			this.Machines = new List<MachineDef>( mdefs );
		}
		
		public override string ToString()
		{
			return $"MachineDefs ({Machines.Count})";
		}
	}


	/// <summary>
	/// Someone asking the Master to start given task
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class StartTaskMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		public string? Id;

		//[ProtoBuf.ProtoMember( 2 )]
		//public string? FileName;

		[ProtoBuf.ProtoMember( 3 )]
		public string? Args;


		public StartTaskMessage() {}

		//public StartScriptMessage( string requestorId, string id, string? fileName, string?args )
		//{
		//	this.Sender = requestorId;
		//	this.Id = id;
		//	this.FileName = fileName;
		//	this.Args = args;
		//}

		public StartTaskMessage( string requestorId, string id, string? args )
		{
			this.Sender = requestorId;
			this.Id = id;
			this.Args = args;
		}

		public override string ToString()
		{
			return string.Format( $"StartTaskMessage {Id}" );
		}

	}

	/// <summary>
	/// Someone asking the Master to kill a running task.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class KillTaskMessage : Message
	{
		// the task instance to kill
		[ProtoBuf.ProtoMember( 1 )]
		[MaybeNull]
		public Guid Guid;


		public KillTaskMessage() {}
		public KillTaskMessage( string requestorId, Guid guid )
		{
			this.Sender = requestorId;
			this.Guid = guid;
		}

		public override string ToString()
		{
			return string.Format( "KillTaskMessage {0}", Guid );
		}

	}

	/// <summary>
	/// Master tells new client about existing tasks
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class TaskDefsMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[MaybeNull]
		public List<DTaskDef> TaskDefs;

		/// <summary>
		/// Whether the recipient shall descard any extra items not contained in this message (false) or just add/update existing (true)
		/// </summary>
		[ProtoBuf.ProtoMember( 2 )]
		public bool Incremental;

		public TaskDefsMessage() {}
		public TaskDefsMessage( IEnumerable<DTaskDef> taskDefs, bool incremental )
		{
			this.TaskDefs = new List<DTaskDef>(taskDefs);
			this.Incremental = incremental;
		}

		public override string ToString()
		{
			if( TaskDefs is null ) return "TaskDefs = null";
			return $"TaskDefs [{string.Join(", ", from x in TaskDefs select x.Id)}], increm={Incremental}";
		}
	}

	/// <summary>
	/// Task controller agent tells other aboout the status of the tasks.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class TaskStateMessage : Message
	{
		public override bool IsFrequent { get { return true; } }

		[ProtoBuf.ProtoMember( 1 )]
		[MaybeNull]
		public Dictionary<string, DTaskState> TasksState;

		public TaskStateMessage() {}
		public TaskStateMessage( Dictionary<string, DTaskState> tasksState )
		{
			this.TasksState = new Dictionary<string, DTaskState>( tasksState );
		}
	}

	/// <summary>
	/// Request sent from a task to the same task instance on controller another agent(s).
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class TaskRequestMessage : Message
	{
		//public override bool IsFrequent { get { return true; } }

		/// <summary>
		/// Task instance to receive this message
		/// </summary>
		[ProtoBuf.ProtoMember( 1 )]
		public Guid TaskInstance;

		/// <summary>
		/// Who should handle the request.
		///   Empty = task controller.
		///   "[ALLWORKERS]" = all agents where the worker for this instance is instantiated.
		/// </summary>
		[ProtoBuf.ProtoMember( 2 )]
		public List<string> Recipients = new List<string>();

		/// <summary>
		/// Unique id of the request (might be used by responses to this particular request)
		/// </summary>
		[ProtoBuf.ProtoMember( 3 )]
		public Guid RequestId;

		/// <summary>
		/// Type id of the request; determines the expected format of the parameters.
		/// </summary>
		[ProtoBuf.ProtoMember( 4 )]
		public string Type = string.Empty;

		/// <summary>
		/// RequestType specific arguments for this request; any string, JSON by convention
		/// </summary>
		[ProtoBuf.ProtoMember( 5 )]
		[MaybeNull]
		public string? Args = string.Empty;

		public TaskRequestMessage() {}
		public TaskRequestMessage( Guid taskInstance, string type, string? args )
		{
			TaskInstance = taskInstance;
			RequestId = Guid.NewGuid();
			Type = type;
			Args = args;
		}
	}

	/// <summary>
	/// Response from a task to another to the same task instance on another agent.
	/// Used as worker status report to the controller, or for communicating among workers.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class TaskResponseMessage : Message
	{
		//public override bool IsFrequent { get { return true; } }

		/// <summary>
		/// Task instance to this response belongs to
		/// </summary>
		[ProtoBuf.ProtoMember( 1 )]
		public Guid TaskInstance;

		/// <summary>
		/// MachineIds to receive this message.
		/// Empty = just the controller on the master.
		/// Use Message.Sender value if you want to reply to the sender only.
		/// </summary>
		[ProtoBuf.ProtoMember( 2 )]
		public List<string> Recipients = new List<string>();

		/// <summary>
		/// Unique id of the request (might be used by responses to this particular request)
		/// Use the value of the TaskInstance is this is the reponse to the task worker instantiation request.
		/// </summary>
		[ProtoBuf.ProtoMember( 3 )]
		public Guid RequestId;

		/// <summary>
		/// Type id of the response; determines the expected format of the parameters.
		/// </summary>
		[ProtoBuf.ProtoMember( 4 )]
		public string Type = string.Empty;

		/// <summary>
		/// RequestType specific arguments for this response; any string, JSON by convention
		/// </summary>
		[ProtoBuf.ProtoMember( 5 )]
		[MaybeNull]
		public string? Args = string.Empty;

		public TaskResponseMessage() {}
		public TaskResponseMessage( Guid taskInstance, Guid requestId, string type, string? args )
		{
			TaskInstance = taskInstance;
			RequestId = requestId;
			Type = type;
			Args = args;
		}
	}


	/// <summary>
	/// Master asking the worker clients to instantiate a task
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class StartTaskWorkerMessage : Message
	{
		/// <summary>
		/// What task instance this worker belongs to.
		/// This can be also used as RequestId in the first response from the worker to its controller
		/// upon instantiating the worker part on the client. For example when the worker instantiation
		/// fails, the TaskResponse might be sent to the controller with this Guid.
		/// </summary>
		[ProtoBuf.ProtoMember( 1 )]
		public Guid TaskInstance;

		/// <summary>
		/// Name of the task this worker belongs to (for debug display purposes)
		/// </summary>
		[ProtoBuf.ProtoMember( 2 )]
		public string Id = string.Empty;

		/// <summary>
		/// MachineIds to start the worker part of the task on (empty = all agents)
		/// </summary>
		[ProtoBuf.ProtoMember( 3 )]
		public List<string> Workers = new List<string>();

		/// <summary>
		/// Name of the worker part script or built-in handler.
		/// </summary>
		/// <remarks>
		/// The name can contain a "subfolder part"
		///   MyScript
		///   FileTools/DownloadFile
		/// Some names are handled by built-in handlers (not requiring external script file).
		/// </remarks>
		[ProtoBuf.ProtoMember( 4 )]
		public string ScriptName = string.Empty;

		/// <summary>
		/// Script code to instantiate (C#); empty for built-in tasks.
		/// </summary>
		[ProtoBuf.ProtoMember( 5 )]
		public string? ScriptCode;

		/// <summary>
		/// Arguments to pass to the task worker
		/// </summary>
		[ProtoBuf.ProtoMember( 6 )]
		public string? Args;

		public StartTaskWorkerMessage() {}

		public StartTaskWorkerMessage( string requestorId, Guid taskInstance, string id )
		{
			this.Sender = requestorId;
			this.TaskInstance = taskInstance;
			this.Id = id;
		}

		public override string ToString()
		{
			return string.Format( $"StartTaskWorkerMessage {ScriptName} [{string.Join(", ", from x in Workers select x)}]" );
		}

	}

	/// <summary>
	/// Master asking the worker clients to remove everything beloning to given taks instance
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class KillTaskWorkersMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		public Guid TaskInstance;

		public KillTaskWorkersMessage() {}

		public KillTaskWorkersMessage( string requestorId, Guid taskInstance )
		{
			this.Sender = requestorId;
			this.TaskInstance = taskInstance;
		}

		public override string ToString()
		{
			return string.Format( $"KillTaskWorkersMessage {TaskInstance}" );
		}

	}
}
