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
	public class VfsNodeDef : IEquatable<VfsNodeDef>
	{
		/// <summary>
		/// Unique id within the system (generated when loading the item from config)
		/// </summary>
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public Guid Guid;

		/// <summary>
		/// Display name of the item
		/// </summary>
		[ProtoBuf.ProtoMember( 2 )]
		[DataMember]
		public string Title = String.Empty;

		/// <summary>
		/// Human readable file id
		/// </summary>
		[ProtoBuf.ProtoMember( 3 )]
		[DataMember]
		public string Id = String.Empty;

		/// <summary>
		/// Machine the file belongs to. Null if global file.
		/// </summary>
		[ProtoBuf.ProtoMember( 4 )]
		[DataMember]
		public string? MachineId = String.Empty;

		/// <summary>
		/// App the file belongs to. Used only if the file is bouond to an app.
		/// </summary>
		[ProtoBuf.ProtoMember( 5 )]
		[DataMember]
		public string? AppId = null;

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
		/// What tools are predefined for this item.
		/// </summary>
		[ProtoBuf.ProtoMember( 9 )]
		[DataMember]
		public List<ToolRef> Tools = new List<ToolRef>();

		public override string ToString()
		{
			return $"{Id}@{MachineId}.{AppId}:{Path}";
		}

		public bool ThisEquals( VfsNodeDef other ) =>
			this.Guid == other.Guid &&
			this.Id == other.Id &&
			this.Title == other.Title &&
			this.MachineId == other.MachineId &&
			this.AppId == other.AppId &&
			this.Path == other.Path &&
			this.Children.SequenceEqual( other.Children ) &&
			this.Tools.SequenceEqual( other.Tools ) &&
			this.IsContainer == other.IsContainer &&
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
