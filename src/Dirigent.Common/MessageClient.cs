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
	/// <summary>What the client needs of the transport under it - plain TCP or TLS. The NetCoreServer client classes have these members already.</summary>
	interface ITransportClient : IDisposable
	{
		Guid Id { get; }
		bool IsConnected { get; }
		bool Connect();
		bool ConnectAsync();
		bool Disconnect();
		bool DisconnectAsync();
		bool SendAsync( byte[] buffer, long offset, long size );
	}

	/// <summary>
	/// The client's connection to the master: framing, the messages in and out, reconnecting - and on a secured master port the TLS and the
	/// proof of the site secret both ways, before anything else is said.
	/// </summary>
	/// <remarks>
	/// The same over plain TCP and over TLS; <see cref="PlainClientTransport"/> and <see cref="TlsClientTransport"/> are the plumbing.
	/// Over TLS the client accepts whatever certificate the server shows - it is self-signed and different at each start of the master, so there
	/// is nothing to trust in advance - and keeps its hash. The proof the master then sends is bound to the certificate <i>it</i> presents; computed
	/// here over the hash of the one this client was shown, it can only match if they are the same, which a relay in between makes impossible.
	/// </remarks>
	class MessageClient : Disposable
	{
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

		/// <summary>
		/// WARNING: Async!
		/// Called when the master can be talked to: the connection is made, and on a secured port the master has proved itself and been proved to.
		/// </summary>
		public Action? Connected;

		/// <summary>
		/// WARNING: Async!
		/// </summary>
		public Action? Disconnected;

		private MsgPackCodec _msgCodec;
		private List<Net.Message> _msgReceived = new List<Net.Message>();
		private bool _autoRecon = false;
		private bool _isDisposing = false;

		private readonly TrafficStats _traffic;
		private readonly SecuritySettings _security;
		private readonly string _address;
		private readonly int _port;
		private ITransportClient _transport = null!;

		private bool Secured => _security.Secured;

		public Guid Id => _transport.Id;

		/// <summary>The master can be talked to. On a secured port that is only after the proofs.</summary>
		public bool IsConnected => _transport.IsConnected && ( !Secured || _established );

		private volatile bool _established;
		private byte[] _clientNonce = Array.Empty<byte>();
		private volatile byte[]? _certHash;
		private Timer? _handshakeTimer;

		public MessageClient( string address, int port, bool autoRecon, TrafficStats? traffic = null, SecuritySettings? security = null )
		{
			_address = address;
			_port = port;
			_autoRecon = autoRecon;
			_traffic = traffic ?? TrafficStats.Default;
			_security = security ?? SecuritySettings.Off;
			_msgCodec = new MsgPackCodec();
			_msgCodec.Traffic = _traffic;
			_msgCodec.MessageReceived = OnMessageReceived;

			if( Secured )
			{
				// whatever certificate is shown is taken; what it is bound to is checked by the proof
				var context = new SslContext( System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13, new System.Security.Cryptography.X509Certificates.X509Certificate2Collection(),
					( sender, certificate, chain, errors ) =>
					{
						if( certificate is not null ) _certHash = SiteProof.CertHash( certificate );
						return certificate is not null;
					} );
				_transport = new TlsClientTransport( context, address, port, this );
			}
			else
			{
				_transport = new PlainClientTransport( address, port, this );
			}
		}

		protected override void Dispose(bool disposingManagedResources)
		{
			_isDisposing = true;
			_handshakeTimer?.Dispose();
			_transport.Dispose();
			base.Dispose(disposingManagedResources);
		}

		/// <summary>Why the master refused this client, or why this client refuses the master; null while neither. Shapes the reconnect rate.</summary>
		private volatile string? _refusal;

		/// <summary>Said by the master, or found out here: this client is not let in or does not let the master in. Logged, shown to the application, and the next attempt is not soon.</summary>
		void Refuse( string reason )
		{
			_refusal = reason;
			log.Error( reason );
			lock( _msgReceived )
			{
				_msgReceived.Add( new RejectedMessage( reason ) );
			}
		}

		void OnMessageReceived( Net.Message instance )
		{
			if( Secured && !_established )
			{
				OnHandshakeMessage( instance );
				return;
			}

			if( instance is RejectedMessage rej )
			{
				_refusal = rej.Reason;
				log.Error( $"The master refused this client: {rej.Reason}" );
			}
			else
			{
				_refusal = null; // the master talks to us again
			}

			lock( _msgReceived )
			{
				_msgReceived.Add( instance );
			}
		}

		// Async!
		// The master's answer to the Hello, or its refusal.
		void OnHandshakeMessage( Net.Message instance )
		{
			if( instance is RejectedMessage rej )
			{
				_refusal = rej.Reason;
				log.Error( $"The master refused this client: {rej.Reason}" );
				lock( _msgReceived ) _msgReceived.Add( instance );
				return;
			}

			if( instance is not ChallengeMessage challenge )
			{
				log.Warn( $"the master at {_address} sent {instance.GetType().Name} instead of a Challenge; closing" );
				_transport.DisconnectAsync();
				return;
			}

			var certHash = _certHash;
			var expected = certHash is null || challenge.MasterNonce?.Length != SiteProof.NonceLength ? null
				: SiteProof.MasterProof( _security.SiteSecret, _clientNonce, challenge.MasterNonce, certHash );

			if( !SiteProof.Matches( expected, challenge.MasterProof ) )
			{
				Refuse( $"the master at {_address} did not prove the site secret - wrong secret, or someone in between" );
				_transport.DisconnectAsync();
				return;
			}

			SendRaw( new ProofMessage( SiteProof.ClientProof( _security.SiteSecret, _clientNonce, challenge.MasterNonce, certHash! ) ) );

			_handshakeTimer?.Dispose();
			_refusal = null;
			_established = true;
			Connected?.Invoke();
		}

		public void GetMessages( ref List<Net.Message> msgList )
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
			_transport.DisconnectAsync();
			while( _transport.IsConnected )
				Thread.Yield();
		}

		public bool Connect()
		{
			if( !_transport.Connect() ) return false;
			if( !Secured ) return true;

			// the proofs go on on the receiving threads; wait for them
			var until = DateTime.UtcNow + _security.HandshakeTimeout;
			while( !_established && _transport.IsConnected && DateTime.UtcNow < until ) Thread.Sleep( 10 );
			return _established;
		}

		public bool ConnectAsync() => _transport.ConnectAsync();

		public bool Disconnect() => _transport.Disconnect();

		/// <summary>
		/// On a secured port the whole of it - TLS and the proofs - has a time to be done in, from the moment the connection is made; a master that does
		/// not speak TLS (security is off there) never answers.
		/// </summary>
		internal void OnTransportTcpConnected() => ArmHandshakeTimer();

		void ArmHandshakeTimer()
		{
			if( !Secured ) return;
			_handshakeTimer?.Dispose();
			_handshakeTimer = new Timer( _ =>
			{
				if( _established || _isDisposing ) return;
				Refuse( $"the master at {_address} did not finish the handshake within {_security.HandshakeTimeout.TotalSeconds:0.#} s - is security on there too, and the same version?" );
				_transport.DisconnectAsync();
			}, null, _security.HandshakeTimeout, Timeout.InfiniteTimeSpan );
		}

		// ---- transport events (all async!)

		internal void OnTransportReady()
		{
			log.Info( $"{( Secured ? "TLS" : "TCP" )} client connected a new session with Id {Id}" );

			if( !Secured )
			{
				_established = true;
				Connected?.Invoke();
				return;
			}

			// the TLS handshake is done; now the proof of the site secret
			_clientNonce = SiteProof.NewNonce();
			SendRaw( new HelloMessage( _clientNonce ) );
		}

		internal void OnTransportDisconnected( bool tcpConnected, bool tlsDone )
		{
			log.Info( $"{( Secured ? "TLS" : "TCP" )} client disconnected a session with Id {Id}" );

			_handshakeTimer?.Dispose();
			bool wasEstablished = _established;
			_established = false;

			// connected, but not even a TLS handshake: the master is plain, or not ours, or not the same version. (Not connected at all is just a master that is not there yet.)
			if( Secured && tcpConnected && !tlsDone && !_isDisposing && _refusal is null )
				Refuse( $"the TLS handshake with the master at {_address} failed - is security on there too, and the same version?" );

			_msgCodec.Reset(); // reset remnants of data from previous connection

			Disconnected?.Invoke();

			if( _autoRecon && !_isDisposing )
			{
				// Wait for a while... - much longer after a refusal: asking again at the usual rate would only fill the master's log
				var refused = _refusal is not null;
				Thread.Sleep( refused ? (int) Protocol.RefusedRetryInterval.TotalMilliseconds : 1000 );

				// Try to connect again
				if( !_stop && !_isDisposing )
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

		internal void OnTransportReceived( byte[] buffer, long offset, long size )
		{
			_msgCodec.ReceivedMessagePart( buffer, offset, size );
		}

		internal void OnTransportError( SocketError error )
		{
			log.Error( $"{( Secured ? "TLS" : "TCP" )} client caught an error with code {error}" );
		}

		private bool _stop;

		/// <summary>Sends a message. On a secured port nothing but the handshake is sent before the master has been proved.</summary>
		public void SendMessage( Net.Message msg )
		{
			if( Secured && !_established )
			{
				log.Debug( $"not sending {msg.GetType().Name}: the handshake with the master is not done" );
				return;
			}
			SendRaw( msg );
		}

		void SendRaw( Net.Message msg )
		{
			var ms = new System.IO.MemoryStream();
			MsgPackCodec.Serialize( ms, msg );
			_transport.SendAsync( ms.GetBuffer(), 0, ms.Position );
			_traffic.Sent( msg.GetType(), ms.Position );
		}
	}

	sealed class PlainClientTransport : NetCoreServer.TcpClient, ITransportClient
	{
		readonly MessageClient _owner;
		public PlainClientTransport( string address, int port, MessageClient owner ) : base( address, port ) { _owner = owner; }

		protected override void OnConnected() => _owner.OnTransportReady();
		protected override void OnDisconnected() => _owner.OnTransportDisconnected( tcpConnected: true, tlsDone: true );
		protected override void OnReceived( byte[] buffer, long offset, long size ) => _owner.OnTransportReceived( buffer, offset, size );
		protected override void OnError( SocketError error ) => _owner.OnTransportError( error );
	}

	sealed class TlsClientTransport : NetCoreServer.SslClient, ITransportClient
	{
		readonly MessageClient _owner;
		volatile bool _handshaked;
		volatile bool _tcpConnected;

		public TlsClientTransport( SslContext context, string address, int port, MessageClient owner ) : base( context, address, port ) { _owner = owner; }

		protected override void OnConnected() { _tcpConnected = true; _owner.OnTransportTcpConnected(); }
		protected override void OnHandshaked() { _handshaked = true; _owner.OnTransportReady(); }
		protected override void OnDisconnected()
		{
			var tcp = _tcpConnected;
			var done = _handshaked;
			_tcpConnected = false;
			_handshaked = false;
			_owner.OnTransportDisconnected( tcp, done );
		}
		protected override void OnReceived( byte[] buffer, long offset, long size ) => _owner.OnTransportReceived( buffer, offset, size );
		protected override void OnError( SocketError error ) => _owner.OnTransportError( error );
	}
}
