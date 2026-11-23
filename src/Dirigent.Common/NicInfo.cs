using System;
using System.Collections.Generic;

namespace Dirigent
{
	/// <summary>
	/// One IPv4 address of a network adapter together with the adapter's MAC address.
	/// </summary>
	/// <remarks>
	/// What Wake-on-LAN needs from a machine that is currently off: the MAC to put in the packet and
	/// the subnet to aim it at. Only the machine itself can tell either, so agents report them when
	/// they connect and the master remembers them.
	///
	/// One entry per (MAC, IPv4 address) pair: an adapter with several addresses yields several entries
	/// with the same MAC.
	/// </remarks>
	[MessagePack.MessagePackObject]
	public class NicInfo : IEquatable<NicInfo>
	{
		/// <summary>The adapter's MAC in the normalized form <c>AA-BB-CC-DD-EE-FF</c>.</summary>
		[MessagePack.Key( 1 )]
		public string Mac = string.Empty;

		/// <summary>The IPv4 address, dotted decimal.</summary>
		[MessagePack.Key( 2 )]
		public string Ip = string.Empty;

		/// <summary>Length of the subnet prefix; 24 for 255.255.255.0.</summary>
		[MessagePack.Key( 3 )]
		public int PrefixLength;

		public NicInfo() {}

		public NicInfo( string mac, string ip, int prefixLength )
		{
			Mac = mac;
			Ip = ip;
			PrefixLength = prefixLength;
		}

		public bool Equals( NicInfo? other ) =>
			other is not null && Mac == other.Mac && Ip == other.Ip && PrefixLength == other.PrefixLength;

		public override bool Equals( object? obj ) => Equals( obj as NicInfo );

		public override int GetHashCode() => HashCode.Combine( Mac, Ip, PrefixLength );

		public override string ToString() => $"{Mac} {Ip}/{PrefixLength}";
	}
}
