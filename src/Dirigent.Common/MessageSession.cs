using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using NetCoreServer;

namespace Dirigent.Net
{
	/// <summary>
	/// What a session needs of the transport under it - plain TCP or TLS. The two NetCoreServer session classes have these members already.
	/// </summary>
	interface ITransportSession
	{
		Guid Id { get; }
		Socket Socket { get; }
		bool SendAsync( byte[] buffer, long offset, long size );
		long Send( byte[] buffer, long offset, long size );
		bool Disconnect();
	}

	/// <summary>The server's listening socket, plain or TLS.</summary>
	interface IListener : IDisposable
	{
		bool Start();
	}

	/// <summary>
	/// One client connected to the master: its messages in, the handshake that lets it in, who it is once it has said so.
	/// </summary>
	/// <remarks>
	/// The same for plain TCP and for TLS: this is the part that does the work, and the two transport classes below are no more than the
	/// plumbing that gives it bytes and a way to send them. On a secured port the session accepts nothing but Hello and then Proof until the
	/// client has shown it knows the site secret; only after that does a ClientIdent mean anything.
	/// </remarks>
	class MessageSession
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public Net.ClientIdent? Ident { get; private set; }

		/// <summary>
		/// Client's name (agent's machineID or non-agent's uuid).
		/// Known as soon as the client sends a ClientIdent message (null till that time)
		/// </summary>
		public string Name => Ident?.Sender ?? string.Empty;

		public Guid Id => _transport.Id;
		public Socket Socket => _transport.Socket;

		private MsgPackCodec _msgCodec;
		private Server _server;
		private readonly ITransportSession _transport;
		private readonly bool _secured;

		/// <summary>Buffer for async received messages (msgs stays there until passed to server's buffer during <see cref="Poll"/>)</summary>
		private ConcurrentQueue<object> _msgsReceived = new ConcurrentQueue<object>();

		enum EHandshake
		{
			/// <summary>TLS is not done yet.</summary>
			Connecting,
			AwaitHello,
			AwaitProof,
			Established,
		}

		private volatile EHandshake _handshake;

		/// <summary>When the connection was made, to close it if it is not proved in time.</summary>
		public DateTime ConnectedAt { get; } = DateTime.UtcNow;

		/// <summary>The client may be heard: on a plain port from the start, on a secured one once it has proved the site secret.</summary>
		public bool Established => _handshake == EHandshake.Established;

		private byte[] _clientNonce = Array.Empty<byte>();
		private byte[] _masterNonce = Array.Empty<byte>();

		/// <summary>Whether the TLS handshake completed; a session that is closed before has failed it, and that is what is said.</summary>
		private volatile bool _tlsDone;

		/// <summary>This session was closed for a reason that has been logged; nothing more is said about it.</summary>
		private volatile bool _closeLogged;

		private string _address = "?";

		public MessageSession( Server server, ITransportSession transport, bool secured )
		{
			_server = server;
			_transport = transport;
			_secured = secured;
			_handshake = secured ? EHandshake.Connecting : EHandshake.Established;

			_msgCodec = new MsgPackCodec();
			_msgCodec.Traffic = server.Traffic;
			_msgCodec.MessageReceived = OnMessageReceived;
			_msgCodec.DecodeFailed = OnDecodeFailed;
		}

		/// <summary>Whether any message of this session has been decoded yet.</summary>
		private volatile bool _decodedAny;

		/// <summary>The master has refused this client; whatever else it sends is dropped.</summary>
		private bool _refused;

		private bool _unreadableHandled;

		/// <summary>The address the client connected from, for the log.</summary>
		/// <remarks>
		/// Looked up when first needed if the connect callback has not run yet: a fast client's first bytes can be received and fail to
		/// decode before it does, and "?" then made the once-per-address log line come out twice for the same address.
		/// </remarks>
		public string RemoteAddress
		{
			get
			{
				if( _address == "?" ) RememberAddress();
				return _address;
			}
		}

		void RememberAddress()
		{
			try { _address = ( _transport.Socket.RemoteEndPoint as IPEndPoint )?.Address.ToString() ?? "?"; }
			catch { /* a socket that is already gone: the address stays what it was */ }
		}

		public bool SendAsync( byte[] buffer, long offset, long size ) => _transport.SendAsync( buffer, offset, size );

		public bool Disconnect() => _transport.Disconnect();

		// ---- transport events (all async!)

