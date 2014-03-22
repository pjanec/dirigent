using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Common;
using Dirigent.Net;

namespace Dirigent.Agent.Core
{
    /// <summary>
    /// An endpoint in a distributed dirigent architecture. Manages local applications
    /// and shares their status with other Agents. 
    /// </summary>
    public class Agent
    {
        LocalOperations localOps;
        NetworkOperations netOps;
        DirigentControlSwitchableProxy proxy;
        IClient client;
        bool fallbackToLocalOnDisconnection;
        
        public Agent(
            string machineId,
            IClient client,
            bool fallbackToLocalOnDisconnection
        )
        {
            LauncherFactory launcherFactory = new LauncherFactory();
            AppInitializedDetectorFactory appInitializedDetectorFactory = new AppInitializedDetectorFactory();
            this.localOps = new LocalOperations(machineId, launcherFactory, appInitializedDetectorFactory);
            this.netOps = new NetworkOperations( client, localOps );
            this.proxy = new DirigentControlSwitchableProxy(selectProxyImpl(client.IsConnected()));
            this.client = client;
            this.fallbackToLocalOnDisconnection = fallbackToLocalOnDisconnection;
        }

        public void tick()
        {
            double currentTime = (double)DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;

            proxy.SwitchImpl(selectProxyImpl(client.IsConnected()));

            localOps.tick( currentTime );
            netOps.tick( currentTime );

        }

        /// <summary>
        /// Returns a proxy object which uses local operations if not connected to master and 
        /// network operations if connected to master.
        /// </summary>
        /// <returns></returns>
        public IDirigentControl Control
        {
            get
            {
                return proxy;
            }
        }

        private IDirigentControl selectProxyImpl( bool isConnected )
        {
            if (fallbackToLocalOnDisconnection)
            {
                return isConnected ? (IDirigentControl)netOps : (IDirigentControl)localOps;
            }
            else // always use netOps
            {
                return netOps;
            }
        }

        public LocalOperations LocalOps
        {
            get
            {
                return localOps;
            }
        }


    }

}
