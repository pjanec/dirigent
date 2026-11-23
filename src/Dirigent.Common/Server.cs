using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using System.Net;
using NetCoreServer;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Concurrent;

namespace Dirigent.Net
{

	/// <summary>
	/// Receives messages from any connected clients and puts them all into a single buffer to be periodically polled.
	/// During Poll() the MessageReceived delegate is called for each message from the buffer and the buffer is emptied.
	/// Outgoing multicast messages are sent just to the clients that are subscribed to the message category.
	/// Outgoing multicast messages are not sent only to clients who already identified themselves by sending a ClientIdent message.
	/// </summary>
    public class Server : Disposable
	{
		/// <summary>Called from <see cref="Poll"/> for each received message</summary>
		public Action<Net.Message>? MessageReceived;

		/// <summary>The names of connected and identified clients</summary>
		public IEnumerable<Net.ClientIdent> Clients => from x in _identifiedClients select x.Value.Ident;

		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		private int _port;

		/// <summary>What the master port is: plain TCP, or TLS with the proof of the site secret both ways.</summary>
		public SecuritySettings Security { get; }

		/// <summary>The certificate presented on a secured port, made at start; null when the port is plain.</summary>
		private readonly X509Certificate2? _certificate;

		/// <summary>The hash of that certificate as the clients see it: what the proof of the master is bound to.</summary>
		internal byte[] CertHash { get; } = Array.Empty<byte>();

		/// <summary>The transport: <see cref="PlainListener"/> or <see cref="TlsListener"/>; the sessions it makes do the same work over either.</summary>
		private readonly IListener _listener;

		/// <summary>Whether too many wrong proofs have come from an address.</summary>
		internal ProofFailureLimiter ProofFailures { get; } = new();

		/// <summary>Says once per address and ten minutes that a TLS handshake failed.</summary>
		private readonly AddressLogLimiter _tlsFailureLog = new( Protocol.UnreadableClientLogInterval );

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

		/// <summary>What this server sent and received, by message type.</summary>
		public TrafficStats Traffic { get; }

		/// <param name="traffic">where to count the traffic; the process-wide <see cref="TrafficStats.Default"/> if not given</param>
		/// <param name="security">null or <see cref="SecuritySettings.Off"/>: plain TCP as always. Otherwise TLS, and no client is accepted before it has proved it knows the site secret.</param>
		public Server( int port, TrafficStats? traffic = null, SecuritySettings? security = null )
		{
			Traffic = traffic ?? TrafficStats.Default;
			Security = security ?? SecuritySettings.Off;
			this._port = port;
			_msgCodec = new MsgPackCodec();

			if( Security.Secured )
			{
				_certificate = MasterCertificate.Create();
				CertHash = SiteProof.CertHash( _certificate );
				var context = new SslContext( System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13, _certificate );
				_listener = new TlsListener( this, context, port );
			}
			else
			{
				_listener = new PlainListener( this, port );
			}

			_listener.Start();
		}

		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				_listener.Dispose();
				_certificate?.Dispose();
			}
			base.Dispose( disposing );
		}

		/// <summary>The handshake of a TLS session failed before any message: a plain client, an old one, or one with security off.</summary>
		internal void OnTlsHandshakeFailed( string address )
		{
			if( _tlsFailureLog.ShouldLog( address ) )
				log.Warn( $"{address}: TLS handshake failed - is security on and the same version on that machine?" );
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

		private readonly Dictionary<string, DateTime> _unreadableLogged = new();

		// async!
		// once per address and UnreadableClientLogInterval: a client that retries every second would otherwise fill the log
		internal void OnFirstMessageUnreadable( string address )
		{
			lock( _unreadableLogged )
			{
				var now = DateTime.UtcNow;
				if( _unreadableLogged.TryGetValue( address, out var last ) && now - last < Protocol.UnreadableClientLogInterval ) return;
				_unreadableLogged[address] = now;
			}

			log.Warn( $"a client at {address} sent a message this master cannot read - it probably runs an older Dirigent than {Protocol.ProductVersion}; update it" );
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

			// a connection that has not been proved in time is closed: one that connects and says nothing must not hold a place
			if( Security.Secured )
			{
				var now = DateTime.UtcNow;
				foreach( var session in _connectedClients.Values )
					if( !session.Established && now - session.ConnectedAt > Security.HandshakeTimeout )
						session.TimeOut( Security.HandshakeTimeout );
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
					log.Debug( $"[master] <= [{msg.Sender}]: {Tools.SafeToString( msg )}" );
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
				log.Debug( $"[master] => [*]: {Tools.SafeToString( msg )}" );
			}

			foreach( var s in _identifiedClients.Values )
			{
				if( s.WantsReceiveMessage( msgCategoryMask ) )
				{
					s.SendAsync( ms.GetBuffer(), 0, ms.Position );
					Traffic.Sent( msg.GetType(), ms.Position );
				}
			}
		}

		/// <summary>
		/// Sends a message of the state feed: to the identified clients of the category that want it - agents always do, a GUI
		/// unless it has asked for it not to be sent - except the one named in <paramref name="except"/>, which gets its own.
		/// </summary>
		public void SendToFeed( Net.Message msg, EMsgRecipCateg msgCategoryMask, string? except = null )
		{
			var ms = new System.IO.MemoryStream();
			MsgPackCodec.Serialize( ms, msg );

			if( !msg.IsFrequent )
			{
				log.Debug( $"[master] => [feed]: {Tools.SafeToString( msg )}" );
			}

			foreach( var s in _identifiedClients.Values )
			{
				if( !s.WantsFeed || !s.WantsReceiveMessage( msgCategoryMask ) ) continue;
				if( except is not null && s.Name == except ) continue;
				s.SendAsync( ms.GetBuffer(), 0, ms.Position );
				Traffic.Sent( msg.GetType(), ms.Position );
			}
		}

		/// <summary>
		/// Records whether a client wants the state feed.
		/// </summary>
		/// <returns>true if it did not want it and now does: it has to be sent the full picture</returns>
		public bool SetFeedWanted( string clientName, bool wanted )
		{
			bool turnedOn = false;
			foreach( var s in _identifiedClients.Values )
			{
				if( s.Name != clientName ) continue;
				if( wanted && !s.WantsFeed ) turnedOn = true;
				s.WantsFeed = wanted;
			}
			return turnedOn;
		}

		/// <summary>Whether the named client wants the feed; true for one that is not connected.</summary>
		public bool WantsFeed( string clientName )
			=> _identifiedClients.Values.Where( x => x.Name == clientName ).All( x => x.WantsFeed );

		/// <summary>
		/// Send message to given client only
		/// </summary>
		public void SendToSingle( Net.Message msg, string clientName )
		{
			var sessions = _identifiedClients.Values.Where( x => x.Name == clientName );
			foreach( var session in sessions )
			{
				log.Debug( $"[master] => [{clientName}]: {Tools.SafeToString( msg )}" );

				var ms = new System.IO.MemoryStream();
				MsgPackCodec.Serialize( ms, msg );
				session.SendAsync( ms.GetBuffer(), 0, ms.Position );
				Traffic.Sent( msg.GetType(), ms.Position );
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
