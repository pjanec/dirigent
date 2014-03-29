using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Common;

namespace Dirigent.Net
{
    /// <summary>
    /// Base class for all messages.
    /// </summary>
    [Serializable]
    public class Message
    {
        public string Sender { get; set; }
    }

    [Serializable]
    public class RemoteOperationErrorMessage : Message
    {
        public string Requestor;
        public string Message; // Error description 
        public Dictionary<string, string> Attributes; // additional attribute pairs (name, value)

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

    [Serializable]
    public class AppsStateMessage : Message
    {
        public Dictionary<AppIdTuple, AppState> appsState;

        public AppsStateMessage( Dictionary<AppIdTuple, AppState> appsState )
        {
            this.appsState = new Dictionary<AppIdTuple, AppState>(appsState);
        }
    }

    [Serializable]
    public class StartAppMessage : Message
    {
        public AppIdTuple appIdTuple;

        public StartAppMessage( AppIdTuple appIdTuple )
        {
            this.appIdTuple = appIdTuple;
        }

        public override string ToString()
        {
            return string.Format("StartApp {0}", appIdTuple.ToString());
        }

    }

    [Serializable]
    public class StopAppMessage : Message
    {
        public AppIdTuple appIdTuple;

        public StopAppMessage( AppIdTuple appIdTuple )
        {
            this.appIdTuple = appIdTuple;
        }

        public override string ToString()
        {
            return string.Format("StopApp {0}", appIdTuple.ToString());
        }

    }

    [Serializable]
    public class RestartAppMessage : Message
    {
        public AppIdTuple appIdTuple;

        public RestartAppMessage( AppIdTuple appIdTuple )
        {
            this.appIdTuple = appIdTuple;
        }
        public override string ToString()
        {
            return string.Format("RestartApp {0}", appIdTuple.ToString());
        }

    }

    [Serializable]
    public class LoadPlanMessage : Message
    {
        public ILaunchPlan plan;

        public LoadPlanMessage( ILaunchPlan plan )
        {
            this.plan = plan;
        }

        public override string ToString()
        {
            return string.Format("LoadPlan {0}", plan.Name);
        }

    }

    [Serializable]
    public class StartPlanMessage : Message
    {
        public override string ToString() { return "StartPlan"; }
    }

    [Serializable]
    public class StopPlanMessage : Message
    {
        public override string ToString() { return "StopPlan"; }
    }

    [Serializable]
    public class RestartPlanMessage : Message
    {
        public override string ToString() { return "RestartPlan"; }
    }

    /// <summary>
    /// Master tells new client about the current launch plan
    /// </summary>
    [Serializable]
    public class CurrentPlanMessage : Message
    {
        public ILaunchPlan plan;

        public CurrentPlanMessage(ILaunchPlan plan)
        {
            this.plan = plan;
        }

        public override string ToString()
        {
            return string.Format("CurrentPlan {0}", plan.Name);
        }
    }

    /// <summary>
    /// Master tells new client about existing plans
    /// </summary>
    [Serializable]
    public class PlanRepoMessage : Message
    {
        public IEnumerable<ILaunchPlan> repo;

        public PlanRepoMessage(IEnumerable<ILaunchPlan> repo)
        {
            this.repo = repo;
        }

        public override string ToString()
        {
            return string.Format("PlanRepo ({0} plans)", repo.Count());
        }
    }

}
