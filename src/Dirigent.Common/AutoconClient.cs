using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using System.ServiceModel;
using System.ServiceModel.Channels;

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
        string name;
        string ipaddr;
        int port;
        bool connected;

        MasterServiceClient client;
        MasterServiceCallback callback;
        IDirigentMasterContract server;  // server proxy

        // protect access to serverObject which is created in separate thred
        private Object thisLock = new Object();
        
        Thread connectionThread = null;
        bool terminate = false;

        public string Name { get { return name;} }

        public AutoconClient(string name, string ipaddr, int port)
        {
            this.name = name;
            this.ipaddr = ipaddr;
            this.port = port;
            this.connected = false;

            var uri = new Uri( string.Format("net.tcp://{0}:{1}", ipaddr, port) );
            var binding = new NetTcpBinding();
            binding.SendTimeout = new TimeSpan(0,0,0,0,500); // shorten the timeout when accessing the service
            //binding.ReceiveTimeout = new TimeSpan(0,0,0,10,0); //  interval of time that a connection can remain inactive, during which no application messages are received, before it is dropped.
            binding.CloseTimeout = new TimeSpan(0,0,0,0,500); // shorten the timeout when closing the channel
            callback = new MasterServiceCallback();
            client = new MasterServiceClient(callback, binding, new EndpointAddress(uri));

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
                    channel.OperationTimeout = TimeSpan.FromSeconds(1);

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
                catch
                {
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
                catch( CommunicationException )
                {
                    InternalDisconnect( false );
                }
                catch( TimeoutException )
                {
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

    }
}
