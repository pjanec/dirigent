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
            int masterPort,
            bool isMaster
        )
        {
            client = new Client(machineId, masterIP, masterPort);
            
            // start server before the client starts connecting to it
            //// WARNING: can't have remoting server and client in the same process? (tcp channel already registered)
            //if( isMaster )
            //{
            //    server = new Server(masterPort);
            //}
            // Idea:
            //   - try to connect to a server to chech if it is still running
            //   - if succesfull, reset the server info as if the server was freshly started
            //   - if not succesfull, start a dedicated standalone server process (or execute its assembly in different app domain)

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
