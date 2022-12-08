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
	/// A node in the virtual file system tree (like the FilePackage.)
	/// It could be
	///   - a link to a real file
	///   - a virtual folder than can contain other file links or other vfolders.
	/// </summary>
	[ProtoBuf.ProtoContract]
	[ProtoBuf.ProtoInclude( 101, typeof( FileDef ) )]
	[ProtoBuf.ProtoInclude( 102, typeof( FileRef ) )]
	[ProtoBuf.ProtoInclude( 103, typeof( FolderDef ) )]
	[ProtoBuf.ProtoInclude( 104, typeof( VFolderDef ) )]
	[ProtoBuf.ProtoInclude( 105, typeof( FilePackageDef ) )]
	public class VfsNodeDef : AssocItemDef, IEquatable<VfsNodeDef>
	{
		/// <summary>
		/// Full file path in the real file system, local to the machine where the file/folder resides.
		/// Paths is already resolved, not containing any macros.
		/// Empty for virtual folders having no counterpart in the real file system.
		/// </summary>
		[ProtoBuf.ProtoMember( 6 )]
		[DataMember]
		public string? Path = null;

		/// <summary>
		/// Is the node a container for another vfs nodes? False = leaf
		/// </summary>
		[ProtoBuf.ProtoMember( 7 )]
		[DataMember]
		public bool IsContainer;

		/// <summary>
		/// Sub-items. Used for folders only.
		/// </summary>
		[ProtoBuf.ProtoMember( 8 )]
		[DataMember]
		public List<VfsNodeDef> Children = new List<VfsNodeDef>();

		/// <summary>
		/// Name of the filter script to resolve this item 
		/// </summary>
		[ProtoBuf.ProtoMember( 10 )]
		[DataMember]
		public string? Filter;

		/// <summary>
		/// Xml node with attributes passed to filter scripts 
		/// </summary>
		[ProtoBuf.ProtoMember( 11 )]
		[DataMember]
		public string? Xml;


		public override string ToString()
		{
			return $"{Id}@{MachineId}.{AppId}:{Path}";
		}

		public bool ThisEquals( VfsNodeDef other ) =>
			base.ThisEquals( other ) &&
			this.Path == other.Path &&
			this.IsContainer == other.IsContainer &&
			this.Children.SequenceEqual( other.Children ) &&
			this.Filter == other.Filter &&
			this.Xml == other.Xml &&
			true;

		// boilerplate
		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(VfsNodeDef? o) => object.Equals(this, o);
		public static bool operator ==(VfsNodeDef o1, VfsNodeDef o2) => object.Equals(o1, o2);
		public static bool operator !=(VfsNodeDef o1, VfsNodeDef o2) => !object.Equals(o1, o2);
		public override int GetHashCode() => Guid.GetHashCode();
	}


	public enum EFLookupType
	{
		Path,
		Newest,
	}

	/// <summary>
	/// Definition of a non-virtual file
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class FileDef : VfsNodeDef, IEquatable<FileDef>
	{
		///// <summary>
		///// Folder where to look for the file. Used by the 'Newest' option.
		///// </summary>
		//[ProtoBuf.ProtoMember( 1 )]
		//public EFLookupType LookupType = EFLookupType.Path;

		public override string ToString() =>$"[File] {base.ToString()}";

		public bool ThisEquals(FileDef o) => base.ThisEquals(o) && true;
		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(FileDef? o) => object.Equals(this, o);
		public override int GetHashCode() => base.GetHashCode();
	}

	/// <summary>
	/// Reference to a file. Path not used, just the Id, MachineId, AppId.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class FileRef : VfsNodeDef, IEquatable<FileRef>
	{
		public override string ToString() =>$"[FileRef] {base.ToString()}";

		public bool ThisEquals( FileRef o ) => base.ThisEquals( o ) && true;
		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(FileRef? o) => object.Equals(this, o);
		public override int GetHashCode() => base.GetHashCode();
	}


	/// <summary>
	/// Definition of folder or virtual associated with a machine, with an application on a machine or with no association (a global file)
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class FolderDef : VfsNodeDef, IEquatable<FolderDef>
	{
		/// <summary>
		/// File name mask in Glob style (allowing stuff like "**/*.{jpg,png}".
		/// </summary>
		[ProtoBuf.ProtoMember( 1 )]
		public string? Mask = String.Empty;

		public override string ToString() =>$"[Folder] {base.ToString()}";

		public bool ThisEquals(FolderDef o) =>
			base.ThisEquals(o) &&
			this.Mask == o.Mask &&
			true;
			
		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(FolderDef? o) => object.Equals(this, o);
		public override int GetHashCode() => base.GetHashCode();
	}

	/// <summary>
	/// Definition of a virtual folder.
	/// Path field is ignored.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class VFolderDef : VfsNodeDef, IEquatable<VFolderDef>
	{
		public override string ToString() =>$"[VFolder] {base.ToString()}";

		public bool ThisEquals(VFolderDef o) => base.ThisEquals(o);
		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(VFolderDef? o) => object.Equals(this, o);
		public override int GetHashCode() => base.GetHashCode();
	}

	/// <summary>
	/// Definition of file package associated with a machine, with an application on a machine or with no association (a global file package)
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class FilePackageDef : VfsNodeDef, IEquatable<FilePackageDef>
	{
		public override string ToString() =>$"[FilePackage] {base.ToString()}";

		public bool ThisEquals(FilePackageDef o) => base.ThisEquals(o);
		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(FilePackageDef? o) => object.Equals(this, o);
		public override int GetHashCode() => base.GetHashCode();
	}

	/// <summary>
	/// Reference to existing package (via Id, AppId, MachineId)
	/// Path field is ignored.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class FilePackageRef : VfsNodeDef, IEquatable<FilePackageRef>
	{
		public override string ToString() =>$"[FilePackageRef] {base.ToString()}";

		public bool ThisEquals(FilePackageRef o) => base.ThisEquals(o);
		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(FilePackageRef? o) => object.Equals(this, o);
		public override int GetHashCode() => base.GetHashCode();
	}

}
