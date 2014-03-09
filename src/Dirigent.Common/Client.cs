using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;

namespace Dirigent.Net
{
    public class Client : IClient
    {
        string name;
        string ipaddr;
        int port;
        ServerRemoteObject serverObject;

        public string Name { get { return name;} }

        public Client( string name, string ipaddr, int port )
        {
            this.name = name;
            this.ipaddr = ipaddr;
            this.port = port;
        }
        
        public void Connect()
        {
            //System.Collections.IDictionary properties = new System.Collections.Hashtable();
            //properties["timeout"] = 500; // PJ: value not taken into account, connection timeout still aroun 1000msec

            //TcpChannel channel = new TcpChannel(
            //                        properties,
            //                        null,
            //                        new BinaryServerFormatterSinkProvider()
            //                        );

            TcpChannel channel = new TcpChannel();

            ChannelServices.RegisterChannel(channel, false);

            RemotingConfiguration.RegisterWellKnownClientType(
                typeof(ServerRemoteObject ),
                string.Format("tcp://{0}:{1}/Dirigent", ipaddr, port)
            );

            serverObject = new ServerRemoteObject();

            serverObject.AddClient( name );
        }

        public void Disconnect()
        {
            serverObject.RemoveClient( name );
        }

        public IEnumerable<Message> ReadMessages()
        {
            return serverObject.ClientMessages( name );
        }

        public void BroadcastMessage( Message msg )
        {
            serverObject.BroadcastMessage( name, msg );
        }
    }
}
