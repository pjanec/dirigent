using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent
{
	public class Service
    {
        public string Name;
        
        /// <summary>
        /// IP address to be ised used to connect to the service;
        /// Localhost 127.0.0.1 if the computers is behind a gateway;
        /// Otherwise the IP of the remote computer
        /// </summary>
        public string GetIP(bool remote) => remote ? FwdIP :LocalIP;

        /// <summary>
        /// Port to be used to connect to the service;
        /// Local forwarded port if the computers is behind a gateway used with local IP 127.0.0.1:PORT;
        /// otherwise the usual port for that service what port number we will use when connecting via 
        /// </summary>
        public int GetPort(bool remote) => remote ? FwdPort :LocalPort;

        //public string UserName;

        //public string Password;

        public string LocalIP; // non-forwarded
        public int LocalPort; // non-forwarded

        public string FwdIP;
        public int FwdPort;
        

        public Service(
            string name,
            string localIP,
            int localPort,
            string remoteIP,
            int remotePort )
        {
			Name = name;

			LocalIP = localIP;
            LocalPort = localPort;

            FwdIP = remoteIP;
            FwdPort = remotePort;
        }




    }
}
