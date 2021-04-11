using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Runtime.Serialization;

using Dirigent.Common;

namespace Dirigent.Net
{
    public static class TypeMapRegistry
    {
        static public Dictionary<uint, System.Type> TypeMap = new Dictionary<uint, Type>()
        {
            { 100, typeof(Message) },
            { 101, typeof(RemoteOperationErrorMessage) },
            { 102, typeof(AppsStateMessage) },
            { 103, typeof(PlansStateMessage) },
            { 104, typeof(LaunchAppMessage) },
            { 105, typeof(KillAppMessage) },
            { 106, typeof(RestartAppMessage) },
            { 107, typeof(SetAppEnabledMessage) },
            //{ 108, typeof(SelectPlanMessage) },
            { 109, typeof(StartPlanMessage) },
            { 110, typeof(StopPlanMessage) },
            { 111, typeof(KillPlanMessage) },
            { 112, typeof(RestartPlanMessage) },
            { 113, typeof(CurrentPlanMessage) },
            { 114, typeof(PlanRepoMessage) },
            { 115, typeof(SetVarsMessage) },
            { 116, typeof(KillAllMessage) },
            { 117, typeof(ShutdownMessage) },
            { 118, typeof(ReinstallMessage) },
            { 119, typeof(TerminateMessage) },
            { 120, typeof(ReloadSharedConfigMessage) },
        };
    }

    /// <summary>
    /// Base class for all messages.
    /// </summary>
    [ProtoBuf.ProtoContract]
    [DataContract]
    [KnownType(typeof(RemoteOperationErrorMessage)) , ProtoBuf.ProtoInclude(101, typeof(RemoteOperationErrorMessage))]
    [KnownType(typeof(AppsStateMessage))            , ProtoBuf.ProtoInclude(102, typeof(AppsStateMessage))]
    [KnownType(typeof(PlansStateMessage))           , ProtoBuf.ProtoInclude(103, typeof(PlansStateMessage))]
    [KnownType(typeof(LaunchAppMessage))            , ProtoBuf.ProtoInclude(104, typeof(LaunchAppMessage))]
    [KnownType(typeof(KillAppMessage))              , ProtoBuf.ProtoInclude(105, typeof(KillAppMessage))]
    [KnownType(typeof(RestartAppMessage))           , ProtoBuf.ProtoInclude(106, typeof(RestartAppMessage))]
    [KnownType(typeof(SetAppEnabledMessage))        , ProtoBuf.ProtoInclude(107, typeof(SetAppEnabledMessage))]
    //[KnownType(typeof(SelectPlanMessage))         , ProtoBuf.ProtoInclude(108, typeof(SelectPlanMessage))]
    [KnownType(typeof(StartPlanMessage))            , ProtoBuf.ProtoInclude(109, typeof(StartPlanMessage))]
    [KnownType(typeof(StopPlanMessage))             , ProtoBuf.ProtoInclude(110, typeof(StopPlanMessage))]
    [KnownType(typeof(KillPlanMessage))             , ProtoBuf.ProtoInclude(111, typeof(KillPlanMessage))]
    [KnownType(typeof(RestartPlanMessage))          , ProtoBuf.ProtoInclude(112, typeof(RestartPlanMessage))]
    [KnownType(typeof(CurrentPlanMessage))          , ProtoBuf.ProtoInclude(113, typeof(CurrentPlanMessage))]
    [KnownType(typeof(PlanRepoMessage))             , ProtoBuf.ProtoInclude(114, typeof(PlanRepoMessage))]
    [KnownType(typeof(SetVarsMessage))              , ProtoBuf.ProtoInclude(115, typeof(SetVarsMessage))]
    [KnownType(typeof(KillAllMessage))              , ProtoBuf.ProtoInclude(116, typeof(KillAllMessage))]
    [KnownType(typeof(ShutdownMessage))             , ProtoBuf.ProtoInclude(117, typeof(ShutdownMessage))]
    [KnownType(typeof(ReinstallMessage))            , ProtoBuf.ProtoInclude(118, typeof(ReinstallMessage))]
    [KnownType(typeof(TerminateMessage))            , ProtoBuf.ProtoInclude(119, typeof(TerminateMessage))]
    [KnownType(typeof(ReloadSharedConfigMessage))   , ProtoBuf.ProtoInclude(120, typeof(ReloadSharedConfigMessage))]
    public class Message
    {
        [ProtoBuf.ProtoMember(1)]
        [DataMember]
        public string Sender { get; set; }

        public static void RegisterProtobufTypeMaps()
        {
			ProtoBuf.Meta.RuntimeTypeModel.Default.Add(typeof (ILaunchPlan), true).AddSubType(50, typeof(LaunchPlan));
        }
    }

