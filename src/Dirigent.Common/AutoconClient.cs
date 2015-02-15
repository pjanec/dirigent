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

                    server.AddClient(name);

                    connected = true;
                }
                catch // in case of error just not set connected to true
                {
                    connected = false;
                    server = null;
                }
            }                    
        }

        public void Disconnect()
        {
            try
            {
                if (connected)
                {
                    lock (thisLock)
                    {
                        if (server != null)
                        {
                            server.RemoveClient(name);
                        }
                    }
                }
                // release the server object
                server = null;
            }
            catch
            {
            }

            connected = false;
        }

        public IEnumerable<Message> ReadMessages()
        {
            if (!connected) return new List<Message>();

            try
            {
                lock (thisLock)
                {
                    return server.ClientMessages(name);
                }
            }
            catch
            {
                Disconnect();
            }
            
            // return empty list if failed
            return new List<Message>();
        }

        public void BroadcastMessage( Message msg )
        {
            if (!connected) return;
            
            try
            {
                lock (thisLock)
                {
                    msg.Sender = name;
                    server.BroadcastMessage(msg);
                }
            }
            catch
            {
                Disconnect();
            }
            
        }

        public bool IsConnected()
        {
            return connected;
        }

        void connectionThreadLoop()
        {
            while (!terminate)
            {
                // try to connect
                if (!connected)
                {
                    Connect();
                }

                // sleep between tries
                if (!terminate) Thread.Sleep(1000);
                if (!terminate) Thread.Sleep(1000);
                if (!terminate) Thread.Sleep(1000);
            }            
        }

        public void Dispose()
        {
            terminate = true;
            connectionThread.Join();
            Disconnect();
        }

    }
}
