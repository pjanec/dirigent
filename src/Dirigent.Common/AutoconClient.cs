using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Channels;
using System.Runtime.Remoting.Channels.Tcp;
using System.Threading;

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
        ServerRemoteObject serverObject;
        TcpChannel channel;

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

            channel = new TcpChannel();

            //System.Collections.IDictionary properties = new System.Collections.Hashtable();
            //properties["timeout"] = 500; // PJ: value not taken into account, connection timeout still aroun 1000msec

            //TcpChannel channel = new TcpChannel(
            //                        properties,
            //                        null,
            //                        new BinaryServerFormatterSinkProvider()
            //                        );

            ChannelServices.RegisterChannel(channel, false);

            RemotingConfiguration.RegisterWellKnownClientType(
                typeof(ServerRemoteObject),
                string.Format("tcp://{0}:{1}/Dirigent", ipaddr, port)
            );

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
                    serverObject = ServerRemoteObject.Instance;

                    serverObject.AddClient(name);

                    connected = true;
                }
                catch // in case of error just not set connected to true
                {
                    connected = false;
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
                        if (serverObject != null)
                        {
                            serverObject.RemoveClient(name);
                        }
                    }
                }
                // release the server object
                serverObject = null;
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
                    return serverObject.ClientMessages(name);
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
                    serverObject.BroadcastMessage(name, msg);
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
