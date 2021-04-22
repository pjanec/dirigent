using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using NetCoreServer;
using Dirigent.Common;
using System.Collections.Concurrent;

namespace Dirigent.Net
{

	/// <summary>
	/// Client session created by the server for communicating with the client.
	/// </summary>
	class ProtoSession : NetCoreServer.TcpSession
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public Net.ClientIdent? Ident { get; private set; }

		/// <summary>
		/// Client's name (agent's machineID or non-agent's uuid).
		/// Known as soon as the client sends a ClientIdent message (null till that time)
		/// </summary>
		public string Name => Ident?.Sender ?? string.Empty;


		private ProtoBufCodec _msgCodec;
		private Server _server;

		/// <summary>Buffer for async received messages (msgs stays there until passed to server's buffer during <see cref="Poll"/>)</summary>
		private ConcurrentQueue<object> _msgsReceived = new ConcurrentQueue<object>(); 


		public ProtoSession( Server server ) : base( server )
		{
			_server = server;
			_msgCodec = new ProtoBufCodec( TypeMapRegistry.TypeMap );
			_msgCodec.MessageReceived = OnProtoMessageReceived;
		}

		// Async!
		void OnProtoMessageReceived( uint msgCode, object instance )
		{
			_msgsReceived.Enqueue( instance );
		}

		public bool WantsReceiveMessage( EMsgRecipCateg msgCategory )
		{
			return ( msgCategory & Ident?.SubscribedTo ) != 0;
		}

		// puts all messages received sincle last call to server's queue
		public void Poll()
		{
			for( int i=_msgsReceived.Count; i > 0; i-- ) // just those reveived so far, not more
			{
				if( _msgsReceived.TryDequeue( out var m ) )
				{
					var msg = m as Message;
					if( msg != null )
					{
						// process client identification messages
						var ident = msg as Net.ClientIdent;
						if( ident != null )
						{
							SetIdent( ident );
						}

						// put message to server's buffer
						_server.BufferMessageReceived( msg );
					}
				}
			}
		}

		void SetIdent( Net.ClientIdent ident )
		{
			//_outgMsgMask = ident.SubscribedTo;
			//Name = ident.Sender;
			Ident = ident;

			// notify server
			_server.ClientIdentified( this );
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

		// Async!
		protected override void OnConnected()
		{
			Console.WriteLine( $"TCP session with Id {Id} connected!" );

			//// Send invite message
			//SendText("Hello from TCP chat! Please send a message or '!' to disconnect the client!");
			//SendAsync(message);
			_server.OnSessionConnected( this );
		}

		// Async!
		protected override void OnDisconnected()
		{
			Console.WriteLine( $"TCP session with Id {Id} disconnected!" );
			_server.OnSessionDisconnected( this );
		}

		// Async!
		protected override void OnReceived( byte[] buffer, long offset, long size )
		{
			_msgCodec.ReceivedMessagePart( buffer, offset, size );
		}

		protected override void OnError( SocketError error )
		{
			Console.WriteLine( $"TCP session caught an error with code {error}" );
		}

	}

	/// <summary>
	/// Receives messages from any connected clients and puts them all into a single buffer to be periodically polled.
	/// During Poll() the MessageReceived delegate is called for each message from the buffer and the buffer is emptied.
	/// Outgoing multicast messages are sent just to the clients that are subscribed to the message category.
	/// Outgoing multicast messages are not sent only to clients who already identified themselves by sending a ClientIdent message.
	/// </summary>
	public class Server : NetCoreServer.TcpServer
	{
		/// <summary>Called from <see cref="Poll"/> for each received message</summary>
		public Action<Net.Message>? MessageReceived;

		/// <summary>The names of connected and identified clients</summary>
		public IEnumerable<Net.ClientIdent> Clients => from x in _identifiedClients select x.Value.Ident;

		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		private int _port;

		/// <summary>Clients just asynchronously connected but not yet madea vaialable for synchronous use</summary>
		private Queue<ProtoSession> _connectingClients = new Queue<ProtoSession>();

		/// <summary>Just asynchrously disconnected but not yet removed from synchronous use</summary>
		private Queue<ProtoSession> _disconnectingClients = new Queue<ProtoSession>(); //

