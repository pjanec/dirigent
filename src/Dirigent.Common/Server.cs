using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using NetCoreServer;
using Dirigent.Common;


namespace Dirigent.Net
{

    class ProtoSession : NetCoreServer.TcpSession
    {
        private ProtoBufCodec _msgCodec;
        private NetCoreServer.TcpServer _server;

        public ProtoSession( NetCoreServer.TcpServer server ) : base(server)
        {
            _server = server;
            _msgCodec = new ProtoBufCodec( TypeMapRegistry.TypeMap );
            _msgCodec.MessageReceived = OnProtoMessageReceived;
        }

        void OnProtoMessageReceived( uint msgCode, object instance )
        {
            // server just re-broadcasts messages to all clients
            var msg = instance as Message;
            if( msg != null )
            {
				BroadcastMessage( msg );
            }
		}

        //public void SendProtoMsg<T>( T msg )
        //{
        //    var ms = new System.IO.MemoryStream();
        //    _msgCodec.ConstructProtoMessage( ms, msg );
        //    SendAsync( ms.GetBuffer(), 0, ms.Position );
        //}
        
        public void BroadcastMessage<T>( T msg )
        {
            var ms = new System.IO.MemoryStream();
            _msgCodec.ConstructProtoMessage( ms, msg );
            _server.Multicast( ms.GetBuffer(), 0, ms.Position );
        }

        //public void SendText( string text )
        //{
        //    var msg = new Common.Messages.Message1() { someTypeMember = new Common.SomeType() { stringMember = text } };
        //    SendProtoMsg( msg );
        //}


        protected override void OnConnected()
        {
            Console.WriteLine($"TCP session with Id {Id} connected!");

            //// Send invite message
            //SendText("Hello from TCP chat! Please send a message or '!' to disconnect the client!");
            //SendAsync(message);
            
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"TCP session with Id {Id} disconnected!");
        }


        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            _msgCodec.ReceivedMessagePart( buffer, offset, size );
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"TCP session caught an error with code {error}");
        }

    }

    public class Server : NetCoreServer.TcpServer 
    {
        private int port;

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public Server( int port, IDirigentControl localAgent, IEnumerable<ILaunchPlan> planRepo=null, string startupPlanName="" )
           : base( IPAddress.Any, port)
        {
            this.port = port;
            Start();
        }

		protected override void Dispose(bool disposingManagedResources)
		{
			base.Dispose(disposingManagedResources);
		}

        protected override TcpSession CreateSession() { return new ProtoSession(this); }
    }


}
