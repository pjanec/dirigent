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
	/// Definition of file package associated with a machine, with an application on a machine or with no association (a global file package)
	/// </summary>
	[ProtoBuf.ProtoContract]
	[DataContract]
	public class FilePackageDef : IEquatable<FilePackageDef>
	{
		/// <summary>
		/// Unique package id
		/// </summary>
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public Guid Guid;

		/// <summary>
		/// Human readable package id
		/// </summary>
		[ProtoBuf.ProtoMember( 2 )]
		[DataMember]
		public string Id = string.Empty;

		/// <summary>
		/// Machine the package belongs to. Null if global file.
		/// </summary>
		[ProtoBuf.ProtoMember( 3 )]
		[DataMember]
		public string? MachineId;

		/// <summary>
		/// App the package belongs to. Used only if the file is bound to an app.
		/// </summary>
		[ProtoBuf.ProtoMember( 4 )]
		[DataMember]
		public string? AppId;

		/// <summary>
		/// List of the files within the package; references to FileDefs
		/// </summary>
		[ProtoBuf.ProtoMember(5)]
		[DataMember]
		public List<Guid> Files = new List<Guid>();


		public bool Equals( FilePackageDef? other )
		{
			if( other is null )
				return false;

			if(
				this.Guid == other.Guid &&
				this.Id == other.Id &&
				this.MachineId == other.MachineId &&
				this.AppId == other.AppId &&
				this.Files.SequenceEqual( other.Files ) &&
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

			var typed = obj as FilePackageDef;
			if( typed is null )
				return false;
			else
				return Equals( typed );
		}

		public override int GetHashCode()
		{
			return this.Guid.GetHashCode();
		}

		public static bool operator ==( FilePackageDef t1, FilePackageDef t2 )
		{
			if( ( object )t1 == null || ( ( object )t2 ) == null )
				return Object.Equals( t1, t2 );

			return t1.Equals( t2 );
		}

		public static bool operator !=( FilePackageDef t1, FilePackageDef t2 )
		{
			if( t1 is null || t2 is null )
				return !Object.Equals( t1, t2 );

			return !( t1.Equals( t2 ) );
		}


		public override string ToString()
		{
			return $"{Id}@{MachineId}.{AppId} ({Files.Count} files)";
		}
	}

}
