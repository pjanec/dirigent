using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.Net;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// The master and its clients must speak the same protocol version. A client that does not - or that is so
	/// old that its first message cannot even be read - is told so, in the logs of both, and disconnected.
	/// </summary>
	[TestClass()]
	public class ProtocolVersionTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 20 );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		/// <summary>Lines the master wrote; the client in the same process writes its own, which merely quote it.</summary>
		static int Count( string text ) => Diagnostics.LogMessages().Count( x => x.Contains( text ) && !x.StartsWith( "The master refused" ) );

		[TestMethod()]
		public async Task AClientSpeakingAnotherProtocolVersionIsRefusedAndToldWhy()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = Scenario.OneMachine(), SecurityOff = true /* about the plain port: raw plain clients, plain-port log lines */ } );

			var ident = new ClientIdent( "oldtimer", EMsgRecipCateg.Gui ) { ProtocolVersion = Protocol.Version - 1 };
			using var client = new Net.Client( ident, "127.0.0.1", bed.MasterPort, autoConn: true );
			string? reason = null;
			client.Rejected = r => reason = r;

			await bed.WaitUntilAsync( () => { client.Tick(); return reason != null; }, Timeout, "the master tells the client it is refused" );

			StringAssert.Contains( reason, $"oldtimer at 127.0.0.1 speaks protocol {Protocol.Version - 1}, this master {Protocol.Version} - update it to {Protocol.ProductVersion}" );
			Assert.AreEqual( reason, client.RejectedReason );

			// the master said it too, once
			Assert.AreEqual( 1, Count( "oldtimer at 127.0.0.1 speaks protocol" ), "the master logs the refusal" );
			// and the client logged the reason as an error
			Assert.AreEqual( 1, Diagnostics.LogMessages().Count( x => x.StartsWith( "The master refused this client:" ) ), "the client logs the refusal" );

			// the session is closed and the client is not a client of the master
			await bed.WaitUntilAsync( () => { client.Tick(); return !client.IsConnected; }, Timeout, "the master closes the session" );
			var clients = await bed.Operator.GetAllClientsStateAsync();
			Assert.IsFalse( clients.Any( c => c.Key == "oldtimer" ), "the refused client never got on the master's list" );
			Assert.AreEqual( 0, Count( "=> oldtimer" ), "the refused client was never identified" );
		}

		[TestMethod()]
		public async Task ARefusedClientAsksAgainSlowly()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = Scenario.OneMachine(), SecurityOff = true /* about the plain port: raw plain clients, plain-port log lines */ } );

			var ident = new ClientIdent( "oldtimer", EMsgRecipCateg.Gui ) { ProtocolVersion = Protocol.Version + 1 };
			using var client = new Net.Client( ident, "127.0.0.1", bed.MasterPort, autoConn: true );

			await bed.WaitUntilAsync( () => Count( "oldtimer at" ) >= 1, Timeout, "the first refusal" );

			// a normal reconnect comes after a second; a refused client waits the long interval
			await Task.Delay( TimeSpan.FromSeconds( 4 ) );
			Assert.AreEqual( 1, Count( "oldtimer at" ), "the refused client does not retry at the normal rate" );
		}

		[TestMethod()]
		public async Task GarbageAsTheFirstMessageClosesTheSessionAndTheMasterSaysWhy()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = Scenario.OneMachine(), SecurityOff = true /* about the plain port: raw plain clients, plain-port log lines */ } );

			var garbage = Enumerable.Range( 0, 40 ).Select( i => (byte) ( 0xC1 + i % 7 ) ).ToArray(); // 0xC1 is never valid MessagePack
			await SendAndExpectClose( bed.MasterPort, garbage );

			Assert.AreEqual( 1, Count( "a client at 127.0.0.1 sent a message this master cannot read - it probably runs an older Dirigent than " + Protocol.ProductVersion + "; update it" ) );

			// again, from the same address: the session is closed again, the line is not repeated
			await SendAndExpectClose( bed.MasterPort, garbage );
			Assert.AreEqual( 1, Count( "sent a message this master cannot read" ), "once per address per ten minutes" );
		}

		[TestMethod()]
		public async Task TheFirstMessageOfAnOlderDirigentIsRecognisedAsUnreadable()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = Scenario.OneMachine(), SecurityOff = true /* about the plain port: raw plain clients, plain-port log lines */ } );

			// 3.1.19 wrote every object as a map with the field names as text
			var oldIdent = new Dictionary<string, object>() { { "Sender", "m9" }, { "SubscribedTo", 1 } };
			var body = MessagePack.MessagePackSerializer.Serialize( new object[] { 121, oldIdent }, MessagePack.Resolvers.ContractlessStandardResolver.Options );
			await SendAndExpectClose( bed.MasterPort, body );

			Assert.AreEqual( 1, Count( "sent a message this master cannot read" ) );
		}

		[TestMethod()]
		public async Task NormalClientsAreNotAffected()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = Scenario.TwoMachines(), GuiPerMachine = true } );

			var clients = await bed.Operator.GetAllClientsStateAsync();
			Assert.AreEqual( 2, clients.Count, "both agents are connected (the GUIs are not listed as clients)" );
			Assert.IsTrue( clients.All( c => c.Value.Ident.ProtocolVersion == Protocol.Version ), "everyone says the current protocol version" );

			Assert.AreEqual( 0, Count( "speaks protocol" ) );
			Assert.AreEqual( 0, Count( "cannot read" ) );
		}

		/// <summary>Sends one framed message body and waits for the master to close the connection.</summary>
		static async Task SendAndExpectClose( int port, byte[] body )
		{
			using var tcp = new TcpClient();
			await tcp.ConnectAsync( "127.0.0.1", port );
			var stream = tcp.GetStream();
			var frame = BitConverter.GetBytes( (uint) body.Length ).Concat( body ).ToArray();
			await stream.WriteAsync( frame );

			var buf = new byte[1024];
			var read = stream.ReadAsync( buf ).AsTask();
			var done = await Task.WhenAny( read, Task.Delay( Timeout ) );
			Assert.AreSame( read, done, "the master closes the session" );
			int n;
			try { n = await read; } catch( System.IO.IOException ) { n = 0; }
			Assert.AreEqual( 0, n, "nothing is said to a client that cannot be understood; the connection is just closed" );
		}
	}
}
