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
	[ProtoBuf.ProtoContract]
	[DataContract]
	public class MachineDef : IEquatable<MachineDef>
	{
		/// <summary>
		/// Unique machine id in the system
		/// </summary>
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public string Id = String.Empty;

		/// <summary>
		/// Machine IP address used for constructing UNC file paths to the machine.
		/// Should stay empty to be auto-determined from the TCP connection.
		/// If not empty, it overrides the auto-determined IP
		/// </summary>
		[ProtoBuf.ProtoMember( 2 )]
		[DataMember]
		public string IP = String.Empty;

		/// <summary>
		/// File shares the machine defines; to be used for remote file access
		/// </summary>
		[ProtoBuf.ProtoMember( 3 )]
		[DataMember]
		public List<FileShareDef> FileShares = new List<FileShareDef>();

		[ProtoBuf.ProtoMember( 4 )]
		[DataMember]
		public List<VfsNodeDef> VfsNodes = new List<VfsNodeDef>();

		[ProtoBuf.ProtoMember( 6 )]
		[DataMember]
		public List<ActionDef> Actions = new List<ActionDef>();

		public bool ThisEquals( MachineDef other ) =>
				this.Id == other.Id &&
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
