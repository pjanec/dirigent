using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;

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
        public Server( int port )
        {
            this.port = port;

            TcpChannel channel = new TcpChannel(port);
            ChannelServices.RegisterChannel(channel, false);
            RemotingConfiguration.RegisterWellKnownServiceType(
                typeof(ServerRemoteObject), 
                "Dirigent",
                WellKnownObjectMode.Singleton);            
        }
    }
}
