using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dirigent.Common;

using System.Runtime.Serialization;

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
			{ 104, typeof( LaunchAppMessage ) },
			{ 105, typeof( KillAppMessage ) },
			{ 106, typeof( RestartAppMessage ) },
			{ 107, typeof( SetAppEnabledMessage ) },
			//{ 108, typeof(SelectPlanMessage) },
			{ 109, typeof( StartPlanMessage ) },
			{ 110, typeof( StopPlanMessage ) },
			{ 111, typeof( KillPlanMessage ) },
			{ 112, typeof( RestartPlanMessage ) },
			{ 113, typeof( CurrentPlanMessage ) },
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
		};
	}

	/// <summary>
	/// Base class for all messages.
	/// </summary>
	[ProtoBuf.ProtoContract]
	[DataContract]
	[KnownType( typeof( RemoteOperationErrorMessage ) ), ProtoBuf.ProtoInclude( 101, typeof( RemoteOperationErrorMessage ) )]
	[KnownType( typeof( AppsStateMessage ) ), ProtoBuf.ProtoInclude( 102, typeof( AppsStateMessage ) )]
	[KnownType( typeof( PlansStateMessage ) ), ProtoBuf.ProtoInclude( 103, typeof( PlansStateMessage ) )]
	[KnownType( typeof( LaunchAppMessage ) ), ProtoBuf.ProtoInclude( 104, typeof( LaunchAppMessage ) )]
	[KnownType( typeof( KillAppMessage ) ), ProtoBuf.ProtoInclude( 105, typeof( KillAppMessage ) )]
	[KnownType( typeof( RestartAppMessage ) ), ProtoBuf.ProtoInclude( 106, typeof( RestartAppMessage ) )]
	[KnownType( typeof( SetAppEnabledMessage ) ), ProtoBuf.ProtoInclude( 107, typeof( SetAppEnabledMessage ) )]
	//[KnownType(typeof(SelectPlanMessage))         , ProtoBuf.ProtoInclude(108, typeof(SelectPlanMessage))]
	[KnownType( typeof( StartPlanMessage ) ), ProtoBuf.ProtoInclude( 109, typeof( StartPlanMessage ) )]
	[KnownType( typeof( StopPlanMessage ) ), ProtoBuf.ProtoInclude( 110, typeof( StopPlanMessage ) )]
	[KnownType( typeof( KillPlanMessage ) ), ProtoBuf.ProtoInclude( 111, typeof( KillPlanMessage ) )]
	[KnownType( typeof( RestartPlanMessage ) ), ProtoBuf.ProtoInclude( 112, typeof( RestartPlanMessage ) )]
	[KnownType( typeof( CurrentPlanMessage ) ), ProtoBuf.ProtoInclude( 113, typeof( CurrentPlanMessage ) )]
	[KnownType( typeof( PlanDefsMessage ) ), ProtoBuf.ProtoInclude( 114, typeof( PlanDefsMessage ) )]
	[KnownType( typeof( SetVarsMessage ) ), ProtoBuf.ProtoInclude( 115, typeof( SetVarsMessage ) )]
	[KnownType( typeof( KillAllMessage ) ), ProtoBuf.ProtoInclude( 116, typeof( KillAllMessage ) )]
	[KnownType( typeof( ShutdownMessage ) ), ProtoBuf.ProtoInclude( 117, typeof( ShutdownMessage ) )]
	[KnownType( typeof( ReinstallMessage ) ), ProtoBuf.ProtoInclude( 118, typeof( ReinstallMessage ) )]
	[KnownType( typeof( TerminateMessage ) ), ProtoBuf.ProtoInclude( 119, typeof( TerminateMessage ) )]
	[KnownType( typeof( ReloadSharedConfigMessage ) ), ProtoBuf.ProtoInclude( 120, typeof( ReloadSharedConfigMessage ) )]
	[KnownType( typeof( ClientIdent ) ), ProtoBuf.ProtoInclude( 121, typeof( ClientIdent ) )]
	[KnownType( typeof( AppDefsMessage ) ), ProtoBuf.ProtoInclude( 122, typeof( AppDefsMessage ) )]
	[KnownType( typeof( CLIRequestMessage ) ), ProtoBuf.ProtoInclude( 123, typeof( CLIRequestMessage ) )]
	[KnownType( typeof( CLIResponseMessage ) ), ProtoBuf.ProtoInclude( 124, typeof( CLIResponseMessage ) )]
	public class Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public string Sender { get; set; } = string.Empty;  // machine name for agents, guid for GUIs, empty for master

		public static void RegisterProtobufTypeMaps()
		{
			//ProtoBuf.Meta.RuntimeTypeModel.Default.Add( typeof( ILaunchPlan ), true ).AddSubType( 50, typeof( LaunchPlan ) );
		}
	}

	[ProtoBuf.ProtoContract]
	[DataContract]
	public class RemoteOperationErrorMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public string Requestor = string.Empty;

		[ProtoBuf.ProtoMember( 2 )]
		[DataMember]
		public string Message = string.Empty; // Error description

		[ProtoBuf.ProtoMember( 3 )]
		[DataMember]
		public Dictionary<string, string>? Attributes; // additional attribute pairs (name, value)

		public RemoteOperationErrorMessage() {}
		public RemoteOperationErrorMessage( string requestor, string msg, Dictionary<string, string>? attribs = null )
		{
			this.Requestor = requestor;
			this.Message = msg;
			this.Attributes = attribs;
		}
	}

	// not used just if multicast option is enabled (like in case of many agents;
	// this msg causes the master to resend to all clients (as in case of any other message) - heavy network load as this message is frequent!
	[ProtoBuf.ProtoContract]
	[DataContract]
	public class AppsStateMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public Dictionary<AppIdTuple, AppState>? AppsState;

		public AppsStateMessage() {}
		public AppsStateMessage( Dictionary<AppIdTuple, AppState> appsState )
		{
			this.AppsState = new Dictionary<AppIdTuple, AppState>( appsState );
		}
	}

	[ProtoBuf.ProtoContract]
	[DataContract]
	public class PlansStateMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public Dictionary<string, PlanState>? PlansState;

		public PlansStateMessage() {}
		public PlansStateMessage( Dictionary<string, PlanState> plansState )
		{
			this.PlansState = new Dictionary<string, PlanState>( plansState );
		}
	}

	[Flags]
	public enum EMsgRecipCateg
	{
		Agent       = 1 << 1,
		Gui         = 1 << 2,
		All         = Agent & Gui,
	}


	[ProtoBuf.ProtoContract]
	[DataContract]
	public class LaunchAppMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public AppIdTuple Id;

		/// <summary>
		/// From what plan the app def should be taken.
		///    null= no plan (use defaults)
		///    empty = last plan applied to this app
		///    non-empty = plan name to take the appdef from
		/// This is used when the command is sent from gui to master.
		/// Must be always null if sent from master to agent (agent does not know about plans anyway)
		/// </summary>
		[ProtoBuf.ProtoMember( 2 )]
		[DataMember]
		public string? PlanName;


		public LaunchAppMessage() {}
		public LaunchAppMessage( AppIdTuple id, string? planName )
		{
			this.Id = id;
			this.PlanName = planName;
		}

		public override string ToString()
		{
			return string.Format( "StartApp {0} plan {1}", Id.ToString(), PlanName );
		}

	}

	[ProtoBuf.ProtoContract]
	[DataContract]
	public class KillAppMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public AppIdTuple Id;

		public KillAppMessage() {}
		public KillAppMessage( AppIdTuple id )
		{
			this.Id = id;
		}

		public override string ToString()
		{
			return string.Format( "StopApp {0}", Id.ToString() );
		}

	}

	[ProtoBuf.ProtoContract]
	[DataContract]
	public class RestartAppMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public AppIdTuple Id;

		public RestartAppMessage() {}
		public RestartAppMessage( AppIdTuple id )
		{
			this.Id = id;
		}
		public override string ToString()
		{
			return string.Format( "RestartApp {0}", Id.ToString() );
		}

	}

	[ProtoBuf.ProtoContract]
	[DataContract]
	public class SetAppEnabledMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public string? PlanName;

		[ProtoBuf.ProtoMember( 2 )]
		[DataMember]
		public AppIdTuple Id;

		[ProtoBuf.ProtoMember( 3 )]
		[DataMember]
		public bool Enabled;

		public SetAppEnabledMessage() {}
		public SetAppEnabledMessage( string planName, AppIdTuple id, bool enabled )
		{
			this.PlanName = planName;
			this.Id = id;
			this.Enabled = enabled;
		}

		public override string ToString()
		{
			return string.Format( "SetAppEnabled [{0}] {1} {2}", PlanName, Id.ToString(), Enabled );
		}

	}

	//[ProtoBuf.ProtoContract]
	//[DataContract]
	//public class SelectPlanMessage : Message
	//{
	//    [ProtoBuf.ProtoMember(1)]
	//    [DataMember]
	//    public string planName;

	//    public SelectPlanMessage() {}
	//    public SelectPlanMessage( String planName )
	//    {
	//        this.plan = plan
	//    }

	//    public override string ToString()
	//    {
	//        return string.Format("SelectPlan {0}", plan.Name);
	//    }

	//}

	[ProtoBuf.ProtoContract]
	[DataContract]
	public class StartPlanMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public String PlanName = string.Empty;

		public StartPlanMessage() {}
		public StartPlanMessage( String planName )
		{
			this.PlanName = planName;
		}

		public override string ToString() { return string.Format( "StartPlan {0}", PlanName ); }
	}

	[ProtoBuf.ProtoContract]
	[DataContract]
	public class StopPlanMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public string PlanName = string.Empty;

		public StopPlanMessage() {}
		public StopPlanMessage( String planName )
		{
			this.PlanName = planName;
		}

		public override string ToString() { return string.Format( "StopPlan {0}", PlanName ); }
	}

	[ProtoBuf.ProtoContract]
	[DataContract]
	public class KillPlanMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public string PlanName = string.Empty;

		public KillPlanMessage() {}
		public KillPlanMessage( String planName )
		{
			this.PlanName = planName;
		}

		public override string ToString() { return string.Format( "KillPlan {0}", PlanName ); }
	}

	[ProtoBuf.ProtoContract]
	[DataContract]
	public class RestartPlanMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public string PlanName = string.Empty;

		public RestartPlanMessage() {}
		public RestartPlanMessage( String planName )
		{
			this.PlanName = planName;
		}

		public override string ToString() { return string.Format( "RestartPlan {0}", PlanName ); }
	}

	/// <summary>
	/// Master tells new client about the current launch plan
	/// </summary>
	[ProtoBuf.ProtoContract]
	[DataContract]
	public class CurrentPlanMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public string PlanName = string.Empty;

		public CurrentPlanMessage() {}
		public CurrentPlanMessage( String planName )
		{
			this.PlanName = planName;
		}

		public override string ToString()
		{
			return string.Format( "CurrentPlan {0}", PlanName );
		}
	}

	/// <summary>
	/// Master tells new client about existing plans
	/// </summary>
	[ProtoBuf.ProtoContract]
	[DataContract]
	public class PlanDefsMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public IEnumerable<PlanDef>? PlanDefs;

		public PlanDefsMessage() {}
		public PlanDefsMessage( IEnumerable<PlanDef> planDefs )
		{
			this.PlanDefs = planDefs;
		}

		public override string ToString()
		{
			return string.Format( "PlanDefs ({0} plans)", PlanDefs?.Count() );
		}
	}

	[ProtoBuf.ProtoContract]
	[DataContract]
	public class SetVarsMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public string Vars = string.Empty;

		public SetVarsMessage() {}
		public SetVarsMessage( string vars )
		{
			this.Vars = vars;
		}

		public override string ToString()
		{
			return string.Format( "SetVars {0}", Vars );
		}

	}


	[ProtoBuf.ProtoContract]
	[DataContract]
	public class KillAllMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public KillAllArgs Args;

		public KillAllMessage() {}
		public KillAllMessage( KillAllArgs args )
		{
			this.Args = args;
		}

		public override string ToString()
		{
			return string.Format( "KillAll" );
		}

	}

	[ProtoBuf.ProtoContract]
	[DataContract]
	public class TerminateMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public TerminateArgs Args;

		public TerminateMessage() {}
		public TerminateMessage( TerminateArgs args )
		{
			this.Args = args;
		}

		public override string ToString()
		{
			return string.Format( "Terminate killApps={0} machineId={1}", Args.KillApps, Args.MachineId );
		}

	}

	[ProtoBuf.ProtoContract]
	[DataContract]
	public class ShutdownMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public ShutdownArgs Args;

		public ShutdownMessage() {}
		public ShutdownMessage( ShutdownArgs args )
		{
			this.Args = args;
		}

		public override string ToString()
		{
			return string.Format( "Shutdown mode={0}", Args.Mode.ToString() );
		}

	}

	[ProtoBuf.ProtoContract]
	[DataContract]
	public class ReinstallMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public ReinstallArgs Args;

		public ReinstallMessage() {}
		public ReinstallMessage( ReinstallArgs args )
		{
			this.Args = args;
		}

		public override string ToString()
		{
			return string.Format( "Reinstall dwonloadMode={0}, url={1}", Args.DownloadMode.ToString(), Args.Url );
		}

	}

	[ProtoBuf.ProtoContract]
	[DataContract]
	public class ReloadSharedConfigMessage : Message
	{
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public ReloadSharedConfigArgs Args;

		public ReloadSharedConfigMessage() {}
		public ReloadSharedConfigMessage( ReloadSharedConfigArgs args )
		{
			this.Args = args;
		}

		public override string ToString()
		{
			return string.Format( "ReloadSharedConfig killApps={0}", Args.KillApps );
		}

	}

	[ProtoBuf.ProtoContract]
	[DataContract]
	public class ClientIdent : Message
	{
		///<summary>Client name. For agents, this equals the MachineId. For Guis this is a stringized GUID</summary>
		public string Name
		{
			get { return Sender; }
			set { Sender = value; }
		}

		//// unique name is enough?
		//[ProtoBuf.ProtoMember(1)]
		//[DataMember]
		//public Guid Uuid;

		[ProtoBuf.ProtoMember( 2 )]
		[DataMember]
		public EMsgRecipCateg SubscribedTo;

		//[ProtoBuf.ProtoMember(3)]
		//[DataMember]
		//public string MachineId; // just for agents


		public ClientIdent() {}

		public override string ToString()
		{
			return $"ClientInfo Name {Sender}, Type {(int)SubscribedTo}";
		}

	}

	[ProtoBuf.ProtoContract]
	[DataContract]
	public class AppDefsMessage : Message
	{

		private static List<AppDef> emptyDefs = new();

		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public IEnumerable<AppDef> AppDefs = emptyDefs;

		/// <summary>
		/// Whether the recipient shall descard any extra appdefs not contained in this message.
		/// </summary>
		[ProtoBuf.ProtoMember( 2 )]
		[DataMember]
		public bool Incremental;

		public AppDefsMessage() {}
		public AppDefsMessage( IEnumerable<AppDef> appDefs, bool incremental )
		{
			AppDefs = appDefs;
			Incremental = incremental;
		}
	}

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

	}

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

	}
}