		/// <summary>The connection is made. For TLS the handshake is still to come.</summary>
		public void OnTransportConnected()
		{
			RememberAddress();
			// on a secured port nothing of TLS has happened yet at this point - say so, rather than "TLS session connected" before a handshake that may fail
			log.Info( _secured
				? $"Connection {Id} accepted from {RemoteAddress}; TLS handshake and site-secret proof follow"
				: $"TCP session with Id {Id} connected from {RemoteAddress}!" );

			_server.OnSessionConnected( this );
		}

		/// <summary>
		/// A TLS session is about to start its handshake. An address that has just failed too many proofs is not talked to at all, TLS included:
		/// closed here, before the handshake costs anything. (Not in <see cref="OnTransportConnected"/>: the stream is not there yet to close.)
		/// </summary>
		public void OnTransportHandshaking()
		{
			if( _server.ProofFailures.IsBlocked( RemoteAddress ) )
			{
				_closeLogged = true;
				Disconnect();
			}
		}

		/// <summary>The bytes can flow: plain TCP once connected, TLS once the handshake is done.</summary>
		public void OnTransportReady()
		{
			_tlsDone = true;
			if( _secured && _handshake == EHandshake.Connecting ) _handshake = EHandshake.AwaitHello;
		}

		public void OnTransportDisconnected()
		{
			log.Info( $"{( _secured ? "TLS" : "TCP" )} session with Id {Id} disconnected!" );

			// an honest client whose secret is not ours checks the master's proof first, finds it wrong and hangs up before it has sent one of its own: for the master that is a failed proof too
			if( _secured && _handshake == EHandshake.AwaitProof && !_closeLogged )
				FailedProof();

			// a secured session that never got through the TLS handshake: not one of ours (a plain client, an old one, one with security off)
			if( _secured && !_tlsDone && !_closeLogged )
				_server.OnTlsHandshakeFailed( RemoteAddress );

			_server.OnSessionDisconnected( this );
		}

		public void OnTransportReceived( byte[] buffer, long offset, long size )
		{
			_msgCodec.ReceivedMessagePart( buffer, offset, size );
		}

		public void OnTransportError( SocketError error )
		{
			log.Error( $"{( _secured ? "TLS" : "TCP" )} session caught an error with code {error}" );
		}

		// Async!
		void OnMessageReceived( Net.Message instance )
		{
			_decodedAny = true;

			// A message on a secured session can only have been decrypted after the TLS handshake - but the callback saying so may
			// not have run yet (the first bytes can be received first). Take the handshake as done rather than closing a good client.
			if( _secured && _handshake == EHandshake.Connecting ) OnTransportReady();

			switch( _handshake )
			{
				case EHandshake.Established:
					if( instance is HelloMessage or ChallengeMessage or ProofMessage )
					{
						// not a thing to say once in: on a plain port a client that speaks TLS has no business here, on a secured one it is a client gone mad
						Close( $"{RemoteAddress}: unexpected {instance.GetType().Name}; closing" );
						return;
					}
					_msgsReceived.Enqueue( instance );
					break;

				case EHandshake.AwaitHello:
					if( instance is HelloMessage hello ) OnHello( hello );
					else Close( $"{RemoteAddress}: {instance.GetType().Name} before the Hello of the handshake; closing" );
					break;

				case EHandshake.AwaitProof:
					if( instance is ProofMessage proof ) OnProof( proof );
					else Close( $"{RemoteAddress}: {instance.GetType().Name} before the Proof of the handshake; closing" );
					break;

				default:
					Close( $"{RemoteAddress}: a message before the TLS handshake was done; closing" );
					break;
			}
		}

		void Close( string why )
		{
			_closeLogged = true;
			log.Warn( why );
			Disconnect();
		}

		void OnHello( HelloMessage hello )
		{
			if( hello.ProtocolVersion != Protocol.Version )
			{
				var reason = $"a client at {RemoteAddress} speaks protocol {hello.ProtocolVersion}, this master {Protocol.Version} - update it to {Protocol.ProductVersion}";
				_closeLogged = true;
				log.Warn( reason );
				Refuse( reason );
				return;
			}

			if( hello.ClientNonce is null || hello.ClientNonce.Length != SiteProof.NonceLength )
			{
				Close( $"{RemoteAddress}: a Hello without a proper nonce; closing" );
				return;
			}

			_clientNonce = hello.ClientNonce;
			_masterNonce = SiteProof.NewNonce();
			var proof = SiteProof.MasterProof( _server.Security.SiteSecret, _clientNonce, _masterNonce, _server.CertHash );

			_handshake = EHandshake.AwaitProof;
			SendMessage( new ChallengeMessage( _masterNonce, proof ) );
		}

