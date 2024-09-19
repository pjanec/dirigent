using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using NetCoreServer;
using System.Collections.Concurrent;

namespace Dirigent.Net
{

	/// <summary>
	/// Client session created by the server for communicating with the client.
	/// </summary>
	class MessageSession : NetCoreServer.TcpSession
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public Net.ClientIdent? Ident { get; private set; }

		/// <summary>
		/// Client's name (agent's machineID or non-agent's uuid).
		/// Known as soon as the client sends a ClientIdent message (null till that time)
		/// </summary>
		public string Name => Ident?.Sender ?? string.Empty;


		private MsgPackCodec _msgCodec;
		private Server _server;

		/// <summary>Buffer for async received messages (msgs stays there until passed to server's buffer during <see cref="Poll"/>)</summary>
		private ConcurrentQueue<object> _msgsReceived = new ConcurrentQueue<object>(); 


		public MessageSession( Server server ) : base( server )
		{
			_server = server;
			_msgCodec = new MsgPackCodec();
			_msgCodec.MessageReceived = OnMessageReceived;
		}

		// Async!
		void OnMessageReceived( Net.Message instance )
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


		//public void BroadcastMessage<T>( T msg )
		//{
		//	var ms = new System.IO.MemoryStream();
		//	_msgCodec.Serialize( ms, msg );
		//	_server.Multicast( ms.GetBuffer(), 0, ms.Position );
		//}

		// Async!
		protected override void OnConnected()
		{
			log.Info( $"TCP session with Id {Id} connected!" );

			_server.OnSessionConnected( this );
		}

		// Async!
		protected override void OnDisconnected()
		{
			log.Info( $"TCP session with Id {Id} disconnected!" );
			_server.OnSessionDisconnected( this );
		}

		// Async!
		protected override void OnReceived( byte[] buffer, long offset, long size )
		{
			_msgCodec.ReceivedMessagePart( buffer, offset, size );
		}

		protected override void OnError( SocketError error )
		{
			log.Error( $"TCP session caught an error with code {error}" );
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
		private Queue<MessageSession> _connectingClients = new Queue<MessageSession>();

		/// <summary>Just asynchrously disconnected but not yet removed from synchronous use</summary>
		private Queue<MessageSession> _disconnectingClients = new Queue<MessageSession>(); //

		/// <summary>Already connected clients available for synchronous processing</summary>
		private Dictionary<Guid, MessageSession> _connectedClients = new Dictionary<Guid, MessageSession>();

		/// <summary>Those clients that already connected and already sent ClientInfo</summary>
		private Dictionary<Guid, MessageSession> _identifiedClients = new Dictionary<Guid, MessageSession>();

		/// <summary>coded for outgoing messages</summary>
		private MsgPackCodec _msgCodec;

		/// <summary>Messages received since last <see cref="Tick"/> from all connected client. Filled & read synchronously from <see cref="Tick"/></summary>
		private ConcurrentQueue<Message> _messagesReceived = new();

		public Server( int port )
			: base( IPAddress.Any, port )
		{
			this._port = port;
			_msgCodec = new MsgPackCodec();

			Start();
		}

		protected override void Dispose( bool disposingManagedResources )
		{
			base.Dispose( disposingManagedResources );
		}

		protected override TcpSession CreateSession()
		{
			return new MessageSession( this );
		}

		// async!
		internal void OnSessionConnected( MessageSession session )
		{
			lock( _connectingClients )
				_connectingClients.Enqueue( session );
		}

		// async!
		internal void OnSessionDisconnected( MessageSession session )
		{
			lock( _disconnectingClients )
				_disconnectingClients.Enqueue( session );
		}

		// called synchronously from session's Poll()
		// also called asynchronously from master's Send() if called from a script on master
		public void BufferMessageReceived( Message msg )
		{
			_messagesReceived.Enqueue( msg );
		}

		// called synchronously from session's Poll()
		internal void ClientIdentified( MessageSession session )
		{
			_identifiedClients[session.Id] = session;

			log.Debug( $"Identified: {session.Id} => {session.Name}" );

		}

		/// <summary>
		/// Process incoming events (connection/disconnection/message...)
		/// </summary>
		public void Tick( Action<Message>? act = null )
		{
			// WARNING:
			//	We process buffered connections before disconnections independently on the order they were detected by the NetCoreServer lib.
			//
			//  If a client connects and disconnects in the same tick, we want to process the connection event first.
			//
			//  If a client disconnects and reconnects in a quick succession (few msecs - seen on some weird networks)
			//  the OnConnected callback for the new TCP session can be fired before the OnDisconnected callback for the old one.
			//  This then seems like the client is connected twice for a short period of time.
			//  We need to be robust against this by supporting that situation in our code, for example by sending messages to both TCP sessions
			//  belonging to the same client. We just hope that it will not happen too often and that the messages will not reach the
			//  already disconnecting client twice and if they do, that it will not cause any harm.

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
					_identifiedClients.Remove( s.Id );
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
				if( !msg.IsFrequent )
				{
					log.Debug( $"[master] <= [{msg.Sender}]: {msg}" );
				}

				MessageReceived?.Invoke( msg );
				act?.Invoke( msg );
			}
		}

		/// <summary>
		/// Sends message to all identified clients who are interested
		/// </summary>
		public void SendToAllSubscribed( Net.Message msg, EMsgRecipCateg msgCategoryMask )
		{
			var ms = new System.IO.MemoryStream();
			MsgPackCodec.Serialize( ms, msg );

			if( !msg.IsFrequent )
			{
				log.Debug( $"[master] => [*]: {msg}" );
			}

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
		public void SendToSingle( Net.Message msg, string clientName )
		{
			var sessions = _identifiedClients.Values.Where( x => x.Name == clientName );
			foreach( var session in sessions )
			{
				log.Debug( $"[master] => [{clientName}]: {msg}" );

				var ms = new System.IO.MemoryStream();
				MsgPackCodec.Serialize( ms, msg );
				session.SendAsync( ms.GetBuffer(), 0, ms.Position );
			}
		}

		public Socket? GetClientSocket( string clientName )
		{
			// Note: this returns the first client with the given name
			// In some rare cases (like rapid client disconnection and reconnection) there can be more
			// client TCP sessions with the same name, hopefully all having the same IP address...
			var session = _identifiedClients.Values.FirstOrDefault( x => x.Name == clientName );
			if( session != null )
			{
				return session.Socket;
			}
			return null;
		}

	}


}
