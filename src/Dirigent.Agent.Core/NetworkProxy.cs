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
    public class NetworkProxy : IDirigentControl
    {
        string machineId;
        IClient client;
        IDirigentControl localOps;


        /// <summary>
        /// Client needs to be already connected or autoconnecting!
        /// </summary>
        /// <param name="client"></param>
        /// <param name="localOps"></param>
        public NetworkProxy(
                    string machineId,
                    IClient client,
                    IDirigentControl localOps )
        {
                
            this.machineId = machineId;
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

        void processIncomingMessage(Message msg)
        {
            Type t = msg.GetType();

            if (t == typeof(AppsStateMessage))
            {
                var m = msg as AppsStateMessage;
                updateRemoteAppState(m.appsState);
            }
            else
            if (t == typeof(SelectPlanMessage))
            {
                var m = msg as SelectPlanMessage;
                localOps.SelectPlan(m.plan);
            }
            else
            if (t == typeof(LaunchAppMessage))
            {
                var m = msg as LaunchAppMessage;
                if (m.appIdTuple.MachineId == machineId)
                {
                    localOps.LaunchApp(m.appIdTuple);
                }
            }
            else
            if (t == typeof(KillAppMessage))
            {
                var m = msg as KillAppMessage;
                if (m.appIdTuple.MachineId == machineId)
                {
                    localOps.KillApp(m.appIdTuple);
                }
            }
            else
            if (t == typeof(RestartAppMessage))
            {
                var m = msg as RestartAppMessage;
                if (m.appIdTuple.MachineId == machineId)
                {
                    localOps.RestartApp(m.appIdTuple);
                }
            }
            else
            if (t == typeof(StartPlanMessage))
            {
                var m = msg as StartPlanMessage;
                localOps.StartPlan();
            }
            else
            if (t == typeof(StopPlanMessage))
            {
                var m = msg as StopPlanMessage;
                localOps.StopPlan();
            }
            else
            if (t == typeof(KillPlanMessage))
            {
                var m = msg as KillPlanMessage;
                localOps.KillPlan();
            }
            else
            if (t == typeof(RestartPlanMessage))
            {
                var m = msg as RestartPlanMessage;
                localOps.RestartPlan();
            }
            else
            if (t == typeof(CurrentPlanMessage))
            {
                var m = msg as CurrentPlanMessage;

                // if master's plan is same as ours, do not do anything, othewise load master's plan
                var localPlan = localOps.GetCurrentPlan();
                if (m.plan != null && (localPlan == null || !m.plan.Equals(localPlan)))
                {
                    localOps.SelectPlan(m.plan);
                }
            }
            else
            if (t == typeof(PlanRepoMessage))
            {
                var m = msg as PlanRepoMessage;
                localOps.SetPlanRepo(m.repo);
            }
            else
            if (t == typeof(RemoteOperationErrorMessage))
            {
                var m = msg as RemoteOperationErrorMessage;
                throw new RemoteOperationErrorException(m.Requestor, m.Message, m.Attributes);
            }
        }

        void processIncomingMessages()
        {
            foreach( var msg in client.ReadMessages() )
            {
                try
                {
                    processIncomingMessage(msg);
                }
                catch (RemoteOperationErrorException) // an error from another agent received
                {
                    throw; // just forward up the stack, DO NOT broadcast an error msg (would cause an endless loop & network flooding)
                }
                catch (Exception ex) // some local operation error as a result of remote request from another agent
                {
                    // send an error message to agents
                    // the requestor is supposed to present an error message to the user
                    client.BroadcastMessage(
                        new RemoteOperationErrorMessage(
                                msg.Sender, // agent that requested the local operation here
                                ex.Message, // description of the problem
                                new Dictionary<string, string>() // additional info to the problem
                                {
                                    { "Exception", ex.ToString() }
                                }
                        )
                    );
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

        public void SelectPlan(ILaunchPlan plan)
        {
            client.BroadcastMessage( new SelectPlanMessage( plan ) );
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
            client.BroadcastMessage(new StopPlanMessage());
        }

        public void KillPlan()
        {
            client.BroadcastMessage( new KillPlanMessage() );
        }

        public void RestartPlan()
        {
            client.BroadcastMessage( new RestartPlanMessage() );
        }

        public void LaunchApp(AppIdTuple appIdTuple)
        {
            client.BroadcastMessage( new LaunchAppMessage( appIdTuple ) );
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