		void OnProof( ProofMessage proof )
		{
			var expected = SiteProof.ClientProof( _server.Security.SiteSecret, _clientNonce, _masterNonce, _server.CertHash );
			if( !SiteProof.Matches( expected, proof.ClientProof ) )
			{
				FailedProof();
				Disconnect();
				return;
			}

			_handshake = EHandshake.Established;
		}

		/// <summary>A wrong proof, or none: the client has seen the master's proof and walked away from it. Said, and counted against its address.</summary>
		void FailedProof()
		{
			_closeLogged = true;
			log.Warn( $"{RemoteAddress}: wrong site secret (or not the same Dirigent)" );
			if( _server.ProofFailures.Record( RemoteAddress, out var blockedNow ) && blockedNow )
				log.Warn( $"{RemoteAddress}: more than {ProofFailureLimiter.MaxFailures} failed proofs within {ProofFailureLimiter.Window.TotalSeconds:0} s - connections from it are closed at once for {ProofFailureLimiter.BlockFor.TotalSeconds:0} s" );
		}

		/// <summary>The handshake took too long: the client is closed out.</summary>
		public void TimeOut( TimeSpan limit )
		{
			Close( $"{RemoteAddress}: the handshake was not completed within {limit.TotalSeconds:0.#} s; closing" );
		}

		void SendMessage( Net.Message msg )
		{
			var ms = new System.IO.MemoryStream();
			MsgPackCodec.Serialize<Net.Message>( ms, msg );
			_transport.SendAsync( ms.GetBuffer(), 0, ms.Position );
			_server.Traffic.Sent( msg.GetType(), ms.Position );
		}

		// Async!
		// A client of an older Dirigent speaks an encoding this master cannot decode, and says so by its very first message
		// being unreadable. Say that once, rather than an error per message, and hang up.
		void OnDecodeFailed( Exception error )
		{
			if( _decodedAny )
			{
				log.Error( $"Deserialize<{typeof( Net.Message ).FullName}> failed: {error.Message} {error.InnerException}" );
				return;
			}

			if( _unreadableHandled ) return;
			_unreadableHandled = true;
			_closeLogged = true;

			_server.OnFirstMessageUnreadable( RemoteAddress );
			Disconnect();
		}

		/// <summary>Tells the client why it is not wanted and hangs up.</summary>
		void Refuse( string reason )
		{
			_refused = true;
			try
			{
				var ms = new System.IO.MemoryStream();
				MsgPackCodec.Serialize<Net.Message>( ms, new RejectedMessage( reason ) );
				_transport.Send( ms.GetBuffer(), 0, ms.Position ); // synchronous, so that it is out before the connection closes
				_server.Traffic.Sent( typeof( RejectedMessage ), ms.Position );
			}
			catch( Exception ex )
			{
				log.Warn( $"Could not tell {RemoteAddress} that it is refused: {ex.Message}" );
			}
			Disconnect();
		}

		/// <summary>
		/// Whether the client wants the state feed (see <see cref="FeedRequestMessage"/>). A client that has not said wants it.
		/// </summary>
		public volatile bool WantsFeed = true;

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
					if( _refused ) continue;

					var msg = m as Message;
					if( msg != null )
					{
						// process client identification messages
						var ident = msg as Net.ClientIdent;
						if( ident != null )
						{
							if( ident.ProtocolVersion != Protocol.Version )
							{
								var reason = $"{ident.Sender} at {RemoteAddress} speaks protocol {ident.ProtocolVersion}, this master {Protocol.Version} - update it to {Protocol.ProductVersion}";
								log.Warn( reason );
								Refuse( reason );
								continue;
							}
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
			Ident = ident;

			// notify server
			_server.ClientIdentified( this );
		}
	}

	// ---- the plain TCP transport

	sealed class PlainTransportSession : NetCoreServer.TcpSession, ITransportSession
	{
		readonly MessageSession _core;

		public PlainTransportSession( TcpServer listener, Server owner ) : base( listener )
		{
			_core = new MessageSession( owner, this, secured: false );
		}

