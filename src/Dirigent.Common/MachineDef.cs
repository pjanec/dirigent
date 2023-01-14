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
	/// Definition of a machine (computer) in the whole system
	/// </summary>
	//[MessagePack.MessagePackObject]
	public class MachineDef : IEquatable<MachineDef>
	{
		/// <summary>
		/// Unique machine id in the system
		/// </summary>
		//[MessagePack.Key( 1 )]
		public string Id = String.Empty;

		/// <summary>
		/// Machine IP address used for constructing UNC file paths to the machine.
		/// Should stay empty to be auto-determined from the TCP connection.
		/// If not empty, it overrides the auto-determined IP
		/// </summary>
		//[MessagePack.Key( 2 )]
		public string IP = String.Empty;

		// format: "00:00:00:00:00:00"
		// empty = not specified
		public string MAC = String.Empty;

		/// <summary>
		/// File shares the machine defines; to be used for remote file access
		/// </summary>
		//[MessagePack.Key( 3 )]
		public List<FileShareDef> FileShares = new List<FileShareDef>();

		//[MessagePack.Key( 4 )]
		public List<VfsNodeDef> VfsNodes = new List<VfsNodeDef>();

		//[MessagePack.Key( 6 )]
		public List<ActionDef> Actions = new List<ActionDef>();

		public bool ThisEquals( MachineDef other ) =>
				this.Id == other.Id &&
				this.IP == other.IP &&
				this.MAC == other.MAC &&
				this.FileShares.SequenceEqual( other.FileShares ) &&
				this.VfsNodes.SequenceEqual( other.VfsNodes ) &&
				this.Actions.SequenceEqual( other.Actions ) &&
				true;

		// boilerplate
		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(MachineDef? o) => object.Equals(this, o);
		public static bool operator ==(MachineDef o1, MachineDef o2) => object.Equals(o1, o2);
		public static bool operator !=(MachineDef o1, MachineDef o2) => !object.Equals(o1, o2);
		public override int GetHashCode() => Id.GetHashCode();


		public override string ToString()
		{
			return $"{Id}";
		}
	}

}
