using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace Dirigent
{
	/// <summary>
	/// Wake-on-LAN: building the magic packet, and getting it out of a machine with several network
	/// interfaces.
	/// </summary>
	public static class WakeOnLan
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		/// <summary>The discard port, which is where sleeping network cards are conventionally told to listen.</summary>
		public const int Port = 9;

		public const string LimitedBroadcast = "255.255.255.255";

		/// <summary>
		/// One datagram to send: from which local address (null = let the system choose), to where, waking whom.
		/// </summary>
		public readonly record struct Datagram( string? LocalIp, string DestinationIp, string Mac );

		/// <summary>
		/// The 102-byte magic packet: six 0xFF bytes followed by the MAC repeated sixteen times.
		/// </summary>
		public static byte[] BuildMagicPacket( string mac )
		{
			var bytes = ParseMac( mac );
			var packet = new byte[6 + 16 * 6];

			for( int i = 0; i < 6; i++ ) packet[i] = 0xFF;
			for( int i = 0; i < 16; i++ ) bytes.CopyTo( packet, 6 + 6 * i );

			return packet;
		}

		/// <summary>
		/// Reads a MAC written with '-', ':' or '.' as the separator or with none, in either case.
		/// </summary>
		/// <exception cref="ArgumentException">anything that is not six hex bytes</exception>
		public static byte[] ParseMac( string mac )
		{
			if( mac is null ) throw new ArgumentException( "MAC address is missing", nameof( mac ) );

			var digits = mac.Trim();
			foreach( var sep in new[] { '-', ':', '.' } )
			{
				if( digits.Contains( sep ) )
				{
					// one kind of separator only, between every two digits
					var parts = digits.Split( sep );
					if( parts.Length != 6 || parts.Any( p => p.Length != 2 ) )
						throw new ArgumentException( $"'{mac}' is not a MAC address", nameof( mac ) );
					digits = string.Concat( parts );
					break;
				}
			}

			if( digits.Length != 12 || !digits.All( Uri.IsHexDigit ) )
				throw new ArgumentException( $"'{mac}' is not a MAC address", nameof( mac ) );

			var result = new byte[6];
			for( int i = 0; i < 6; i++ )
			{
				result[i] = byte.Parse( digits.AsSpan( 2 * i, 2 ), NumberStyles.HexNumber, CultureInfo.InvariantCulture );
			}
			return result;
		}

		/// <summary>The MAC in the normalized form <c>AA-BB-CC-DD-EE-FF</c>.</summary>
		public static string NormalizeMac( string mac ) => BitConverter.ToString( ParseMac( mac ) );

		/// <summary>
		/// The address that reaches every host of the subnet the address belongs to.
		/// </summary>
		public static string DirectedBroadcast( string ip, int prefixLength )
		{
			if( !IPAddress.TryParse( ip, out var addr ) || addr.AddressFamily != AddressFamily.InterNetwork )
				throw new ArgumentException( $"'{ip}' is not an IPv4 address", nameof( ip ) );
			if( prefixLength < 0 || prefixLength > 32 )
				throw new ArgumentException( $"prefix length {prefixLength} is out of range", nameof( prefixLength ) );

			var b = addr.GetAddressBytes();
			uint value = ( (uint) b[0] << 24 ) | ( (uint) b[1] << 16 ) | ( (uint) b[2] << 8 ) | b[3];
			uint hostMask = prefixLength == 0 ? uint.MaxValue : ~( uint.MaxValue << ( 32 - prefixLength ) );
			value |= hostMask;

			return $"{value >> 24}.{( value >> 16 ) & 0xFF}.{( value >> 8 ) & 0xFF}.{value & 0xFF}";
		}

		/// <summary>
		/// Decides what to send, without sending anything.
		/// </summary>
		/// <param name="macs">the addresses to wake; usually one</param>
		/// <param name="targetNics">what is known of the target's adapters; only their subnets are used</param>
		/// <param name="localAddresses">IPv4 addresses of the sending machine</param>
		/// <remarks>
		/// Every packet goes to the limited broadcast and to the directed broadcast of every subnet the
		/// target is known to live in - the latter reaches it through routers that forward directed
		/// broadcasts. And every packet goes out from every local address, each bound separately: on a
		/// machine with several interfaces the limited broadcast leaves through one of them only, and
		/// which one is not up to us - possibly not the one the target is on.
		/// </remarks>
		public static List<Datagram> Plan( IEnumerable<string> macs, IEnumerable<NicInfo> targetNics, IEnumerable<string> localAddresses )
		{
			var destinations = new List<string>() { LimitedBroadcast };
			foreach( var nic in targetNics )
			{
				string directed;
				try { directed = DirectedBroadcast( nic.Ip, nic.PrefixLength ); }
				catch( ArgumentException ) { continue; } // a bad remembered entry must not prevent waking by the others
				if( !destinations.Contains( directed ) ) destinations.Add( directed );
			}

			var locals = localAddresses.Distinct().Cast<string?>().ToList();
			if( locals.Count == 0 ) locals.Add( null ); // unbound, better than nothing

			var plan = new List<Datagram>();
			foreach( var mac in macs.Distinct() )
			{
				foreach( var local in locals )
				{
					foreach( var dest in destinations )
					{
						plan.Add( new Datagram( local, dest, mac ) );
					}
				}
			}
			return plan;
		}

		/// <summary>
		/// Sends the planned datagrams. One that fails is logged and does not stop the rest.
		/// </summary>
		/// <returns>how many were sent</returns>
		public static int Send( IEnumerable<Datagram> plan )
		{
			int sent = 0;
			foreach( var s in plan )
			{
				try
				{
					var packet = BuildMagicPacket( s.Mac );
					using var udp = s.LocalIp is null
						? new UdpClient()
						: new UdpClient( new IPEndPoint( IPAddress.Parse( s.LocalIp ), 0 ) );
					udp.EnableBroadcast = true;
					udp.Send( packet, packet.Length, new IPEndPoint( IPAddress.Parse( s.DestinationIp ), Port ) );
					sent++;
				}
				catch( Exception e )
				{
					log.Warn( $"Wake-on-LAN of {s.Mac} to {s.DestinationIp} from {s.LocalIp ?? "(any)"} failed: {e.Message}" );
				}
			}
			return sent;
		}
	}
}
