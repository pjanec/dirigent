﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;


using Dirigent.Common;

namespace Dirigent.Net
{

    public class Server 
    {
        int port;
/*
        MasterService service;
        ServiceHost host;
*/

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public static string MasterSenderName = "<master>";
			/// <summary>
        /// The "message broker" for forwarding messages to clients.
        /// Just instantiate the class to make the server working.
        /// The clients remotely access the MasterService via the IDirigentMasterContract interface.
        /// </summary>
        /// <param name="port"></param>
        public Server( int port, IDirigentControl localAgent, IEnumerable<ILaunchPlan> planRepo=null, string startupPlanName="" )
        {
            this.port = port;
/*
            var uri = new Uri( string.Format("net.tcp://0.0.0.0:{0}", port) );
            var binding = new NetTcpBinding();
			binding.Name = "MasterConnBinding";
            binding.MaxReceivedMessageSize =  Int32.MaxValue; // default 65535 is not enough for long plans
			binding.Security.Mode = SecurityMode.None;
            service = new MasterService(localAgent);
            host = new ServiceHost( service, uri);
            var endpoint = host.AddServiceEndpoint(typeof(IDirigentMasterContract), binding, "");
            //endpoint.Behaviors.Add(new ClientTrackerEndpointBehavior());
            //endpoint.Behaviors.Add( new ProtoBuf.ServiceModel.ProtoEndpointBehavior() );
			//foreach (var op in endpoint.Contract.Operations)
			//{
			//             DataContractSerializerOperationBehavior dcsBehavior = op.Behaviors.Find<DataContractSerializerOperationBehavior>();
			//             if (dcsBehavior != null)
			//                 op.Behaviors.Remove(dcsBehavior);
			//             op.Behaviors.Add(new ProtoBuf.ServiceModel.ProtoOperationBehavior(op));
			//}
            //Dirigent.Net.Message.RegisterProtobufTypeMaps();

			host.Open();

            // although there can't be any clients connected, this caches the planRepo internally
            // this cached one is then sent to the client when it first connects
            if (planRepo != null)
            {
                log.InfoFormat("Forcing plan repository ({0} items)", planRepo.Count() );
                service.BroadcastMessage(MasterSenderName, new PlanRepoMessage(planRepo));
            }

            // start the initial launch plan if specified
            if (planRepo != null && startupPlanName != null && startupPlanName != "")
            {
                ILaunchPlan startupPlan = null;
                startupPlan = planRepo.FirstOrDefault((i) => i.Name == startupPlanName);
                if( startupPlan != null )
                {
	                log.InfoFormat("Forcing plan '{0}'", startupPlanName);
					service.BroadcastMessage(MasterSenderName, new CurrentPlanMessage(startupPlanName));
                }
                else
				{
                    log.ErrorFormat("Unknown default plan name '{0}'", startupPlanName);
				}
            }
*/        
        }

		public void Dispose()
		{
/*
            host.Close();
            service.Dispose();
*/
        }

    }


}
