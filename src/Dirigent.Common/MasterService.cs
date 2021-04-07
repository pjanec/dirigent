// WCF implementation of the Dirigent Master service

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Dispatcher;
using System.ServiceModel.Description;

using Dirigent.Common;

namespace Dirigent.Net
{
    class ClientInfo
    {
        public string Name;
        public List<Message> MsgQueue;
        public long lastActivityTicks;
    }

    [ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Reentrant, InstanceContextMode=InstanceContextMode.Single)]
    public class MasterService : IDirigentMasterContract
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        Dictionary<string, ClientInfo> clients = new Dictionary<string, ClientInfo>();

        // cached currennt plan repository
        // loaded from shared config file on server startup
        // set via PlanRepoMessage
        // communicated via message so that clients do not need an extra public intefrace for reading this
        List<ILaunchPlan> PlanRepo;

        //// cached current plan;
        //// set via SelectPlanMessage
        string CurrentPlanName;

        Timer disconTimer;

        double inactivityTimeout = 120.0;

		IDirigentControl localAgent;

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
					if (!string.IsNullOrEmpty(CurrentPlanName))
					{
						ci.MsgQueue.Add(new CurrentPlanMessage(CurrentPlanName));
					}

					// send state of all plans as gathered by the local agent
					{
						var d = new Dictionary<string, PlanState>();
						foreach (var p in PlanRepo)
						{
							d[p.Name] = localAgent.GetPlanState(p.Name);// invoke callbeck to get current plan
						}

						ci.MsgQueue.Add(new PlansStateMessage(d));
					}
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

		public MasterService(IDirigentControl localAgent)
        {
			this.localAgent = localAgent;
			disconTimer = new Timer(DetectDisconnections, null, 0, 1000);
        }

        /// <summary>
        /// returns true if the message was fully handled and shall not be further processed
        /// </summary>
        /// <param name="clientName"></param>
        /// <param name="msg"></param>
        /// <returns></returns>
        private bool HandleMessage(Message msg)
        {
            Type t = msg.GetType();

			//if (t == typeof(SelectPlanMessage))
			//{
			//    var m = msg as SelectPlanMessage;
			//    lock (clients)
			//    {
			//        CurrentPlan = m.plan;
			//    }
			//}
			//else
			if (t == typeof(CurrentPlanMessage))
			{
				var m = msg as CurrentPlanMessage;
				lock (clients)
				{
					CurrentPlanName = m.planName;
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
			else
			if (t == typeof(SetAppEnabledMessage))
			{
				// Keep our local copy of the plan repo (the app Enabled checkbox flag) up-to-date with the clients
				// This is useful as when we are adding a new client, we are sending him our local copy of the plan repo 
				// This way the new client gen updated version having correct "enabled" checkboxes
				var m = msg as SetAppEnabledMessage;
				lock (clients)
				{
					// find the plan
					var plan = PlanRepo.Find(x => x.Name.Equals(m.planName, StringComparison.OrdinalIgnoreCase));
					if (plan != null)
					{
						// find the appdef within the plan
						var appDef = plan.getAppDefs().ToList().Find(x => x.AppIdTuple == m.appIdTuple);
						if (appDef != null)
						{
							// change the enabled flag
							appDef.Disabled = !m.enabled;
						}
					}
				}
			}


			return false;
        }

        public void BroadcastMessage( Message msg )
        {

            if (HandleMessage(msg))
            {
                log.Debug(string.Format("Message handled: {0}", msg.ToString()));
                return;
            }

			if( msg.GetType() != typeof( AppsStateMessage ) )
			{
				log.Debug( string.Format( "Broadcasting message: {0}", msg.ToString() ) );
			}

			// check for timed-out clients that still communicate
            bool senderUnknown;
			lock( clients )
			{
				senderUnknown =
					msg.Sender!=Server.MasterSenderName // ignore messages from master
					&& !clients.ContainsKey(msg.Sender);
			}
			if( senderUnknown )
			{
				log.Debug(string.Format( "Client {0} not known (timed out?) but communicates - will be re-added.", msg.Sender));
				AddClient( msg.Sender );
			}

            // put to message queue for each client, including the sender (agents rely on that!)
            lock( clients )
            {
			
				foreach( var ci in clients.Values )
                {
                    //Console.WriteLine("...Adding msg to queue of client: {0}", ci.Name);
					ci.MsgQueue.Add( msg );
                }
            }
        }

        public void BroadcastMessage(string sender, Message msg)
        {
            msg.Sender = sender;
            BroadcastMessage(msg);
        }

        public List<string> Clients() 
        {
            return new List<string>( clients.Keys );
        }

        public IEnumerable<Message> ClientMessages( string clientName ) 
        {
			if (!clients.ContainsKey(clientName))
				return new List<Message>();

			var ci = clients[clientName];
            var msgList = new List<Message>( ci.MsgQueue );
            
            // messages saved, clear the list of waiting messages
            ci.MsgQueue.Clear();

            // refresh inactivity timer
            ci.lastActivityTicks = DateTime.UtcNow.Ticks;

            return msgList;
        }

        ///// <summary>
        ///// Clears all client information.
        ///// </summary>
        //public void Reset()
        //{
        //    clients.Clear();
        //}

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
                    log.Info(string.Format("Timing out client '{0}' after {1} seconds of inactivity.", name, inactivityTimeout));
                    clients.Remove(name);
                }
            }

        }

    }
}
