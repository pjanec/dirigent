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

        /// <summary>
        /// The "message broker" for forwarding messages to clients.
        /// Just instantiate the class to make the server working.
        /// The clients remotely access the ServerRemoteObject.
        /// </summary>
        /// <param name="port"></param>
        public Server( int port, IEnumerable<ILaunchPlan> planRepo=null )
        {
            this.port = port;

            TcpChannel channel = new TcpChannel(port);
            ChannelServices.RegisterChannel(channel, false);
            
            // makes the single instance available to all the clients
            var rem = RemotingServices.Marshal(ServerRemoteObject.Instance, "Dirigent");
            
            // although there can't be any clients connected, this caches the planRepo internally
            // this cached one is then sent to the client when it first connects
            ServerRemoteObject.Instance.BroadcastMessage("<master>", new PlanRepoMessage(planRepo));
        }
    }
}
