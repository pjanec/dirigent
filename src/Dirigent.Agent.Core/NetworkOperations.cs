using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Common;
using Dirigent.Net;

namespace Dirigent.Agent.Core
{
    /// <summary>
    /// Handles communication with master
    ///  - connection to master
    ///  - reception of messages from master
    ///  - publishing info to master
    /// </summary>
    public class NetworkOperations : IDirigentControl
    {
        string machineId;
        IClient client;
        IDirigentControl localOps;


        /// <summary>
        /// Client needs to be already connected or autoconnecting!
        /// </summary>
        /// <param name="client"></param>
        /// <param name="localOps"></param>
        public NetworkOperations(
                    IClient client,
                    IDirigentControl localOps )
        {
                
            this.machineId = client.Name;
            this.client = client;
            this.localOps = localOps;

        }

        void publishLocalAppState()
        {
            // get status of local apps and send to others
            Dictionary<AppIdTuple, AppState> localAppsState = new Dictionary<AppIdTuple, AppState>();
            
            var plan = localOps.GetCurrentPlan();
            if( plan == null ) return;

            foreach( var appDef in plan.getAppDefs() )
            {
                if( appDef.AppIdTuple.MachineId == machineId )
                {
                    localAppsState[appDef.AppIdTuple] = localOps.GetAppState( appDef.AppIdTuple );
                }
            }

            client.BroadcastMessage( new AppsStateMessage( localAppsState ) );

        }

        void updateRemoteAppState( Dictionary<AppIdTuple, AppState> remoteAppsState )
        {
            foreach( KeyValuePair<AppIdTuple, AppState> kvp in remoteAppsState )
            {
                if( kvp.Key.MachineId != machineId )
                {
                    localOps.SetRemoteAppState( kvp.Key, kvp.Value );
                }
            }            
        }

        void processIncomingMessages()
        {
            foreach( var msg in client.ReadMessages() )
            {
                Type t = msg.GetType();
                
                if( t == typeof(AppsStateMessage) )
                {
                    var m = msg as AppsStateMessage;
                    updateRemoteAppState( m.appsState );
                }
                else
                if( t == typeof(LoadPlanMessage) )
                {
                    var m = msg as LoadPlanMessage;
                    localOps.LoadPlan( m.plan );
                }
                else
                if( t == typeof(RunAppMessage) )
                {
                    var m = msg as RunAppMessage;
                    localOps.RunApp( m.appIdTuple );
                }
                else
                if( t == typeof(KillAppMessage) )
                {
                    var m = msg as KillAppMessage;
                    localOps.KillApp( m.appIdTuple );
                }
                else
                if( t == typeof(RestartAppMessage) )
                {
                    var m = msg as RestartAppMessage;
                    localOps.RestartApp( m.appIdTuple );
                }
                else
                if( t == typeof(StartPlanMessage) )
                {
                    var m = msg as StartPlanMessage;
                    localOps.StartPlan();
                }
                else
                if( t == typeof(StopPlanMessage) )
                {
                    var m = msg as StopPlanMessage;
                    localOps.StopPlan();
                }
                else
                if( t == typeof(RestartPlanMessage) )
                {
                    var m = msg as RestartPlanMessage;
                    localOps.RestartPlan();
                }
                else
                if (t == typeof(CurrentPlanMessage))
                {
                    var m = msg as CurrentPlanMessage;
                    localOps.LoadPlan( m.plan );
                }
                else
                if (t == typeof(PlanRepoMessage))
                {
                    var m = msg as PlanRepoMessage;
                    localOps.SetPlanRepo( m.repo );
                }

            }
        }
        
        public void tick( double currentTime )
        {
            publishLocalAppState();
            processIncomingMessages();
        }

        //
        // Implementation of IDirigentControl
        //
        public AppState GetAppState(AppIdTuple appIdTuple)
        {
            return localOps.GetAppState(appIdTuple);
        }

        public void SetRemoteAppState(AppIdTuple appIdTuple, AppState state )
        {
            localOps.SetRemoteAppState(appIdTuple, state);
        }

        public void LoadPlan(ILaunchPlan plan)
        {
            client.BroadcastMessage( new LoadPlanMessage( plan ) );
        }

        public ILaunchPlan GetCurrentPlan()
        {
            return localOps.GetCurrentPlan();
        }

        public IEnumerable<ILaunchPlan> GetPlanRepo()
        {
            return localOps.GetPlanRepo();
        }

        public void SetPlanRepo(IEnumerable<ILaunchPlan> planRepo)
        {
            client.BroadcastMessage( new PlanRepoMessage( planRepo ) );
        }


        public void StartPlan()
        {
            client.BroadcastMessage( new StartPlanMessage() );
        }

        public void StopPlan()
        {
            client.BroadcastMessage( new StopPlanMessage() );
        }

        public void RestartPlan()
        {
            client.BroadcastMessage( new RestartPlanMessage() );
        }

        public void RunApp(AppIdTuple appIdTuple)
        {
            client.BroadcastMessage( new RunAppMessage( appIdTuple ) );
        }

        public void RestartApp(AppIdTuple appIdTuple)
        {
            client.BroadcastMessage( new RestartAppMessage( appIdTuple ) );
        }

        public void KillApp(AppIdTuple appIdTuple)
        {
            client.BroadcastMessage( new KillAppMessage( appIdTuple ) );
        }
    }
}
