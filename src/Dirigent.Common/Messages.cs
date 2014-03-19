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
    public class RunAppMessage : Message
    {
        public AppIdTuple appIdTuple;

        public RunAppMessage( AppIdTuple appIdTuple )
        {
            this.appIdTuple = appIdTuple;
        }

    }

    [Serializable]
    public class KillAppMessage : Message
    {
        public AppIdTuple appIdTuple;

        public KillAppMessage( AppIdTuple appIdTuple )
        {
            this.appIdTuple = appIdTuple;
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
    }

    [Serializable]
    public class LoadPlanMessage : Message
    {
        public ILaunchPlan plan;

        public LoadPlanMessage( ILaunchPlan plan )
        {
            this.plan = plan;
        }

    }

    [Serializable]
    public class StartPlanMessage : Message
    {
    }

    [Serializable]
    public class StopPlanMessage : Message
    {
    }

    [Serializable]
    public class RestartPlanMessage : Message
    {
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

    }

}
