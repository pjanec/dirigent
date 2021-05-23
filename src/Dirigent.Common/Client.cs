using System;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent.Net
{
	public class MasterConnectionTimeoutException : Exception
	{
		public string ip;
		public int port;

		public MasterConnectionTimeoutException( string ip, int port )
			: base( string.Format( "Failed to connect to master {0}:{1}", ip, port ) )
		{
			this.ip = ip;
			this.port = port;
		}
	}

	//[ProtoBuf.ProtoContract]
	//public class ClientIdent
	//{
	//	[ProtoBuf.ProtoMember( 2 )]
	//	public string Name;

	//	[ProtoBuf.ProtoMember( 2 )]
	//	public EMsgRecipCateg SubscribedTo;
	//}

	/// <summary>
	/// Dirigent client endpoint based on protbuf messaging. Connects to master.
	/// Buffers message received from master. Within Poll() method calls MessageReceived delegate for each received message.
	/// Allows sending a message to master.
	/// </summary>
	public class Client	: Disposable
	{
		public Action<Net.Message>? MessageReceived;

		public string MasterIP => _masterIP;
		public int MasterPort => _masterPort;
		public Net.ClientIdent Ident => _ident;

		private Net.ClientIdent _ident;
		private string _masterIP;
		private int _masterPort;
		private ProtoClient _protoClient;
		private List<object> _messagesReceived = new List<object>();

		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		/// <summary>
		/// Creates a dirigent client endpoint based on protbuf messaging.
		/// </summary>
		/// <param name="ident">if Name is empty, will be assigned a GUID matching the one from NetCoreServer's client instance</param>
		/// <param name="autoConn">If autoConn==false, connects once and throws if failed. autoConn==true, try to connect until succeedes; and reconnects if connection is lost; never throws .</param>
		public Client( Net.ClientIdent ident, string masterIP, int masterPort, bool autoConn = false )
		{
			_ident = ident;
			_masterIP = masterIP;
			_masterPort = masterPort;

			Net.Message.RegisterProtobufTypeMaps();


			_protoClient = new ProtoClient( _masterIP, _masterPort, autoConn );

			if( string.IsNullOrEmpty( _ident.Name ) )
			{
				_ident.Name = _protoClient.Id.ToString();
			}

			// as the first thing when connected, tell the master who we are 
			_protoClient.Connected = () =>
			{
				_protoClient.SendMessage( _ident );
			};

			if( autoConn ) // if we want autoconnecting, do not wait for it...
			{

				_protoClient.ConnectAsync();
			}
			else // if we do not want autoconnecting, throw exception on disconnection
			{
				_protoClient.Disconnected = () =>
				{
					throw new MasterConnectionTimeoutException( _masterIP, _masterPort );
				};

			}
		}

		public bool Connect()
		{
			return _protoClient.Connect();
		}

		public void Disconnect()
		{
			_protoClient.Disconnect();
		}

		/// <summary>
		/// Reads all buffered incoming messages and calls MessageRecevied delegate for each
		/// </summary>
		/// <returns></returns>
		public void Tick( Action<Message>? act = null )
		{
			_protoClient.GetMessages( ref _messagesReceived );
			foreach( var m in _messagesReceived )
			{
				var msg = m as Message;
				if( msg != null )
				{
					if( !msg.IsFrequent )
					{
						log.Debug( $"[{_ident.Name}] <= [master]: {msg}" );
					}

					MessageReceived?.Invoke( msg );
					act?.Invoke( msg );
				}
			}
		}

		/// <summary>
		/// Sends message to master
		/// </summary>
		public void Send( Message msg )
		{
			if( IsDisposed ) return;

			msg.Sender = _ident.Sender;
			_protoClient.SendMessage( msg );
		}

		public bool IsConnected => _protoClient.IsConnected;

		protected override void Dispose( bool disposing )
		{
			if( !disposing ) return;
			_protoClient.Dispose();
		}
	}
}
