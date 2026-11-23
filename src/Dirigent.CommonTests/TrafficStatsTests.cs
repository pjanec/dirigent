using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.Net;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Dirigent.Tests
{
	/// <summary>
	/// What went over the wire, counted per message type - on a real server and client, and by the counter itself.
	/// </summary>
	[TestClass()]
	public class TrafficStatsTests
	{
		static int FreePort()
		{
			var l = new TcpListener( IPAddress.Loopback, 0 );
			l.Start();
			var port = ( (IPEndPoint) l.LocalEndpoint ).Port;
			l.Stop();
			return port;
		}

		/// <summary>The framed size of a message: header and body, as put on the wire.</summary>
		static long FramedSize( Message msg )
		{
			var ms = new System.IO.MemoryStream();
			MsgPackCodec.Serialize( ms, msg );
			return ms.Position;
		}

		static void WaitFor( Func<bool> condition, string what, Action? pump = null )
		{
			var clock = Stopwatch.StartNew();
			while( !condition() )
			{
				Assert.IsTrue( clock.Elapsed < TimeSpan.FromSeconds( 10 ), $"timed out waiting for {what}" );
				pump?.Invoke();
				Thread.Sleep( 10 );
			}
		}

		[TestMethod()]
		public void TheCounterAddsUpPerTypeAndDirectionAndSortsByBytesTest()
		{
			var stats = new TrafficStats();
			stats.Sent( typeof( KillAllStatus ), 10 );
			stats.Sent( typeof( KillAllStatus ), 10 );
			stats.Sent( typeof( ClientStateMessage ), 500 );
			stats.Received( typeof( KillAllStatus ), 7 );

			var r = stats.Snapshot();

			Assert.AreEqual( 2, r.sent.Count );
			Assert.AreEqual( "ClientStateMessage", r.sent[0].type, "the biggest first" );
			Assert.AreEqual( 1, r.sent[0].messages );
			Assert.AreEqual( 500, r.sent[0].bytes );
			Assert.AreEqual( 2, r.sent[1].messages );
			Assert.AreEqual( 20, r.sent[1].bytes );
			Assert.AreEqual( 3, r.sentTotal.messages );
			Assert.AreEqual( 520, r.sentTotal.bytes );
			Assert.AreEqual( 1, r.received.Count );
			Assert.AreEqual( 7, r.receivedTotal.bytes );

			stats.Reset();
			var after = stats.Snapshot();
			Assert.AreEqual( 0, after.sent.Count );
			Assert.AreEqual( 0, after.receivedTotal.bytes );
		}

		[TestMethod()]
		public void AServerAndAClientCountWhatTheySendAndReceiveTest()
		{
			var port = FreePort();
			var serverStats = new TrafficStats();
			var clientStats = new TrafficStats();

			using var server = new Server( port, serverStats );
			using var client = new Client( new ClientIdent( "c1", EMsgRecipCateg.Gui ), "127.0.0.1", port, autoConn: true, traffic: clientStats );

			// the client says who it is as the first thing it does
			WaitFor( () => server.Clients.Any(), "the client to be identified", () => server.Tick() );

			// three messages up ...
			for( int i = 0; i < 3; i++ ) client.Send( new KillAllStatus( true ) );
			var up = FramedSize( new KillAllStatus( true ) { Sender = "c1" } );

			WaitFor( () => serverStats.Snapshot().received.Any( t => t.type == nameof( KillAllStatus ) && t.messages == 3 ),
				"three messages to arrive", () => server.Tick() );

			// ... and two down, to everybody subscribed
			for( int i = 0; i < 2; i++ ) server.SendToAllSubscribed( new KillAllStatus( false ), EMsgRecipCateg.All );
			server.SendToSingle( new KillAllStatus( false ), "c1" );
			var down = FramedSize( new KillAllStatus( false ) );

			WaitFor( () => clientStats.Snapshot().received.Any( t => t.type == nameof( KillAllStatus ) && t.messages == 3 ),
				"three messages to arrive", () => client.Tick() );

			var s = serverStats.Snapshot();
			var c = clientStats.Snapshot();

			TrafficStats.TypeTraffic Of( System.Collections.Generic.List<TrafficStats.TypeTraffic> l ) => l.Single( t => t.type == nameof( KillAllStatus ) );

			Assert.AreEqual( 3, Of( s.received ).messages );
			Assert.AreEqual( 3 * up, Of( s.received ).bytes, "the framed bytes: header and body" );
			Assert.AreEqual( 3, Of( s.sent ).messages, "two to all subscribed (one client) and one to a single client" );
			Assert.AreEqual( 3 * down, Of( s.sent ).bytes );

			Assert.AreEqual( 3, Of( c.sent ).messages );
			Assert.AreEqual( 3 * up, Of( c.sent ).bytes );
			Assert.AreEqual( 3, Of( c.received ).messages );
			Assert.AreEqual( 3 * down, Of( c.received ).bytes );

			Assert.AreEqual( 1, s.received.Single( t => t.type == nameof( ClientIdent ) ).messages, "the identification is traffic too" );
		}
	}
}
