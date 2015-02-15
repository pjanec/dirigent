using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.ServiceModel;
using System.ServiceModel.Channels;

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

    public class MasterServiceClient : DuplexClientBase<IDirigentMasterContract>
    {
        public MasterServiceClient(object callbackInstance, Binding binding, EndpointAddress remoteAddress)
            : base(callbackInstance, binding, remoteAddress) { }
    }

    public class MasterServiceCallback : IDirigentMasterContractCallback
    {
        public void MessageFromServer(  Message msg )
        {
            //Console.WriteLine("Text from server: {0}", line);
        }
    }

    public class Client : IClient
    {
        string name;
        string ipaddr;
        int port;

        MasterServiceClient client;
        MasterServiceCallback callback;
        IDirigentMasterContract server;  // server proxy
        
        public string Name { get { return name;} }

        public Client( string name, string ipaddr, int port )
        {
            this.name = name;
            this.ipaddr = ipaddr;
            this.port = port;
        }
        
        public void Connect()
        {
            var uri = new Uri( string.Format("net.tcp://{0}:{1}", ipaddr, port) );
            var binding = new NetTcpBinding();
            //binding.SendTimeout = new TimeSpan(0,0,0,0,500); // shorten the timeout when accessing the service
            callback = new MasterServiceCallback();
            client = new MasterServiceClient(callback, binding, new EndpointAddress(uri));
            server = client.ChannelFactory.CreateChannel();

            try
            {
                server.AddClient(name);
            }
            catch
            {
                throw new MasterConnectionTimeoutException(ipaddr, port);
            }

        }

        public void Disconnect()
        {
            server.RemoveClient( name );
        }

        public IEnumerable<Message> ReadMessages()
        {
            try
            {
                return server.ClientMessages(name);
            }
            catch (KeyNotFoundException ex)
            {
                throw new UnknownClientName(name);
            }
        }

        public void BroadcastMessage( Message msg )
        {
            msg.Sender = name;
            server.BroadcastMessage( msg );
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
