using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using Dirigent.Common;

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
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        string name;
        string ipaddr;
        int port;
        string mcastIP;
        int mcastPort;
        string localIP;
		int timeoutMs;

        MasterServiceClient client;
        MasterServiceCallback callback;
        IDirigentMasterContract server;  // server proxy
        
        public string Name { get { return name;} }

		public string MasterIP { get { return this.ipaddr; } }
		public int MasterPort { get { return this.port; } }
		public string McastIP { get { return this.mcastIP; } }
		public int McastPort { get { return this.mcastPort; } }
		public string LocalIP { get { return this.localIP; } }

        public Client( string name, string ipaddr, int port, string mcastIP, int mcastPort, string localIP, int timeoutMs=5000 )
        {
            this.name = name;
            this.ipaddr = ipaddr;
            this.port = port;
            this.mcastIP = mcastIP;
            this.mcastPort = mcastPort;
            this.localIP = localIP;
			this.timeoutMs = timeoutMs;
        }
        
        public void Connect()
        {
            var uri = new Uri( string.Format("net.tcp://{0}:{1}", ipaddr, port) );
            var binding = new NetTcpBinding();
			binding.Name = "MasterConnBinding";
            //binding.SendTimeout = new TimeSpan(0,0,0,0,timeoutMs); // shorten the timeout when accessing the service
            binding.CloseTimeout = new TimeSpan(0,0,0,0,timeoutMs); // shorten the timeout when closing the channel and there is an error
            binding.MaxReceivedMessageSize =  Int32.MaxValue; // default 65535 is not enough for long plans
			binding.Security.Mode = SecurityMode.None;
            callback = new MasterServiceCallback();
            client = new MasterServiceClient(callback, binding, new EndpointAddress(uri));
            //client.Endpoint.Behaviors.Add( new ProtoBuf.ServiceModel.ProtoEndpointBehavior() );
			//foreach (var op in client.Endpoint.Contract.Operations)
			//{
   //             DataContractSerializerOperationBehavior dcsBehavior = op.Behaviors.Find<DataContractSerializerOperationBehavior>();
   //             if (dcsBehavior != null)
   //                 op.Behaviors.Remove(dcsBehavior);
   //             op.Behaviors.Add(new ProtoBuf.ServiceModel.ProtoOperationBehavior(op));
			//}
            //Dirigent.Net.Message.RegisterProtobufTypeMaps();

            server = client.ChannelFactory.CreateChannel();

            try
            {
                server.AddClient(name);
            }
            catch
            {
                CloseChannel();
                throw new MasterConnectionTimeoutException(ipaddr, port);
            }

        }

        public void Disconnect()
        {
            if( server == null ) return; // was not connected

            try
            {
                server.RemoveClient( name );
            }
            catch (CommunicationException)
            {
            }

            CloseChannel();
        }

		static List<Message> _emptyMsgList = new List<Message>();
		
		public IEnumerable<Message> ReadMessages()
        {
			
            try
            {
                return server.ClientMessages(name);
            }
            catch (KeyNotFoundException)
            {
                throw new UnknownClientName(name);
            }
			catch( System.ServiceModel.CommunicationException ex)
			{
				// this happens if computer awakes from hibernation
				log.Error(String.Format("Error reading client '{0}' messages from master.", name), ex);
				return _emptyMsgList;
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

        /// <summary>
        /// close the WCF channel
        /// </summary>
        void CloseChannel()
        {
            if (server == null) return;

            var channel = server as ICommunicationObject;
            try
            {
                channel.Close();
            }
            catch (CommunicationException)
            {
                channel.Abort();
            }
            catch (TimeoutException)
            {
                channel.Abort();
            }

            server = null;
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;
            CloseChannel();
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }



}
