using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Runtime.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace Dirigent.Net
{
	/// <summary>
	/// Base class for all messages.
	/// </summary>
	//[MessagePack.MessagePackObject]
	[MessagePack.Union( 101, typeof( RemoteOperationErrorMessage ) )]
	[MessagePack.Union( 102, typeof( AppsStateMessage ) )]
	[MessagePack.Union( 103, typeof( PlansStateMessage ) )]
	[MessagePack.Union( 104, typeof( StartAppMessage ) )]
	[MessagePack.Union( 105, typeof( KillAppMessage ) )]
	[MessagePack.Union( 106, typeof( RestartAppMessage ) )]
	[MessagePack.Union( 107, typeof( SetAppEnabledMessage ) )]
    [MessagePack.Union( 108, typeof(SelectPlanMessage))]
	[MessagePack.Union( 109, typeof( StartPlanMessage ) )]
	[MessagePack.Union( 110, typeof( StopPlanMessage ) )]
	[MessagePack.Union( 111, typeof( KillPlanMessage ) )]
	[MessagePack.Union( 112, typeof( RestartPlanMessage ) )]
  //[MessagePack.Union( 113, typeof( CurrentPlanMessage ) )]
	[MessagePack.Union( 114, typeof( PlanDefsMessage ) )]
	[MessagePack.Union( 115, typeof( SetVarsMessage ) )]
	[MessagePack.Union( 116, typeof( KillAllMessage ) )]
	[MessagePack.Union( 117, typeof( ShutdownMessage ) )]
	[MessagePack.Union( 118, typeof( ReinstallMessage ) )]
	[MessagePack.Union( 119, typeof( TerminateMessage ) )]
	[MessagePack.Union( 120, typeof( ReloadSharedConfigMessage ) )]
	[MessagePack.Union( 121, typeof( ClientIdent ) )]
	[MessagePack.Union( 122, typeof( AppDefsMessage ) )]
	[MessagePack.Union( 123, typeof( CLIRequestMessage ) )]
	[MessagePack.Union( 124, typeof( CLIResponseMessage ) )]
	[MessagePack.Union( 125, typeof( ResetMessage ) )]
	[MessagePack.Union( 126, typeof( ClientStateMessage ) )]
	[MessagePack.Union( 127, typeof( StartScriptMessage ) )]
	[MessagePack.Union( 128, typeof( KillScriptMessage ) )]
	[MessagePack.Union( 129, typeof( ScriptDefsMessage ) )]
	//[MessagePack.Union( 130, typeof( ScriptsStateMessage ) )]
	[MessagePack.Union( 131, typeof( ApplyPlanMessage ) )]
	[MessagePack.Union( 132, typeof( SetWindowStyleMessage ) )]
	[MessagePack.Union( 133, typeof( VfsNodesMessage ) )]
	[MessagePack.Union( 134, typeof( MachineDefsMessage ) )]
	//[MessagePack.Union( 135, typeof( StartTaskMessage ) )]
	//[MessagePack.Union( 136, typeof( KillTaskMessage ) )]
	[MessagePack.Union( 137, typeof( MenuItemDefsMessage ) )]
	[MessagePack.Union( 138, typeof( ScriptStateMessage ) )]
	[MessagePack.Union( 139, typeof( MachineStateMessage ) )]
	[MessagePack.Union( 140, typeof( RunActionMessage ) )]
	[MessagePack.Union( 141, typeof( UserNotificationMessage ) )]



	public abstract class Message
	{
		//[MessagePack.Key( 0 )]
		public string Sender { get; set; } = string.Empty;  // machine name for agents, guid for GUIs, empty for master

		// do not dump it on console
		[MessagePack.IgnoreMember]
		public virtual bool IsFrequent { get { return false; } }
	}

	/// <summary>
	/// Agent tells others there was an error processing some operation. Master resends this to GUIs.
	/// </summary>
	//[MessagePack.MessagePackObject]
	public class RemoteOperationErrorMessage : Message
	{
		//[MessagePack.Key( 1 )]
		public string Requestor = string.Empty;

		//[MessagePack.Key( 2 )]
		public string Message = string.Empty; // Error description

		//[MessagePack.Key( 3 )]
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
	//[MessagePack.MessagePackObject]
	public class AppsStateMessage : Message
	{
		[MessagePack.IgnoreMember]
		public override bool IsFrequent { get { return true; } }

		//[MessagePack.Key( 1 )]
		[MaybeNull]
		public Dictionary<AppIdTuple, AppState> AppsState;

		// time on sender when sending this message
		//[MessagePack.Key( 2 )]
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
	//[MessagePack.MessagePackObject]
	public class PlansStateMessage : Message
	{
		[MessagePack.IgnoreMember]
		public override bool IsFrequent { get { return true; } }

		//[MessagePack.Key( 1 )]
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
	//[MessagePack.MessagePackObject]
	public class PlanAppsStateMessage : Message
	{
		//[MessagePack.Key( 1 )]
		[MaybeNull]
		public string PlanName;

		//[MessagePack.Key( 2 )]
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
	//[MessagePack.MessagePackObject]
	public class StartAppMessage : Message
	{
		//[MessagePack.Key( 1 )]
		public AppIdTuple Id;

		/// <summary>
		/// From what plan the app def should be taken.
		///    null = last plan applied to this app
		///    empty = no plan (use default app def if exist)
		///    non-empty = plan name to take the appdef from
		/// This is used when the command is sent from gui to master.
		/// Must be always null if sent from master to agent (agent does not know about plans anyway)
		/// </summary>
		//[MessagePack.Key( 2 )]
		public string? PlanName;

		//[MessagePack.Key( 3 )]
		public StartAppFlags Flags;

		/// <summary>Env vars to be set for a process; also set as local vars for use in macro expansion</summary>
		//[MessagePack.Key( 4 )]
		public Dictionary<string,string>? Vars;

		/// <summary>Do we want to set/change the variables for a processs?</summary>
		/// <remarks>This is necessary as protobuf will send empty dictionary as null</remarks>
		//[MessagePack.Key( 5 )]
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
	//[MessagePack.MessagePackObject]
	public class KillAppMessage : Message
	{
		//[MessagePack.Key( 1 )]
		public AppIdTuple Id;

		//[MessagePack.Key( 2 )]
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
	//[MessagePack.MessagePackObject]
	public class RestartAppMessage : Message
	{
		//[MessagePack.Key( 1 )]
		public AppIdTuple Id;

		/// <summary>Env vars to be set for a process; also set as local vars for use in macro expansion</summary>
		//[MessagePack.Key( 2 )]
		public Dictionary<string,string>? Vars;

		/// <summary>Do we want to set/change the variables for a processs?</summary>
		/// <remarks>This is necessary as protobuf will send empty dictionary as null</remarks>
		//[MessagePack.Key( 3 )]
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
	//[MessagePack.MessagePackObject]
	public class SetAppEnabledMessage : Message
	{
		//[MessagePack.Key( 1 )]
		public string? PlanName;

		//[MessagePack.Key( 2 )]
		public AppIdTuple Id;

		//[MessagePack.Key( 3 )]
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
	//[MessagePack.MessagePackObject]
	public class SelectPlanMessage : Message
	{
		//[MessagePack.Key( 1 )]
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
	//[MessagePack.MessagePackObject]
	public class StartPlanMessage : Message
	{
		//[MessagePack.Key( 1 )]
		public String PlanName = string.Empty;

		/// <summary>Env vars to be set for each process in the plan; also set as local vars for use in macro expansion</summary>
		//[MessagePack.Key( 2 )]
		public Dictionary<string,string>? Vars;

		/// <summary>Do we want to set/change the variables for a processs?</summary>
		/// <remarks>This is necessary as protobuf will send empty dictionary as null</remarks>
		//[MessagePack.Key( 3 )]
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
	//[MessagePack.MessagePackObject]
	public class StopPlanMessage : Message
	{
		//[MessagePack.Key( 1 )]
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
	//[MessagePack.MessagePackObject]
	public class KillPlanMessage : Message
	{
		//[MessagePack.Key( 1 )]
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
	//[MessagePack.MessagePackObject]
	public class RestartPlanMessage : Message
	{
		//[MessagePack.Key( 1 )]
		public string PlanName = string.Empty;

		/// <summary>Env vars to be set for each process in the plan; also set as local vars for use in macro expansion</summary>
		//[MessagePack.Key( 2 )]
		public Dictionary<string,string>? Vars;

		/// <summary>Do we want to set/change the variables for a processs?</summary>
		/// <remarks>This is necessary as protobuf will send empty dictionary as null</remarks>
		//[MessagePack.Key( 3 )]
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
	////[MessagePack.MessagePackObject]
	//public class CurrentPlanMessage : Message
	//{
	//	//[MessagePack.Key( 1 )]
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
	//[MessagePack.MessagePackObject]
	public class PlanDefsMessage : Message
	{
		//[MessagePack.Key( 1 )]
		[MaybeNull]
		public List<PlanDef> PlanDefs;

		/// <summary>
		/// Whether the recipient shall descard any extra items not contained in this message (false) or just add/update existing (true)
		/// </summary>
		//[MessagePack.Key( 2 )]
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
	//[MessagePack.MessagePackObject]
	public class SetVarsMessage : Message
	{
		//[MessagePack.Key( 1 )]
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
	//[MessagePack.MessagePackObject]
	public class KillAllMessage : Message
	{
		//[MessagePack.Key( 1 )]
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
	//[MessagePack.MessagePackObject]
	public class TerminateMessage : Message
	{
		//[MessagePack.Key( 1 )]
		public TerminateArgs Args;
		public string? MachineId; // where to terminate

		public TerminateMessage() {}
		public TerminateMessage( string requestorId, TerminateArgs args, string? machineId = null )
		{
			this.Sender = requestorId;
			this.Args = args;
			this.MachineId = machineId;
		}

		public override string ToString()
		{
			return string.Format( "Terminate killApps={0} machineId={1}", Args.KillApps, MachineId );
		}

	}

	/// <summary>
	/// Someone asking the Master to shutdown/reboot the computers. Master resends to agents.
	/// </summary>
	//[MessagePack.MessagePackObject]
	public class ShutdownMessage : Message
	{
		//[MessagePack.Key( 1 )]
		public ShutdownArgs Args;

		public string? MachineId; // where to kill the apps; null or empty means everywhere

		public ShutdownMessage() {}
		public ShutdownMessage( string requestorId, ShutdownArgs args, string? machineId )
		{
			this.Sender = requestorId;
			this.Args = args;
			this.MachineId = machineId;
		}

		public override string ToString()
		{
			return string.Format( "Shutdown mode={0}, machine={1}", Args.Mode.ToString(), MachineId );
		}

	}

	/// <summary>
	/// Someone asking the Master to reinstall the Dirigent. Master resends to all.
	/// </summary>
	//[MessagePack.MessagePackObject]
	public class ReinstallMessage : Message
	{
		//[MessagePack.Key( 1 )]
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
	//[MessagePack.MessagePackObject]
	public class ReloadSharedConfigMessage : Message
	{
		//[MessagePack.Key( 1 )]
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
	//[MessagePack.MessagePackObject]
	public class ClientIdent : Message
	{
		///<summary>Client name. For agents, this equals the MachineId. For Guis this is a stringized GUID</summary>
		////[MessagePack.Key( 1 )]	// this does not need seralizing, as the Sender is already serialized
		[MessagePack.IgnoreMember]
		public string Name
		{
			get { return Sender; }
			set { Sender = value; }
		}

		//[MessagePack.Key( 2 )]
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

		[MessagePack.IgnoreMember]
		public bool IsGui => (SubscribedTo & EMsgRecipCateg.Gui) != 0;

		[MessagePack.IgnoreMember]
		public bool IsAgent => (SubscribedTo & EMsgRecipCateg.Agent) != 0;

	}

	/// <summary>
	/// Master tells the agent what app defs to use. Agent will overwrite the already known apps and add the new apps.
	/// The app defs are applied when next time starting the app, the currently runnign app is not affected.
	/// </summary>
	//[MessagePack.MessagePackObject]
	public class AppDefsMessage : Message
	{

		//[MessagePack.Key( 1 )]
		[MaybeNull] // when constructed without arguments by protobuf
		public List<AppDef> AppDefs;

		/// <summary>
		/// Whether the recipient shall descard any extra items not contained in this message (false) or just add/update existing (true)
		/// </summary>
		//[MessagePack.Key( 2 )]
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
	//[MessagePack.MessagePackObject]
	public class CLIRequestMessage : Message
	{
		//[MessagePack.Key( 2 )]
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
	//[MessagePack.MessagePackObject]
	public class CLIResponseMessage : Message
	{
		//[MessagePack.Key( 2 )]
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
	//[MessagePack.MessagePackObject]
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
	//[MessagePack.MessagePackObject]
	public class ClientStateMessage : Message
	{
		[MessagePack.IgnoreMember]
		public override bool IsFrequent { get { return true; } }

		// time on sender when sending this message
		//[MessagePack.Key( 1 )]
		public DateTime TimeStamp;

		//[MessagePack.Key( 2 )]
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
	//[MessagePack.MessagePackObject]
	public class StartScriptMessage : Message
	{
		/// <summary>
		/// Guid to assign to a script once its instance is created
		/// </summary>
		//[MessagePack.Key( 1 )]
		public Guid Instance;
					
		//[MessagePack.Key( 2 )]
		public string Title = string.Empty;

		/// <summary>
		/// Name of the script as known to the script library.
		/// If empty, the script is taken via from the script defs loaded from the shared config (Def.Id == Instance).
		/// </summary>
		/// <remarks>
		/// The name can contain a "subfolder part"
		///   MyScript
		///   FileTools/DownloadFile
		/// Some names are stored in the built-in script library (not requiring external script file).
		/// </remarks>
		//[MessagePack.Key( 3 )]
		public string ScriptName = string.Empty;

		/// <summary>
		/// Script code to instantiate (C#); empty for built-in tasks or file-based scripts.
		/// If empty, the script is loaded from the script library.
		/// </summary>
		//[MessagePack.Key( 4 )]
		public string? SourceCode;


		//[MessagePack.Key( 5 )]
		public byte[]? Args;

		/// <summary>
		/// Client where the script shall be started.
		/// </summary>
		//[MessagePack.Key( 6 )]
		public string HostClientId = "";

		/// <summary>
		/// Who wants the results back. Null=not defined.
		/// </summary>
		//[MessagePack.Key( 7 )]
		public string? Requestor = null;

		public StartScriptMessage() {}

		/// <summary> Starts a singleton script from a ScriptDef on master </summary>
		public StartScriptMessage( string requestorId, Guid singletonScriptId, string? args )
		{
			this.Instance = singletonScriptId;
			this.Args = Tools.Serialize( args );
			this.Requestor = requestorId;
		}

		/// <summary> Starts script on given client </summary>
		public StartScriptMessage( string requestorId, Guid instance, string scriptName, string? scriptCode, byte[]? args, string title, string hostClientId )
		{
			this.Instance = instance;
			this.Title = title;
			this.ScriptName = scriptName;
			this.SourceCode = scriptCode;
			this.Args = args;
			this.HostClientId = hostClientId;
			this.Requestor = requestorId;
		}


		public override string ToString()
		{
			return string.Format( $"StartScriptMessage [{Instance}] {Title} {ScriptName} @ {HostClientId}" );
		}

	}

	/// <summary>
	/// Asking to kill an instance of a running script (wherever it is running)
	/// </summary>
	//[MessagePack.MessagePackObject]
	public class KillScriptMessage : Message
	{
		//[MessagePack.Key( 1 )]
		public Guid Instance;


		public KillScriptMessage() {}
		public KillScriptMessage( string requestorId, Guid instance )
		{
			this.Sender = requestorId;
			this.Instance = instance;
		}

		public override string ToString()
		{
			return string.Format( "KillScript {0}", Instance );
		}

	}

	/// <summary>
	/// Master tells new client about existing script definitions
	/// (they are used for single-instance scripts presented on GUI)
	/// </summary>
	//[MessagePack.MessagePackObject]
	public class ScriptDefsMessage : Message
	{
		//[MessagePack.Key( 1 )]
		[MaybeNull]
		public List<ScriptDef> ScriptDefs;

		/// <summary>
		/// Whether the recipient shall descard any extra items not contained in this message (false) or just add/update existing (true)
		/// </summary>
		//[MessagePack.Key( 2 )]
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

	///// <summary>
	///// Master tells clients aboout the status of the scripts.
	///// </summary>
	////[MessagePack.MessagePackObject]
	//public class ScriptsStateMessage : Message
	//{
	//	[MessagePack.IgnoreMember]
	//	public override bool IsFrequent { get { return true; } }

	//	//[MessagePack.Key( 1 )]
	//	[MaybeNull]
	//	public Dictionary<Guid, ScriptState> ScriptsState;

	//	public ScriptsStateMessage() {}
	//	public ScriptsStateMessage( Dictionary<Guid, ScriptState> scriptsState )
	//	{
	//		this.ScriptsState = new Dictionary<Guid, ScriptState>( scriptsState );
	//	}
	//}

	/// <summary>
	/// Someone is asking the master to update the app defs to the ones in given plan.
	/// Plan is applied either to given app from the plan (if specified), otherwise to all the apps in the plan.
	/// The app defs are applied when next time starting the app, the currently runnign app is not affected.
	/// </summary>
	//[MessagePack.MessagePackObject]
	public class ApplyPlanMessage : Message
	{
		//[MessagePack.Key( 1 )]
		public string PlanName = string.Empty;

		// if empty, the plan is applied to all the apps from the plan
		//[MessagePack.Key( 2 )]
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
	//[MessagePack.MessagePackObject]
	public class SetWindowStyleMessage : Message
	{
		// if empty, the plan is applied to all the apps from the plan
		//[MessagePack.Key( 1 )]
		public AppIdTuple AppIdTuple;

		//[MessagePack.Key( 2 )]
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
	//[MessagePack.MessagePackObject]
	public class VfsNodesMessage : Message
	{
		//[MessagePack.Key( 1 )]
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
	//[MessagePack.MessagePackObject]
	public class MachineDefsMessage : Message
	{
		//[MessagePack.Key( 1 )]
		public List<MachineDef> Machines = new();


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
	/// Master tells new client about existing actions
	/// </summary>
	//[MessagePack.MessagePackObject]
	public class MenuItemDefsMessage : Message
	{
		//[MessagePack.Key( 1 )]
		[MaybeNull]
		public List<AssocMenuItemDef> MenuItemDefs;

		/// <summary>
		/// Whether the recipient shall descard any extra items not contained in this message (false) or just add/update existing (true)
		/// </summary>
		//[MessagePack.Key( 2 )]
		public bool Incremental;

		public MenuItemDefsMessage() {}
		public MenuItemDefsMessage( IEnumerable<AssocMenuItemDef> miDefs, bool incremental )
		{
			this.MenuItemDefs = new List<AssocMenuItemDef>(miDefs);
			this.Incremental = incremental;
		}

		public override string ToString()
		{
			if( MenuItemDefs is null ) return "MenuItemDefs = null";
			return $"MenuItemDefs [{string.Join(", ", from x in MenuItemDefs select x.Id)}], increm={Incremental}";
		}
	}

	/// <summary>
	/// Client tells other the status of the script running on it
	/// Sent periodically for running scripts, and once for finished script (Status=Finished or Failed means the task should be removed).
	/// </summary>
	//[MessagePack.MessagePackObject]
	public class ScriptStateMessage : Message
	{
		//[MessagePack.Key( 1 )]
		public Guid Instance;
		
		//[MessagePack.Key( 2 )]
		public ScriptState State = new();

		public ScriptStateMessage() {}
		public ScriptStateMessage( Guid instance, ScriptState state )
		{
			this.Instance = instance;
			this.State = state;
		}

		public override string ToString()
		{
			return $"ScriptState [{Instance}] {State}";
		}
	}

	/// <summary>
	/// Agent on the machine is updating the machine state to master (at regular intervals)
	/// </summary>
	//[MessagePack.MessagePackObject]
	public class MachineStateMessage : Message
	{
		[MessagePack.IgnoreMember]		
		public override bool IsFrequent { get { return true; } }

		//[MessagePack.Key( 1 )]
		public string Id = "";

		// time on sender when sending this message
		//[MessagePack.Key( 2 )]
		public DateTime TimeStamp;

		//[MessagePack.Key( 3 )]
		public MachineState State = new();

		public MachineStateMessage() {}
		public MachineStateMessage( string id, DateTime timeStamp, MachineState state )
		{
		    this.Id = id;
			this.TimeStamp = timeStamp;
			this.State = state;
		}

	}

	//[MessagePack.MessagePackObject]
	public class RunActionMessage : Message
	{
		//[MessagePack.Key( 1 )]
		public ActionDef? Def;

		/// <summary>Internal/local vars passed to the action (can be used macro expansion)</summary>
		//[MessagePack.Key( 2 )]
		public Dictionary<string,string>? Vars;

		/// <summary>
		/// Client where the action shall be started.
		/// </summary>
		//[MessagePack.Key( 3 )]
		public string HostClientId = "";

		/// <summary>
		/// Who wants the result of the action (if any)
		/// </summary>
		//[MessagePack.Key( 4 )]
		public string? Requestor = null;

		public RunActionMessage() {}

		/// <param name="vars">if null, variables will NOT be changed from last use</param>
		public RunActionMessage( string requestorId, ActionDef def, string hostClientId, Dictionary<string,string>? vars=null )
		{
			this.Def = def;
			this.Vars = vars;
			this.HostClientId = hostClientId;
			this.Requestor = requestorId;
		}

		public override string ToString()
		{
			return $"RunAction {Def}, vars={Tools.EnvVarListToString(Vars)}";
		}

	}

	/// <summary>
	/// We want the user on given client to be notified about something
	/// </summary>
	//[MessagePack.MessagePackObject]
	public class UserNotificationMessage : Message
	{
		public enum ECategory
		{
			Info,
			Warning,
			Error,
		}

		public enum EPresentationType
		{
			Default,
			MessageBox,
			BalloonTip,
		}


		/// <summary>
		/// Where to show the notification
		/// </summary>
		//[MessagePack.Key( 1 )]
		public string HostClientId = string.Empty;

		/// <summary>
		/// Categiry of the message
		/// </summary>
		//[MessagePack.Key( 2 )]
		public ECategory Category = ECategory.Info;

		/// <summary>
		/// Catogiry of the message
		/// </summary>
		//[MessagePack.Key( 3 )]
		public EPresentationType PresentationType = EPresentationType.Default;

		/// <summary>
		/// Title of the message
		/// </summary>
		//[MessagePack.Key( 4 )]
		public string? Title;

		/// <summary>
		/// Message to show
		/// </summary>
		//[MessagePack.Key( 5 )]
		public string Message = string.Empty;

		/// <summary>
		/// What action to run if the user clicks on the notification
		/// </summary>
		//[MessagePack.Key( 6 )]
		public ActionDef? Action;


		//[MessagePack.Key( 7 )]
		public Dictionary<string, string>? Attributes; // additional attribute pairs (name, value)

		/// <summary>
		/// How long to keep the mesage displayed in seconds (0=default)
		/// </summary>
		//[MessagePack.Key( 8 )]
		public double Timeout;
	}
}
