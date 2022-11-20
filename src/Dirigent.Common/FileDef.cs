using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Dirigent
{

	public enum EFLookupType
	{
		Path,
		Newest,
	}

	/// <summary>
	/// Definition of file associated with a machine, with an application on a machine or with no association (a global file)
	/// </summary>
	[ProtoBuf.ProtoContract]
	[DataContract]
	public class FileDef : IEquatable<FileDef>
	{
		/// <summary>
		/// Unique file id within the system
		/// </summary>
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public Guid Guid;

		/// <summary>
		/// Human readable file id
		/// </summary>
		[ProtoBuf.ProtoMember( 2 )]
		[DataMember]
		public string Id = String.Empty;

		/// <summary>
		/// Machine the file belongs to. Null if global file.
		/// </summary>
		[ProtoBuf.ProtoMember( 3 )]
		[DataMember]
		public string? MachineId = String.Empty;

		/// <summary>
		/// App the file belongs to. Used only if the file is bouond to an app.
		/// </summary>
		[ProtoBuf.ProtoMember( 4 )]
		[DataMember]
		public string? AppId = null;

		/// <summary>
		/// Full file path. Null if Newest mode is used.
		/// May contain env var macros %ENVVAR% (evaluated locally on the machine where file persists).
		/// </summary>
		[ProtoBuf.ProtoMember(5)]
		[DataMember]
		public string? Path = null;

		/// <summary>
		/// Folder where to look for the file. Used by the 'Newest' option.
		/// </summary>
		[ProtoBuf.ProtoMember( 6 )]
		[DataMember]
		public EFLookupType LookupType = EFLookupType.Path;

		/// <summary>
		/// Folder where to look for the file. Used by the 'Newest' option.
		/// </summary>
		[ProtoBuf.ProtoMember( 7 )]
		[DataMember]
		public string? Folder = null;

		/// <summary>
		/// File name mask. Used by the 'Newest' option.
		/// </summary>
		[ProtoBuf.ProtoMember( 8 )]
		[DataMember]
		public string? Mask = null;


		// use this to check for duplicates
		public bool SameAs( FileDef? other )
		{
			if( other is null )
				return false;

			if(
				//this.Guid == other.Guid &&  // we want to compare the content independently on the Guid
				this.Id == other.Id &&
				this.MachineId == other.MachineId &&
				this.AppId == other.AppId &&
				this.Path == other.Path &&
				this.LookupType == other.LookupType &&
				this.Folder == other.Folder &&
				this.Mask == other.Mask &&
				true
			)
				return true;
			else
				return false;
		}

		public bool Equals( FileDef? other )
		{
			if( other is null )
				return false;

			if(
				this.Guid == other.Guid &&	// same guid means same content
				this.SameAs( other ) &&
				//this.MachineId == other.MachineId &&
				//this.AppId == other.AppId &&
				//this.Path == other.Path &&
				//this.LookupType == other.LookupType &&
				//this.Folder == other.Folder &&
				//this.Mask == other.Mask &&
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

			var typed = obj as FileDef;
			if( typed is null )
				return false;
			else
				return Equals( typed );
		}

		public override int GetHashCode()
		{
			return this.Id.GetHashCode();
		}

		public static bool operator ==( FileDef t1, FileDef t2 )
		{
			if( ( object )t1 == null || ( ( object )t2 ) == null )
				return Object.Equals( t1, t2 );

			return t1.Equals( t2 );
		}

		public static bool operator !=( FileDef t1, FileDef t2 )
		{
			if( t1 is null || t2 is null )
				return !Object.Equals( t1, t2 );

			return !( t1.Equals( t2 ) );
		}


		public override string ToString()
		{
			var path = Path;
			if( LookupType == EFLookupType.Newest )
			{
				path = $"newest {Mask} in {Folder}";
			}

			if( !string.IsNullOrEmpty(AppId) && !string.IsNullOrEmpty(AppId) )
				return $"{Id}@{MachineId}.{AppId}:{path}";
			if( !string.IsNullOrEmpty(MachineId) )
				return $"{Id}@{MachineId}:{path}";
			return $"{Id}@{path}";
		}
	}

}