		protected override void OnConnected() { _core.OnTransportConnected(); _core.OnTransportReady(); }
		protected override void OnDisconnected() => _core.OnTransportDisconnected();
		protected override void OnReceived( byte[] buffer, long offset, long size ) => _core.OnTransportReceived( buffer, offset, size );
		protected override void OnError( SocketError error ) => _core.OnTransportError( error );
	}

	sealed class PlainListener : NetCoreServer.TcpServer, IListener
	{
		readonly Server _owner;
		public PlainListener( Server owner, int port ) : base( IPAddress.Any, port ) { _owner = owner; }
		protected override TcpSession CreateSession() => new PlainTransportSession( this, _owner );
	}

	// ---- the TLS transport

	sealed class TlsTransportSession : NetCoreServer.SslSession, ITransportSession
	{
		readonly MessageSession _core;

		public TlsTransportSession( SslServer listener, Server owner ) : base( listener )
		{
			_core = new MessageSession( owner, this, secured: true );
		}

		protected override void OnConnected() => _core.OnTransportConnected();
		protected override void OnHandshaking() => _core.OnTransportHandshaking();
		protected override void OnHandshaked() => _core.OnTransportReady();
		protected override void OnDisconnected() => _core.OnTransportDisconnected();
		protected override void OnReceived( byte[] buffer, long offset, long size ) => _core.OnTransportReceived( buffer, offset, size );
		protected override void OnError( SocketError error ) => _core.OnTransportError( error );
	}

	sealed class TlsListener : NetCoreServer.SslServer, IListener
	{
		readonly Server _owner;
		public TlsListener( Server owner, SslContext context, int port ) : base( context, IPAddress.Any, port ) { _owner = owner; }
		protected override SslSession CreateSession() => new TlsTransportSession( this, _owner );
	}

	// ---- limits

	/// <summary>
	/// Counts the wrong proofs from each address: more than five within a minute and the address is shut out for a minute. The site secret
	/// must not be guessable at the speed of a network.
	/// </summary>
	public sealed class ProofFailureLimiter
	{
		public const int MaxFailures = 5;
		public static readonly TimeSpan Window = TimeSpan.FromSeconds( 60 );
		public static readonly TimeSpan BlockFor = TimeSpan.FromSeconds( 60 );

		readonly Func<DateTime> _now;
		readonly Dictionary<string, Queue<DateTime>> _failures = new();
		readonly Dictionary<string, DateTime> _blockedUntil = new();

		public ProofFailureLimiter( Func<DateTime>? now = null ) { _now = now ?? ( () => DateTime.UtcNow ); }

		/// <summary>Notes a failure.</summary>
		/// <param name="justBlocked">true for the failure that made the address blocked</param>
		/// <returns>whether the address is blocked now</returns>
		public bool Record( string address, out bool justBlocked )
		{
			lock( _failures )
			{
				var now = _now();
				justBlocked = false;

				if( !_failures.TryGetValue( address, out var q ) ) _failures[address] = q = new Queue<DateTime>();
				q.Enqueue( now );
				while( q.Count > 0 && now - q.Peek() > Window ) q.Dequeue();

				if( q.Count > MaxFailures )
				{
					justBlocked = !( _blockedUntil.TryGetValue( address, out var until ) && until > now );
					_blockedUntil[address] = now + BlockFor;
					q.Clear();
					return true;
				}
				return _blockedUntil.TryGetValue( address, out var u ) && u > now;
			}
		}

		public bool IsBlocked( string address )
		{
			lock( _failures )
				return _blockedUntil.TryGetValue( address, out var until ) && until > _now();
		}
	}

	/// <summary>Says yes to the first and then to every <c>interval</c> after it, for each address: a line in a log that must not be repeated for a client that tries again every few seconds.</summary>
	public sealed class AddressLogLimiter
	{
		readonly TimeSpan _interval;
		readonly Func<DateTime> _now;
		readonly Dictionary<string, DateTime> _last = new();

		public AddressLogLimiter( TimeSpan interval, Func<DateTime>? now = null ) { _interval = interval; _now = now ?? ( () => DateTime.UtcNow ); }

		public bool ShouldLog( string address )
		{
			lock( _last )
			{
				var now = _now();
				if( _last.TryGetValue( address, out var last ) && now - last < _interval ) return false;
				_last[address] = now;
				return true;
			}
		}
	}
}
