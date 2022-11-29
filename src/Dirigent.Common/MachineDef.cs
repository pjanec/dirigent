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

		/// <summary>
		/// Files associated with the machine only (not associated with an application).
		/// References to a global table of files.
		/// </summary>
		[ProtoBuf.ProtoMember( 4 )]
		[DataMember]
		public List<Guid> Files = new List<Guid>();

		/// <summary>
		/// Machine-specific file packages.
		/// References to a global table of file packages.
		/// </summary>
		[ProtoBuf.ProtoMember( 5 )]
		[DataMember]
		public List<Guid> FilePackages = new List<Guid>();

		[ProtoBuf.ProtoMember( 6 )]
		[DataMember]
		public List<ToolRef> Tools = new List<ToolRef>();


		public bool Equals( MachineDef? other )
		{
			if( other is null )
				return false;

			if(
				this.Id == other.Id &&
				this.FileShares.SequenceEqual( other.FileShares ) &&
				this.Files.SequenceEqual( other.Files ) &&
				this.FilePackages.SequenceEqual( other.FilePackages ) &&
				this.Tools.SequenceEqual( other.Tools ) &&
				true
			)
				return true;
			else
				return false;
		}

		public override bool Equals( Object? obj )
		{
			if( obj == null )
				return false;

			var typed = obj as MachineDef;
			if( typed is null )
				return false;
			else
				return Equals( typed );
		}

		public override int GetHashCode()
		{
			return this.Id.GetHashCode();
		}

		public static bool operator ==( MachineDef t1, MachineDef t2 )
		{
			if( ( object )t1 == null || ( ( object )t2 ) == null )
				return Object.Equals( t1, t2 );

			return t1.Equals( t2 );
		}

		public static bool operator !=( MachineDef t1, MachineDef t2 )
		{
			if( t1 is null || t2 is null )
				return !Object.Equals( t1, t2 );

			return !( t1.Equals( t2 ) );
		}


		public override string ToString()
		{
			return $"{Id}";
		}
	}

}
