using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Dirigent.Common;

namespace Dirigent.Net
{

    class ClientInfo
    {
        public string Name;
        public List<Message> MsgQueue;
        public long lastActivityTicks;
    }

    /// <summary>
    /// Server object that is remotely callable by clients (through Remoting).
    /// Caches messages for each client separately until the client reads them.
    /// Clients first register themselves by AddClient, then poll their messages via ClientMessages().
    /// Both client or server can broadcast a message.
    /// </summary>
    public class ServerRemoteObject : System.MarshalByRefObject
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private static ServerRemoteObject instance;

        Dictionary<string, ClientInfo> clients = new Dictionary<string, ClientInfo>();


        // cached currennt plan repository
        // loaded from shared config file on server startup
        // set via PlanRepoMessage
        // communicated via message so that clients do not need an extra public intefrace for reading this
        List<ILaunchPlan> PlanRepo;

        // cached current plan;
        // set via LoadPlanMessage
        ILaunchPlan CurrentPlan;

        Timer disconTimer;

        double inactivityTimeout = 5.0;

        public static ServerRemoteObject Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new ServerRemoteObject();
                }
                return instance;
            }
        }
        
        
        private ServerRemoteObject()
        {
            disconTimer = new Timer(DetectDisconnections, null, 0, 1000);
        }

        public void SetInactivityTimeOut( double timeoutSeconds )
        {
            this.inactivityTimeout = timeoutSeconds;
        }


        public override object InitializeLifetimeService()
        {
            return (null);
        }

        /// <summary>
        /// Register a client.
        /// </summary>
        /// <param name="name"></param>
        public void AddClient(string name) 
        {
            if (name != null) 
            {
                log.Info(string.Format("Adding client '{0}'", name));
                lock (clients)
                {
                    var ci = new ClientInfo();
                    ci.Name = name;
                    ci.MsgQueue = new List<Message>();
                    ci.lastActivityTicks = DateTime.UtcNow.Ticks;

                    clients[name] = ci;

                    // inform new clients about current shared state
                    ci.MsgQueue.Add( new PlanRepoMessage(PlanRepo));
                    ci.MsgQueue.Add(new CurrentPlanMessage(CurrentPlan));

                }
            }
        }

        public void RemoveClient(String name) 
        {
            log.Info(string.Format("Removing client '{0}'", name));
            lock (clients) 
            {
                clients.Remove(name);
            }
        }

        /// <summary>
        /// returns true if the message was fully handled and shall not be further processed
        /// </summary>
        /// <param name="clientName"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        private bool HandleMessage(string clientName, Message msg)
        {
            Type t = msg.GetType();

            if (t == typeof(LoadPlanMessage))
            {
                var m = msg as LoadPlanMessage;
                lock (clients)
                {
                    CurrentPlan = m.plan;
                }
            }
            else
            if (t == typeof(CurrentPlanMessage))
            {
                var m = msg as CurrentPlanMessage;
                lock (clients)
                {
                    CurrentPlan = m.plan;
                }
            }
            else
            if (t == typeof(PlanRepoMessage))
            {
                var m = msg as PlanRepoMessage;
                lock (clients)
                {
                    PlanRepo = new List<ILaunchPlan>(m.repo);
                }
            }


            return false;
        }

        public void BroadcastMessage( string clientName, Message msg )
        {

            if (HandleMessage(clientName, msg))
            {
                log.Debug(string.Format("Message handled: {0}", msg.ToString()));
                return;
            }

            log.Debug(string.Format("Broadcasting message: {0}", msg.ToString()));

            // put to message queue for each client, including the sender (agents rely on that!)
            lock( clients )
            {
                foreach( var ci in clients.Values )
                {
                    ci.MsgQueue.Add( msg );
                }
            }
        }
        
        public List<string> Clients() 
        {
            return new List<string>( clients.Keys );
        }

        public IEnumerable<Message> ClientMessages( string clientName ) 
        {
            var ci = clients[clientName];
            var msgList = new List<Message>( ci.MsgQueue );
            
            // messages saved, clear the list of waiting messages
            ci.MsgQueue.Clear();

            // refresh inactivity timer
            ci.lastActivityTicks = DateTime.UtcNow.Ticks;

            return msgList;
        }

        /// <summary>
        /// Clears all client information.
        /// </summary>
        public void Reset()
        {
            clients.Clear();
        }

        private void DetectDisconnections( object state )
        {
            long currTicks = DateTime.UtcNow.Ticks;

            lock (clients)
            {
                var toRemove = new List<string>();

                foreach( var client in clients.Values )
                {
                    var ts = new TimeSpan(DateTime.UtcNow.Ticks - client.lastActivityTicks);
                    var inactivityPeriod = ts.TotalSeconds;

                    if (inactivityPeriod >= inactivityTimeout)
                    {
                        toRemove.Add(client.Name);
                    }

                }

                foreach( var name in toRemove )
                {
                    log.Info(string.Format("Timing out client '{0}'", name));
                    clients.Remove(name);
                }
            }

        }

    }



 }
