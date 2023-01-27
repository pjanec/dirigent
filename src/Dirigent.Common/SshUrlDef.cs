using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Dirigent
{

	/// <summary>
	/// Definition of a SshUrl for a machine
	/// </summary>
	//[MessagePack.MessagePackObject]
	public class SshUrlDef : IEquatable<SshUrlDef>
	{
		/// <summary>
		/// Unique name of the share
		/// </summary>
		//[MessagePack.Key( 2 )]
		public string UrlPrefix = String.Empty;

		/// <summary>
		/// Local folder path (full one, from root, including drive letter)
		/// </summary>
		//[MessagePack.Key( 3 )]
		public string Path = String.Empty;


		public bool ThisEquals( SshUrlDef other ) =>
				this.UrlPrefix == other.UrlPrefix &&
				this.Path == other.Path &&
				true;

		// boilerplate
		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(SshUrlDef? o) => object.Equals(this, o);
		public static bool operator ==(SshUrlDef o1, SshUrlDef o2) => object.Equals(o1, o2);
		public static bool operator !=(SshUrlDef o1, SshUrlDef o2) => !object.Equals(o1, o2);
		public override int GetHashCode() => UrlPrefix.GetHashCode() ^ Path.GetHashCode();

		public override string ToString()
		{
			return $"{UrlPrefix} -> {Path}";
		}
	}

}
