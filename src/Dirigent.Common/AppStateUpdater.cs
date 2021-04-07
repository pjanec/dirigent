
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Dirigent.Common;
using System.IO;

namespace Dirigent.Net
{

    // Maintains UDP multicast socket on same port number as the master TCP port
    // Asynchronously receives app state packets from other agents and store to locaked queue.
    // The receive queue is polled each tick and app states are updated.
    // Each tick serializes all local app state to a blob and sends via UDP.
    public class AppStateUpdater
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    	UdpClient udpClient;
		bool initialized = false;
		IDirigentControl localOps;

		const string MAGIC_STR = "Dirigent.AppState:";
		byte[] MAGIC = System.Text.Encoding.ASCII.GetBytes(MAGIC_STR);

		IPAddress mcastIP;
		IPAddress localIP;
		int mcastPort;

		public AppStateUpdater( string mcastIP, int mcastPort, string localIP, IDirigentControl localOps )
		{
			this.localOps = localOps;
			this.mcastIP = IPAddress.Parse( mcastIP );
			this.mcastPort = mcastPort;
			this.localIP = IPAddress.Parse( localIP );

			udpClient = new UdpClient(AddressFamily.InterNetwork);
			udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
			udpClient.EnableBroadcast = true;
			udpClient.Client.Bind( new IPEndPoint( this.localIP, mcastPort ) );
			udpClient.JoinMulticastGroup( this.mcastIP );
			udpClient.MulticastLoopback = true;

			MAGIC = System.Text.Encoding.ASCII.GetBytes(MAGIC_STR);

			try
			{
				udpClient.BeginReceive( new AsyncCallback( recv ), null );
				initialized = true;
			}
			catch ( Exception e )
			{
				log.Error( e.ToString() );
				initialized = false;
			}
		}


		public void Dispose()
		{
			if( udpClient != null )
			{
				udpClient.Close();
				udpClient = null;
			}
		}

		bool StartsWithMagic( byte[] buf )
		{
			if( buf.Length < MAGIC.Length )
				return false;

			for (int i = 0; i < MAGIC.Length; i++)
			{
				if( buf[i] != MAGIC[i] )
					return false;
			}

			return true;
		}

		Queue<byte[]> receivedPackets = new Queue<byte[]>();

		private void recv( IAsyncResult res )
		{
			if ( udpClient == null ) return;

			try	// necessary to catch the exception when the socket is closed
			{
				IPEndPoint RemoteIpEndPoint = new IPEndPoint( IPAddress.Any, mcastPort );
				byte[] receivedBytes = udpClient.EndReceive( res, ref RemoteIpEndPoint );
				udpClient.BeginReceive( new AsyncCallback( recv ), null );

				lock(receivedPackets)
				{
					receivedPackets.Enqueue( receivedBytes );
				}

			}
			catch
			{
			}
		}

		public void Tick()
		{
			if( !initialized ) return;

			// process received packet queue
			lock( receivedPackets )
			{
				while( receivedPackets.Count > 0 )
				{
					var buf = receivedPackets.Dequeue();
					UpdateRemoteAppsFromBuffer( buf );
				}
			}

			// send local apps state
			var outgoing = WriteLocalAppsToBuffer();

			var remoteEndpt = new IPEndPoint( mcastIP, mcastPort );
			udpClient.Send( outgoing, outgoing.Length, remoteEndpt );
		}

		void UpdateRemoteAppsFromBuffer( byte[] buf )
		{
			if( !StartsWithMagic( buf ) )
				return;

			try  
			{  
				using (var stream = new System.IO.MemoryStream( buf, MAGIC.Length, buf.Length - MAGIC.Length ))  
				{  
					var appStateList = ProtoBuf.Serializer.Deserialize<Dictionary<AppIdTuple, AppState>>(stream);  
					foreach( var kv in appStateList )
					{
						if( !localOps.IsLocalApp( kv.Key ) )
						{
							localOps.SetRemoteAppState( kv.Key, kv.Value );
						}
					}
				}  
			}  
			catch  
			{ 
				// Log error
				throw;  
			}
		}

		byte[] WriteLocalAppsToBuffer()
		{
            var localAppsState = GetLocalApps();
            
			using( var stream = new MemoryStream() )  
			{  
				stream.Write( MAGIC, 0, MAGIC.Length );
				ProtoBuf.Serializer.Serialize(stream, localAppsState );  
				return stream.ToArray();  
			}  
		}

		Dictionary<AppIdTuple, AppState> GetLocalApps()
		{
            Dictionary<AppIdTuple, AppState> localAppsState = new Dictionary<AppIdTuple, AppState>();

			foreach (var plan in localOps.GetPlanRepo())
			{
				foreach (var pair in localOps.GetAllAppsState())
				{
					var appId = pair.Key;
					var appState = pair.Value;

					if( localOps.IsLocalApp( appId ) )
					{
						localAppsState[appId] = appState;
					}
				}
			}

			return localAppsState;

		}



    }

}