    [ProtoBuf.ProtoContract]
    [DataContract]
    public class RemoteOperationErrorMessage : Message
    {
        [ProtoBuf.ProtoMember(1)]
        [DataMember]
        public string Requestor;
        
        [ProtoBuf.ProtoMember(2)]
        [DataMember]
        public string Message; // Error description 
        
        [ProtoBuf.ProtoMember(3)]
        [DataMember]
        public Dictionary<string, string> Attributes; // additional attribute pairs (name, value)

        public RemoteOperationErrorMessage() {}
        public RemoteOperationErrorMessage(string requestor, string msg, Dictionary<string, string> attribs = null)
        {
            this.Requestor = requestor;
            this.Message = msg;
            if( attribs != null )
            {
                this.Attributes = attribs;
            }
        }
    }

    // not used just if multicast option is enabled (like in case of many agents;
    // this msg causes the master to resend to all clients (as in case of any other message) - heavy network load as this message is frequent!
    [ProtoBuf.ProtoContract]
    [DataContract]
    public class AppsStateMessage : Message
    {
        [ProtoBuf.ProtoMember(1)]
        [DataMember]
        public Dictionary<AppIdTuple, AppState> appsState;

        public AppsStateMessage() {}
        public AppsStateMessage( Dictionary<AppIdTuple, AppState> appsState )
        {
            this.appsState = new Dictionary<AppIdTuple, AppState>(appsState);
        }
    }

    [ProtoBuf.ProtoContract]
    [DataContract]
    public class PlansStateMessage : Message
    {
        [ProtoBuf.ProtoMember(1)]
        [DataMember]
        public Dictionary<string, PlanState> plansState;

        public PlansStateMessage() {}
        public PlansStateMessage( Dictionary<string, PlanState> plansState )
        {
            this.plansState = new Dictionary<string, PlanState>(plansState);
        }
    }

    [ProtoBuf.ProtoContract]
    [DataContract]
    public class LaunchAppMessage : Message
    {
        [ProtoBuf.ProtoMember(1)]
        [DataMember]
        public AppIdTuple appIdTuple;

        public LaunchAppMessage() {}
        public LaunchAppMessage( AppIdTuple appIdTuple )
        {
            this.appIdTuple = appIdTuple;
        }

        public override string ToString()
        {
            return string.Format("StartApp {0}", appIdTuple.ToString());
        }

    }

    [ProtoBuf.ProtoContract]
    [DataContract]
    public class KillAppMessage : Message
    {
        [ProtoBuf.ProtoMember(1)]
        [DataMember]
        public AppIdTuple appIdTuple;

        public KillAppMessage() {}
        public KillAppMessage( AppIdTuple appIdTuple )
        {
            this.appIdTuple = appIdTuple;
        }

        public override string ToString()
        {
            return string.Format("StopApp {0}", appIdTuple.ToString());
        }

    }

    [ProtoBuf.ProtoContract]
    [DataContract]
    public class RestartAppMessage : Message
    {
        [ProtoBuf.ProtoMember(1)]
        [DataMember]
        public AppIdTuple appIdTuple;

        public RestartAppMessage() {}
        public RestartAppMessage( AppIdTuple appIdTuple )
        {
            this.appIdTuple = appIdTuple;
        }
        public override string ToString()
        {
            return string.Format("RestartApp {0}", appIdTuple.ToString());
        }

    }

    [ProtoBuf.ProtoContract]
    [DataContract]
    public class SetAppEnabledMessage : Message
    {
        [ProtoBuf.ProtoMember(1)]
        [DataMember]
        public string planName;

        [ProtoBuf.ProtoMember(2)]
        [DataMember]
        public AppIdTuple appIdTuple;

        [ProtoBuf.ProtoMember(3)]
        [DataMember]
        public bool enabled;

        public SetAppEnabledMessage() {}
        public SetAppEnabledMessage( string planName, AppIdTuple appIdTuple, bool enabled )
        {
            this.planName = planName;    
            this.appIdTuple = appIdTuple;
            this.enabled = enabled;
        }

        public override string ToString()
        {
            return string.Format("SetAppEnabled [{0}] {1} {2}", planName, appIdTuple.ToString(), enabled);
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
        [ProtoBuf.ProtoMember(1)]
        [DataMember]
        public String planName;

        public StartPlanMessage() {}
        public StartPlanMessage( String planName )
        {
            this.planName = planName;
        }

		public override string ToString() { return string.Format("StartPlan {0}", planName); }
    }

    [ProtoBuf.ProtoContract]
    [DataContract]
    public class StopPlanMessage : Message
    {
        [ProtoBuf.ProtoMember(1)]
        [DataMember]
        public string planName;

        public StopPlanMessage() {}
        public StopPlanMessage( String planName )
        {
            this.planName = planName;
        }

