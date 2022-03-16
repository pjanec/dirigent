using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Net;
using NetCoreServer;
using System.Threading;

namespace Dirigent.Net
{


	class ProtoClient : NetCoreServer.TcpClient
	{
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

		/// <summary>
		/// WARNING: Async!
		/// </summary>
		public Action? Connected;

		/// <summary>
		/// WARNING: Async!
		/// </summary>
		public Action? Disconnected;

		private ProtoBufCodec _msgCodec;
		private List<object> _msgReceived = new List<object>();
		private bool _autoRecon = false;
		private bool _isDisposing = false;

		public ProtoClient( string address, int port, bool autoRecon ) : base( address, port )
		{
			_autoRecon = autoRecon;
			_msgCodec = new ProtoBufCodec( TypeMapRegistry.TypeMap );
			_msgCodec.MessageReceived = OnMessageReceived;
		}

		protected override void Dispose(bool disposingManagedResources)
		{
			_isDisposing = true;
			base.Dispose(disposingManagedResources);
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
			while( IsConnected )
				Thread.Yield();
		}

		protected override void OnConnected()
		{
			log.Info( $"TCP client connected a new session with Id {Id}" );

			Connected?.Invoke();
		}

		protected override void OnDisconnected()
		{
			log.Info( $"TCP client disconnected a session with Id {Id}" );

			Disconnected?.Invoke();

			if( _autoRecon && !_isDisposing )
			{
				// Wait for a while...
				Thread.Sleep( 1000 );

				// Try to connect again
				if( !_stop )
				{
					while(true)	// loop to retry the connect call if exception happens
					{
						try
						{
							ConnectAsync();
							break;
						}
						catch( System.Exception ex )
						{
							log.Error( $"TCP client ConnectAsync exception: {ex}" );
							
							// wait a while before trying again
							Thread.Sleep( 5000 );
						}
					}
				}
			}
		}

		protected override void OnReceived( byte[] buffer, long offset, long size )
		{
			//Console.WriteLine(Encoding.UTF8.GetString(buffer, (int)offset, (int)size));
			_msgCodec.ReceivedMessagePart( buffer, offset, size );
		}

		protected override void OnError( SocketError error )
		{
			log.Error( $"TCP client caught an error with code {error}" );
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
		//    var msg = new Messages.Message1() { someTypeMember = new SomeType() { stringMember = text } };
		//    SendProtoMsg( msg );
		//}
	}

}
