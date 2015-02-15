using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;


using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Description;

using Dirigent.Common;

namespace Dirigent.Net
{

    public class Server // WCF serve
    {
        int port;

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The "message broker" for forwarding messages to clients.
        /// Just instantiate the class to make the server working.
        /// The clients remotely access the MasterService via the IDirigentMasterContract interface.
        /// </summary>
        /// <param name="port"></param>
        public Server( int port, IEnumerable<ILaunchPlan> planRepo=null, string startupPlanName="" )
        {
            this.port = port;

            var uri = new Uri( string.Format("net.tcp://0.0.0.0:{0}", port) );
            var binding = new NetTcpBinding();
            var service = new MasterService();
            var host = new ServiceHost( service, uri);
            var endpoint = host.AddServiceEndpoint(typeof(IDirigentMasterContract), binding, "");
            //endpoint.Behaviors.Add(new ClientTrackerEndpointBehavior());
            host.Open(); // never closed as the server runs forever

            // although there can't be any clients connected, this caches the planRepo internally
            // this cached one is then sent to the client when it first connects
            if (planRepo != null)
            {
                log.InfoFormat("Forcing plan repository ({0} items)", planRepo.Count() );
                service.BroadcastMessage("<master>", new PlanRepoMessage(planRepo));
            }

            // start the initial launch plan if specified
            if (planRepo != null && startupPlanName != null && startupPlanName != "")
            {
                ILaunchPlan startupPlan;
                try
                {
                     startupPlan = planRepo.First((i) => i.Name == startupPlanName);
                }
                catch
                {
                    throw new UnknownPlanName(startupPlanName);
                }

                log.InfoFormat("Forcing plan '{0}'", startupPlan.Name);
                service.BroadcastMessage("<master>", new CurrentPlanMessage(startupPlan));
            }
        
        }
    }


}
