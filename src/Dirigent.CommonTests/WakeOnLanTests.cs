using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent.Tests
{
	/// <summary>
	/// The magic packet and the planning of where to send it. Nothing here touches the network.
	/// </summary>
	[TestClass()]
	public class WakeOnLanTests
	{
		static readonly byte[] Mac = { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0x0F };

		[TestMethod()]
		public void MagicPacketIs102BytesOfSixFFsThenTheMacSixteenTimes()
		{
			var packet = WakeOnLan.BuildMagicPacket( "AA-BB-CC-DD-EE-0F" );

			Assert.AreEqual( 102, packet.Length );
			for( int i = 0; i < 6; i++ ) Assert.AreEqual( 0xFF, packet[i], $"sync byte {i}" );
			for( int rep = 0; rep < 16; rep++ )
			{
				CollectionAssert.AreEqual( Mac, packet.Skip( 6 + 6 * rep ).Take( 6 ).ToArray(), $"repetition {rep}" );
			}
		}

		[DataTestMethod()]
		[DataRow( "AA-BB-CC-DD-EE-0F" )]
		[DataRow( "aa:bb:cc:dd:ee:0f" )]
		[DataRow( "aa.bb.cc.dd.ee.0f" )]
		[DataRow( "AABBCCDDEE0F" )]
		[DataRow( "  aabbccddee0F " )]
		public void MacIsAcceptedWithAnySeparatorOrNone( string text )
		{
			CollectionAssert.AreEqual( Mac, WakeOnLan.ParseMac( text ) );
		}

		[DataTestMethod()]
		[DataRow( "" )]
		[DataRow( "AA-BB-CC-DD-EE" )]
		[DataRow( "AA-BB-CC-DD-EE-0F-11" )]
		[DataRow( "AA-BB-CC-DD-EE-GG" )]
		[DataRow( "AA-BB:CC-DD-EE-0F" )]
		[DataRow( "A-BB-CC-DD-EE-0F" )]
		[DataRow( "AABBCCDDEE0" )]
		[DataRow( "AABBCCDDEE0F00" )]
		public void ABadMacIsRejected( string text )
		{
			Assert.ThrowsException<ArgumentException>( () => WakeOnLan.ParseMac( text ) );
			Assert.ThrowsException<ArgumentException>( () => WakeOnLan.BuildMagicPacket( text ) );
		}

		[TestMethod()]
		public void MacIsNormalizedToDashesAndUpperCase()
		{
			Assert.AreEqual( "AA-BB-CC-DD-EE-0F", WakeOnLan.NormalizeMac( "aa:bb:cc:dd:ee:0f" ) );
		}

		[DataTestMethod()]
		[DataRow( "192.168.1.37", 24, "192.168.1.255" )]
		[DataRow( "10.20.30.40", 16, "10.20.255.255" )]
		[DataRow( "10.20.30.40", 8, "10.255.255.255" )]
		[DataRow( "192.168.1.37", 22, "192.168.3.255" )]
		[DataRow( "192.168.1.37", 32, "192.168.1.37" )]
		[DataRow( "192.168.1.37", 0, "255.255.255.255" )]
		public void DirectedBroadcastOfTheSubnet( string ip, int prefix, string expected )
		{
			Assert.AreEqual( expected, WakeOnLan.DirectedBroadcast( ip, prefix ) );
		}

		[TestMethod()]
		public void DirectedBroadcastRejectsNonsense()
		{
			Assert.ThrowsException<ArgumentException>( () => WakeOnLan.DirectedBroadcast( "not an ip", 24 ) );
			Assert.ThrowsException<ArgumentException>( () => WakeOnLan.DirectedBroadcast( "1.2.3.4", 33 ) );
			Assert.ThrowsException<ArgumentException>( () => WakeOnLan.DirectedBroadcast( "1.2.3.4", -1 ) );
		}

		[TestMethod()]
		public void EveryLocalAddressSendsToEveryDestination()
		{
			var targetNics = new[] { new NicInfo( "AA-BB-CC-DD-EE-0F", "192.168.1.37", 24 ) };

			var plan = WakeOnLan.Plan( new[] { "AA-BB-CC-DD-EE-0F" }, targetNics, new[] { "10.0.0.5", "192.168.1.5" } );

			var expected = new[]
			{
				new WakeOnLan.Datagram( "10.0.0.5",    "255.255.255.255", "AA-BB-CC-DD-EE-0F" ),
				new WakeOnLan.Datagram( "10.0.0.5",    "192.168.1.255",   "AA-BB-CC-DD-EE-0F" ),
				new WakeOnLan.Datagram( "192.168.1.5", "255.255.255.255", "AA-BB-CC-DD-EE-0F" ),
				new WakeOnLan.Datagram( "192.168.1.5", "192.168.1.255",   "AA-BB-CC-DD-EE-0F" ),
			};
			CollectionAssert.AreEquivalent( expected, plan );
		}

		[TestMethod()]
		public void NoLocalAddressMeansOneUnboundSend()
		{
			var plan = WakeOnLan.Plan( new[] { "AA-BB-CC-DD-EE-0F" }, new NicInfo[0], new string[0] );

			CollectionAssert.AreEqual(
				new[] { new WakeOnLan.Datagram( null, "255.255.255.255", "AA-BB-CC-DD-EE-0F" ) }, plan );
		}

		[TestMethod()]
		public void SubnetsSharedByTheTargetsAdaptersAreAimedAtOnce()
		{
			var targetNics = new[]
			{
				new NicInfo( "AA-BB-CC-DD-EE-0F", "192.168.1.37", 24 ),
				new NicInfo( "AA-BB-CC-DD-EE-0F", "192.168.1.38", 24 ),
				new NicInfo( "11-22-33-44-55-66", "10.1.2.3", 16 ),
			};

			var plan = WakeOnLan.Plan( new[] { "AA-BB-CC-DD-EE-0F" }, targetNics, new[] { "10.0.0.5" } );

			CollectionAssert.AreEquivalent(
				new[] { "255.255.255.255", "192.168.1.255", "10.1.255.255" },
				plan.Select( p => p.DestinationIp ).ToList() );
		}

		[TestMethod()]
		public void ConfiguredMacAloneIsPlannedWhileCachedSubnetsAreStillUsed()
		{
			// what Master.WakeUp feeds the plan when SharedConfig gives a MAC: only that MAC,
			// but the subnets come from what the machine reported
			var cached = new[] { new NicInfo( "AA-BB-CC-DD-EE-0F", "192.168.1.37", 24 ) };

			var plan = WakeOnLan.Plan( new[] { "02-00-00-00-00-01" }, cached, new[] { "10.0.0.5" } );

			Assert.IsTrue( plan.All( p => p.Mac == "02-00-00-00-00-01" ), "the configured MAC only" );
			CollectionAssert.AreEquivalent(
				new[] { "255.255.255.255", "192.168.1.255" }, plan.Select( p => p.DestinationIp ).ToList() );
		}

		[TestMethod()]
		public void EveryCachedMacIsPlannedWhenNothingIsConfigured()
		{
			var cached = new[]
			{
				new NicInfo( "AA-BB-CC-DD-EE-0F", "192.168.1.37", 24 ),
				new NicInfo( "11-22-33-44-55-66", "10.1.2.3", 24 ),
			};

			var plan = WakeOnLan.Plan( cached.Select( n => n.Mac ), cached, new[] { "10.0.0.5" } );

			CollectionAssert.AreEquivalent(
				new[] { "AA-BB-CC-DD-EE-0F", "11-22-33-44-55-66" },
				plan.Select( p => p.Mac ).Distinct().ToList() );
			Assert.AreEqual( 2 * 3, plan.Count, "two MACs, three destinations each, one local address" );
		}

		[TestMethod()]
		public void ABadRememberedAddressDoesNotSpoilThePlan()
		{
			var cached = new[] { new NicInfo( "AA-BB-CC-DD-EE-0F", "garbage", 24 ) };

			var plan = WakeOnLan.Plan( new[] { "AA-BB-CC-DD-EE-0F" }, cached, new[] { "10.0.0.5" } );

			Assert.AreEqual( 1, plan.Count );
			Assert.AreEqual( "255.255.255.255", plan[0].DestinationIp );
		}
	}
}