		public override string ToString() { return string.Format("StopPlan {0}", planName); }
    }

    [ProtoBuf.ProtoContract]
    [DataContract]
    public class KillPlanMessage : Message
    {
        [ProtoBuf.ProtoMember(1)]
        [DataMember]
        public string planName;

        public KillPlanMessage() {}
        public KillPlanMessage( String planName )
        {
            this.planName = planName;
        }

		public override string ToString() { return string.Format("KillPlan {0}", planName); }
    }

    [ProtoBuf.ProtoContract]
    [DataContract]
    public class RestartPlanMessage : Message
    {
        [ProtoBuf.ProtoMember(1)]
        [DataMember]
        public string planName;

        public RestartPlanMessage() {}
        public RestartPlanMessage( String planName )
        {
            this.planName = planName;
        }

		public override string ToString() { return string.Format("RestartPlan {0}", planName); }
    }

	/// <summary>
	/// Master tells new client about the current launch plan
	/// </summary>
	[ProtoBuf.ProtoContract]
    [DataContract]
	public class CurrentPlanMessage : Message
	{
        [ProtoBuf.ProtoMember(1)]
		[DataMember]
		public string planName;

        public CurrentPlanMessage() {}
		public CurrentPlanMessage(String planName)
		{
			this.planName = planName;
		}

		public override string ToString()
		{
			return string.Format("CurrentPlan {0}", planName);
		}
	}

	/// <summary>
	/// Master tells new client about existing plans
	/// </summary>
    [ProtoBuf.ProtoContract]
	[DataContract]
    public class PlanRepoMessage : Message
    {
        [ProtoBuf.ProtoMember(1)]
        [DataMember]
        public IEnumerable<ILaunchPlan> repo;

        public PlanRepoMessage() {}
        public PlanRepoMessage(IEnumerable<ILaunchPlan> repo)
        {
            this.repo = repo;
        }

        public override string ToString()
        {
            return string.Format("PlanRepo ({0} plans)", repo.Count());
        }
    }

    [ProtoBuf.ProtoContract]
    [DataContract]
    public class SetVarsMessage : Message
    {
        [ProtoBuf.ProtoMember(1)]
        [DataMember]
        public string vars;

        public SetVarsMessage() {}
        public SetVarsMessage( string vars )
        {
            this.vars = vars;    
        }

        public override string ToString()
        {
            return string.Format("SetVars {0}", vars);
        }

    }


    [ProtoBuf.ProtoContract]
    [DataContract]
    public class KillAllMessage : Message
    {
        [ProtoBuf.ProtoMember(1)]
        [DataMember]
        public KillAllArgs args;

        public KillAllMessage() {}
        public KillAllMessage( KillAllArgs args )
        {
            this.args = args;    
        }

        public override string ToString()
        {
            return string.Format("KillAll");
        }

    }

    [ProtoBuf.ProtoContract]
    [DataContract]
    public class TerminateMessage : Message
    {
        [ProtoBuf.ProtoMember(1)]
        [DataMember]
        public TerminateArgs args;

        public TerminateMessage() {}
        public TerminateMessage( TerminateArgs args )
        {
            this.args = args;    
        }

        public override string ToString()
        {
            return string.Format("Terminate killApps={0} machineId={1}", args.KillApps, args.MachineId);
        }

    }

    [ProtoBuf.ProtoContract]
    [DataContract]
    public class ShutdownMessage : Message
    {
        [ProtoBuf.ProtoMember(1)]
        [DataMember]
        public ShutdownArgs args;

        public ShutdownMessage() {}
        public ShutdownMessage( ShutdownArgs args )
        {
            this.args = args;    
        }

        public override string ToString()
        {
            return string.Format("Shutdown mode={0}", args.Mode.ToString());
        }

    }

    [ProtoBuf.ProtoContract]
    [DataContract]
    public class ReinstallMessage : Message
    {
        [ProtoBuf.ProtoMember(1)]
        [DataMember]
        public ReinstallArgs args;

        public ReinstallMessage() {}
        public ReinstallMessage( ReinstallArgs args )
        {
            this.args = args;    
        }

        public override string ToString()
        {
            return string.Format("Reinstall dwonloadMode={0}, url={1}", args.DownloadMode.ToString(), args.Url);
        }

    }

    [ProtoBuf.ProtoContract]
    [DataContract]
    public class ReloadSharedConfigMessage : Message
    {
        [ProtoBuf.ProtoMember(1)]
        [DataMember]
        public ReloadSharedConfigArgs args;

        public ReloadSharedConfigMessage() {}
        public ReloadSharedConfigMessage( ReloadSharedConfigArgs args )
        {
            this.args = args;    
        }

        public override string ToString()
        {
            return string.Format("ReloadSharedConfig killApps={0}", args.KillApps);
        }

    }

}
