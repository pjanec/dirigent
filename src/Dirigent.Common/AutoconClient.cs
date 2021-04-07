using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using Dirigent.Common;

namespace Dirigent.Net
{
    /// <summary>
    /// Client that automatically reconnects if master disappears and reappears;
    /// Tries to connect in its own thread.
    /// If not connected, the calls return immediately (and do nothing)
    /// 
    /// Note: The server get connected/disconnected by a separate thread;
    ///       everything dealing with connection/disconnection needs to be
    ///       locked to avoid race condition.
    /// </summary>
    public class AutoconClient : IClient
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        string name;
        string ipaddr;
        int port;
        bool connected;
		int timeoutMs;

        MasterServiceClient client;
        MasterServiceCallback callback;
        IDirigentMasterContract server;  // server proxy

        // protect access to serverObject which is created in separate thred
        private Object thisLock = new Object();
        
        Thread connectionThread = null;
        bool terminate = false;

        public string Name { get { return name;} }

		public string MasterIP { get { return this.ipaddr; } }
		public int MasterPort { get { return this.port; } }

		public AutoconClient(string name, string ipaddr, int port, int timeoutMs=5000)
        {
            //Test1();

            this.name = name;
            this.ipaddr = ipaddr;
            this.port = port;
            this.connected = false;
			this.timeoutMs = timeoutMs;

            var uri = new Uri( string.Format("net.tcp://{0}:{1}", ipaddr, port) );
            var binding = new NetTcpBinding();
			binding.Name = "MasterConnBinding";
            binding.SendTimeout = new TimeSpan(0,0,0,0,timeoutMs); // shorten the timeout when accessing the service
            //binding.ReceiveTimeout = new TimeSpan(0,0,0,10,0); //  interval of time that a connection can remain inactive, during which no application messages are received, before it is dropped.
            binding.MaxReceivedMessageSize =  Int32.MaxValue; // default 65535 is not enough for long plans
            binding.CloseTimeout = new TimeSpan(0,0,0,0,timeoutMs); // shorten the timeout when closing the channel
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
            Dirigent.Net.Message.RegisterProtobufTypeMaps();

            terminate = false;
            connectionThread = new Thread(connectionThreadLoop);
            connectionThread.Start();

        }


        
        public void Connect()
        {
            lock (thisLock)
            {
                try 
                {
                    server = client.ChannelFactory.CreateChannel();
                    
                    // reduce write timeout
                    IClientChannel channel = server as IClientChannel;
                    channel.OperationTimeout = TimeSpan.FromSeconds((double)timeoutMs/1000);

                    server.AddClient(name);

                    connected = true;
                }
                catch // in case of error just not set connected to true
                {
                    connected = false;
                    CloseChannel();
                }
            }                    
        }

        public void Disconnect()
        {
            InternalDisconnect( true );
        }

        void InternalDisconnect( bool gracefully )
        {
            lock (thisLock)
            {
                if (connected)
                {
                    if (server != null)
                    {
                        if( gracefully )  // try to remove the client from the serbver only if disconenct not called because of communication error
                                          // to avoid another error or timeout
                        {
                            try
                            {
                                server.RemoveClient(name);
                            }
                            catch
                            {
                            }
                        }
                    }
                }

                // release the server object
                CloseChannel();

                connected = false;
            }
        }

        public IEnumerable<Message> ReadMessages()
        {
            if( Monitor.TryEnter(thisLock) ) // do not block if just trying to connect or otherwise busy
            {
                try
                {
                    if (!connected) return new List<Message>();
                    return server.ClientMessages(name);
                }
                catch( System.Exception ex )
                {
                    log.Error( "Comm error", ex );

                    InternalDisconnect( false );
                }
                finally
                {
                    Monitor.Exit(thisLock);
                }
            }
            
            // return empty list if failed
            return new List<Message>();
        }

        public void BroadcastMessage( Message msg )
        {
            if( Monitor.TryEnter( thisLock ) ) // do not block if just trying to connect
            {
                try
                {
                    if (!connected) return;
                    msg.Sender = name;
                    server.BroadcastMessage(msg);
                }
                catch( CommunicationException ex )
                {
                    log.Error( "Comm error", ex );
                    InternalDisconnect( false );
                }
                catch( TimeoutException ex )
                {
                    log.Error( "Time Out", ex );
                    InternalDisconnect( false );
                }
                finally
                {
                    Monitor.Exit( thisLock );
                }
            }
            
        }

        public bool IsConnected()
        {
            //lock (thisLock) // no locking really required; even under race condition when we get the wrong reading ("connected" when it is not really connected) we will safely fail on accessing the connection - it is properly handled everywhere
            {
                return connected;
            }
        }

        void connectionThreadLoop()
        {
            while (!terminate)
            {
                // try to connect
                lock( thisLock )
                {
                    if (!connected)
                    {
                        Connect();
                    }
                }

                // sleep between tries
                if (!terminate) Thread.Sleep(1000);
                if (!terminate) Thread.Sleep(1000);
                if (!terminate) Thread.Sleep(1000);
            }            
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

        public void Dispose()
        {
            terminate = true;
            connectionThread.Join();
            InternalDisconnect( true );
        }



	    //static void Test1()
	    //{
     //       var ser = new System.Runtime.Serialization.DataContractSerializer(typeof(Dirigent.Net.AppsStateMessage));
     //       var payload = new Dictionary<Common.AppIdTuple, Common.AppState>();
     //       payload[ new Common.AppIdTuple("m1.app1")] = new Common.AppState();
     //       var msg = new Dirigent.Net.AppsStateMessage( payload );
     //       var stream = new System.IO.MemoryStream();
     //       ser.WriteObject( stream, msg );
     //       stream.Seek(0, System.IO.SeekOrigin.Begin);
     //       var rawDataStr = new System.IO.StreamReader(stream).ReadToEnd();
	    //}


    }

}
