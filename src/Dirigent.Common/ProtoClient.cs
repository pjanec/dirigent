using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using NetCoreServer;
using Dirigent.Common;
using System.Threading;

namespace Dirigent.Net
{


    class ProtoClient : NetCoreServer.TcpClient
    {
        public Action Disconnected;

        private ProtoBufCodec _msgCodec;
        private List<object> _msgReceived = new List<object>();
        private bool _autoRecon = false;

        public ProtoClient(string address, int port, bool autoRecon) : base(address, port)
        {
            _autoRecon = autoRecon;
            _msgCodec = new ProtoBufCodec( TypeMapRegistry.TypeMap );
            _msgCodec.MessageReceived = OnMessageReceived;
        }

        void OnMessageReceived( uint msgCode, object instance )
        {
            lock( _msgReceived )
            {
                _msgReceived.Add( instance );
            }
        }

        public void GetMessages( ref List<object> msgList )
        {
            msgList.Clear();
            lock( _msgReceived )
            {
                msgList.AddRange( _msgReceived );
                _msgReceived.Clear();
            }

        }

        public void DisconnectAndStop()
        {
            _stop = true;
            DisconnectAsync();
            while (IsConnected)
                Thread.Yield();
        }

        protected override void OnConnected()
        {
            Console.WriteLine($"TCP client connected a new session with Id {Id}");
        }

        protected override void OnDisconnected()
        {
            Console.WriteLine($"TCP client disconnected a session with Id {Id}");

            Disconnected?.Invoke();

            if( _autoRecon )
            {
                // Wait for a while...
                Thread.Sleep(1000);

                // Try to connect again
                if (!_stop)
                    ConnectAsync();
            }
        }

        protected override void OnReceived(byte[] buffer, long offset, long size)
        {
            //Console.WriteLine(Encoding.UTF8.GetString(buffer, (int)offset, (int)size));
            _msgCodec.ReceivedMessagePart( buffer, offset, size );
        }

        protected override void OnError(SocketError error)
        {
            Console.WriteLine($"TCP client caught an error with code {error}");
        }

        private bool _stop;

        public void SendMessage<T>( T msg )
        {
            var ms = new System.IO.MemoryStream();
            _msgCodec.ConstructProtoMessage( ms, msg );
            SendAsync( ms.GetBuffer(), 0, ms.Position );
        }
        
        //public void SendText( string text )
        //{
        //    var msg = new Messages.Message1() { someTypeMember = new Common.SomeType() { stringMember = text } };
        //    SendProtoMsg( msg );
        //}
    }

}
