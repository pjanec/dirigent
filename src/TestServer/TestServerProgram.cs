using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Net;

namespace TestServer
{
    class TestServerProgram
    {
        static void Main(string[] args)
        {
            var s = new Server(12345);
            // server works through its ServerRemoteObject
            Console.WriteLine("Press a key to exit the server.");
            Console.ReadLine();
        }
    }
}
