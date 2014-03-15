using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Common;
using Dirigent.Net;

namespace Dirigent.Agent.Core
{
    public class Agent
    {
        LocalOperations localOps;
        NetworkOperations netOps;
        
        Client client;
        Server server;

        public Agent(
            string machineId,
            string masterIP,
            int masterPort
        )
        {
            client = new Client(machineId, masterIP, masterPort);
            
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
