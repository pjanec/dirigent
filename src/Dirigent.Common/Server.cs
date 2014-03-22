using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;

using Dirigent.Common;

namespace Dirigent.Net
{
    public class Server
    {
        int port;

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// The "message broker" for forwarding messages to clients.
        /// Just instantiate the class to make the server working.
        /// The clients remotely access the ServerRemoteObject.
        /// </summary>
        /// <param name="port"></param>
        public Server( int port, IEnumerable<ILaunchPlan> planRepo=null, string startupPlanName="" )
        {
            this.port = port;

            TcpChannel channel = new TcpChannel(port);
            ChannelServices.RegisterChannel(channel, false);
            
            // makes the single instance available to all the clients
            var rem = RemotingServices.Marshal(ServerRemoteObject.Instance, "Dirigent");

            ServerRemoteObject.Instance.SetInactivityTimeOut(1000);
            
            // although there can't be any clients connected, this caches the planRepo internally
            // this cached one is then sent to the client when it first connects
            if (planRepo != null)
            {
                log.InfoFormat("Forcing plan repository ({0} items)", planRepo.Count() );
                ServerRemoteObject.Instance.BroadcastMessage("<master>", new PlanRepoMessage(planRepo));
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
                ServerRemoteObject.Instance.BroadcastMessage("<master>", new CurrentPlanMessage(startupPlan));
            }
        
        }
    }
}
