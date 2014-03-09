using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dirigent.Net
{

    class ClientInfo
    {
        public string Name;
        public List<Message> MsgQueue;
    }

    /// <summary>
    /// Server object that is remotely callable by clients (through Remoting).
    /// Caches messages for each client separately until the client reads them.
    /// Clients first register themselves by AddClient, then poll their messages via ClientMessages().
    /// Both client or server can broadcast a message.
    /// </summary>
    public class ServerRemoteObject : System.MarshalByRefObject
    { 
        Dictionary<string, ClientInfo> clients = new Dictionary<string, ClientInfo>();

        /// <summary>
        /// Register a client.
        /// </summary>
        /// <param name="name"></param>
        public void AddClient(string name) 
        {
            if (name != null) 
            {
                lock (clients)
                {
                    var ci = new ClientInfo();
                    ci.Name = name;
                    ci.MsgQueue = new List<Message>();

                    clients[name] = ci;
                }
            }
        }

        public void RemoveClient(String name) 
        {
            lock (clients) 
            {
                clients.Remove(name);
            }
        }
        
        public void BroadcastMessage( string clientName, Message msg )
        {
            Console.WriteLine("Broadcasting message: {0}", msg.ToString());
            // put to message queue for each client
            lock( clients )
            {
                foreach( var ci in clients.Values )
                {
                    //if( ci.Name != clientName ) // do not send back to sender
                    {
                        ci.MsgQueue.Add( msg );
                    }
                }
            }
        }
        
        public List<string> Clients() 
        {
            return new List<string>( clients.Keys );
        }

        public IEnumerable<Message> ClientMessages( string clientName ) 
        {
            var msgList = new List<Message>( clients[clientName].MsgQueue );
            
            // messages saved, clear the list of waiting messages
            clients[clientName].MsgQueue.Clear();

            return msgList;
        }

        /// <summary>
        /// Clears all client information.
        /// </summary>
        public void Reset()
        {
            clients.Clear();
        }

    }



 }
