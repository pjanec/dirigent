using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Common;
using Dirigent.Net;

namespace Dirigent.Common
{
    /// <summary>
    /// Allows to control the dirigent over the network.
    /// To be used by a control GUI for example.
    /// 
    /// Uses Client to be part of the communication network.
    /// Caches the state of all apps.
    /// </summary>
    public class NetworkControl : IDirigentControl
    {
        /// <summary>
        /// public state of all apps from current launch plan
        /// </summary>
        Dictionary<AppIdTuple, AppState> appsState;


        IClient client;
        ILaunchPlan plan;
        
        public NetworkControl( IClient client )
        {
            this.client = client;
            appsState = new Dictionary<AppIdTuple, AppState>();
        }

        void updateRemoteAppState( Dictionary<AppIdTuple, AppState> remoteAppsState )
        {
            foreach( KeyValuePair<AppIdTuple, AppState> kvp in remoteAppsState )
            {
                appsState[kvp.Key] = kvp.Value;
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
                //else
                //if( t == typeof(LoadPlanMessage) )
                //{
                //    var m = msg as LoadPlanMessage;
                //    localOps.LoadPlan( m.plan );
                //}
                //else
                //if( t == typeof(RunAppMessage) )
                //{
                //    var m = msg as RunAppMessage;
                //    localOps.RunApp( m.appIdTuple );
                //}
                //else
                //if( t == typeof(KillAppMessage) )
                //{
                //    var m = msg as KillAppMessage;
                //    localOps.KillApp( m.appIdTuple );
                //}
                //else
                //if( t == typeof(RestartAppMessage) )
                //{
                //    var m = msg as RestartAppMessage;
                //    localOps.RestartApp( m.appIdTuple );
                //}
                //else
                //if( t == typeof(StartPlanMessage) )
                //{
                //    var m = msg as StartPlanMessage;
                //    localOps.StartPlan();
                //}
                //else
                //if( t == typeof(StopPlanMessage) )
                //{
                //    var m = msg as StopPlanMessage;
                //    localOps.StopPlan();
                //}
                //else
                //if( t == typeof(RestartPlanMessage) )
                //{
                //    var m = msg as RestartPlanMessage;
                //    localOps.RestartPlan();
                //}
            }
        }

        public AppState GetAppState(AppIdTuple appIdTuple)
        {
            return appsState[appIdTuple];
        }

        public void SetAppState(AppIdTuple appIdTuple, AppState state)
        {
            // makes no sense
            throw new NotImplementedException();
        }

        public void LoadPlan(ILaunchPlan plan)
        {
            this.plan = plan;
            client.BroadcastMessage( new LoadPlanMessage( plan ) );
        }

        public ILaunchPlan GetPlan()
        {
            return plan;
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
