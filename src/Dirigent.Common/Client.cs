using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;


namespace Dirigent.Net
{
    public class MasterConnectionTimeoutException : Exception
    {
        public string ip;
        public int port;

        public MasterConnectionTimeoutException(string ip, int port)
            : base(string.Format("Failed to connect to master {0}:{1}", ip, port))
        {
            this.ip = ip;
            this.port = port;
        }
    }

    public class UnknownClientName : Exception
    {
        public string name;

        public UnknownClientName(string name)
            : base(string.Format("Unknown client name '{0}'", name))
        {
            this.name =name;
        }
    }

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

            serverObject = ServerRemoteObject.Instance;

            try
            {
                serverObject.AddClient(name);
            }
            catch
            {
                throw new MasterConnectionTimeoutException(ipaddr, port);
            }

        }

        public void Disconnect()
        {
            serverObject.RemoveClient( name );
        }

        public IEnumerable<Message> ReadMessages()
        {
            try
            {
                return serverObject.ClientMessages(name);
            }
            catch (KeyNotFoundException ex)
            {
                throw new UnknownClientName(name);
            }
        }

        public void BroadcastMessage( Message msg )
        {
            msg.Sender = name;
            serverObject.BroadcastMessage( msg );
        }

        public bool IsConnected()
        {
            // we don't know whether wqe are still connected, we throw exceptions if not, just return true
            return true;
        }

        public void Dispose()
        {
        }
    }
}
