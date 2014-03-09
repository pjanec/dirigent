using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Dirigent.Common;
using Dirigent.Net;

namespace TestClient
{
    class TestClientProgram
    {
        static void Main(string[] args)
        {
            SharedXmlConfigReader cr = new SharedXmlConfigReader();
            var cfg = cr.Load( File.OpenText("../../../../data/SharedConfig.xml") );


            var cl = new Client("cl1", "localhost", 12345);
            cl.Connect();

            LaunchPlan plan1 = new LaunchPlan(
                "plan1", 
                new List<AppDef>()
                {
                    new AppDef() { AppIdTuple = new AppIdTuple("m1", "a"), StartupOrder = -1, Dependencies=new List<string>() {"b"} },
                    new AppDef() { AppIdTuple = new AppIdTuple("m1", "b"), StartupOrder = -1, },
                    new AppDef() { AppIdTuple = new AppIdTuple("m1", "c"), StartupOrder = -1, Dependencies=new List<string>() {"b"} },
                    new AppDef() { AppIdTuple = new AppIdTuple("m1", "d"), StartupOrder = -1, Dependencies=new List<string>() {"a"} },
                }
            );

            cl.BroadcastMessage( new LoadPlanMessage(plan1) );
            
            var messages = cl.ReadMessages();
            foreach( var msg in messages )
            {
                Console.WriteLine("Received: {0}", msg.ToString());
                
                if( msg.GetType() == typeof(LoadPlanMessage) )
                {
                    LoadPlanMessage m = msg as LoadPlanMessage;
                    Console.WriteLine("  LoadPlan '{0}' ({1} applications)", m.plan.Name, m.plan.getAppDefs().Count<AppDef>());
                }
            }
        }
    }
}
