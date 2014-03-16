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
        
        public Agent(
            string machineId,
            IClient client
        )
        {
            LauncherFactory launcherFactory = new LauncherFactory();
            AppInitializedDetectorFactory appInitializedDetectorFactory = new AppInitializedDetectorFactory();
            localOps = new LocalOperations( machineId, launcherFactory, appInitializedDetectorFactory );
            netOps = new NetworkOperations( client, localOps );
        }

        public void tick()
        {
            double currentTime = (double)DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;

            localOps.tick( currentTime );
            netOps.tick( currentTime );
        }

        public IDirigentControl getControl()
        {
            return netOps;
        }


    }
}