		/// <summary>Already connected clients available for synchronous processing</summary>
		private Dictionary<Guid, ProtoSession> _connectedClients = new Dictionary<Guid, ProtoSession>();

		/// <summary>Those clients that already connected and already sent ClientInfo</summary>
		private Dictionary<string, ProtoSession> _identifiedClients = new Dictionary<string, ProtoSession>();

		/// <summary>coded for outgoing messages</summary>
		private ProtoBufCodec _msgCodec;

		/// <summary>Messages received since last <see cref="Tick"/> from all connected client. Filled & read synchronously from <see cref="Tick"/></summary>
		private Queue<Message> _messagesReceived = new Queue<Message>();

		public Server( int port )
			: base( IPAddress.Any, port )
		{
			this._port = port;
			_msgCodec = new ProtoBufCodec( TypeMapRegistry.TypeMap );

			Net.Message.RegisterProtobufTypeMaps();

			Start();
		}

		protected override void Dispose( bool disposingManagedResources )
		{
			base.Dispose( disposingManagedResources );
		}

		protected override TcpSession CreateSession()
		{
			return new ProtoSession( this );
		}

		// async!
		internal void OnSessionConnected( ProtoSession session )
		{
			lock( _connectingClients )
				_connectingClients.Enqueue( session );
		}

		// async!
		internal void OnSessionDisconnected( ProtoSession session )
		{
			lock( _disconnectingClients )
				_disconnectingClients.Enqueue( session );
		}

		// called synchronously from session's Poll()
		internal void BufferMessageReceived( Message msg )
		{
			_messagesReceived.Enqueue( msg );
		}

		// called synchronously from session's Poll()
		internal void ClientIdentified( ProtoSession session )
		{
			_identifiedClients[session.Name] = session;

			log.Debug( $"Identified: {session.Id} => {session.Name}" );

		}

		/// <summary>
		/// Process incoming events (connection/disconnection/message...)
		/// </summary>
		public void Tick( Action<Message>? act = null )
		{
			// process clients that has connected since last tick
			lock( _connectingClients )
			{
				while( _connectingClients.Count > 0 )
				{
					var s = _connectingClients.Dequeue();
					_connectedClients[s.Id] = s;
					log.Debug( $"Connected: {s.Id}" );
				}
			}

			// process clients that has disconnected since last tick
			lock( _disconnectingClients )
			{
				while( _disconnectingClients.Count > 0 )
				{
					var s = _disconnectingClients.Dequeue();

					log.Debug( $"Disconnected: {s.Id}" );

					_connectedClients.Remove( s.Id );
					if( !string.IsNullOrEmpty( s.Name ) )
					{
						_identifiedClients.Remove( s.Name );
					}
				}
			}

			// gather messages received from all sessions since last tick and put them to server's buffer
			foreach( var session in _connectedClients.Values )
			{
				session.Poll();
			}

			// invoke MessageReceived delegate for each message received
			while( _messagesReceived.TryDequeue( out var msg ) )
			{
				log.Debug( $"Incoming from {msg.Sender}: {msg}" );
				MessageReceived?.Invoke( msg );
				act?.Invoke( msg );
			}
		}

		/// <summary>
		/// Sends message to all identified clients who are interested
		/// </summary>
		public void SendToAllSubscribed<T>( T msg, EMsgRecipCateg msgCategoryMask )
		{
			var ms = new System.IO.MemoryStream();
			_msgCodec.ConstructProtoMessage( ms, msg );

			log.Debug( $"Multicast: {msg}" );

			foreach( var s in _identifiedClients.Values )
			{
				if( s.WantsReceiveMessage( msgCategoryMask ) )
				{
					s.SendAsync( ms.GetBuffer(), 0, ms.Position );
				}
			}
		}

		/// <summary>
		/// Send message to given client only
		/// </summary>
		public void SendToSingle<T>( T msg, string clientName )
		{
			if( _identifiedClients.TryGetValue( clientName, out var session ) )
			{
				log.Debug( $"Unicast to {clientName}: {msg}" );

				var ms = new System.IO.MemoryStream();
				_msgCodec.ConstructProtoMessage( ms, msg );
				session.SendAsync( ms.GetBuffer(), 0, ms.Position );
			}
		}

	}


}
