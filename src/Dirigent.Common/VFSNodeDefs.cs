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
	//[MessagePack.MessagePackObject]
	[MessagePack.Union( 101, typeof( FileDef ) )]
	[MessagePack.Union( 102, typeof( FileRef ) )]
	[MessagePack.Union( 103, typeof( FolderDef ) )]
	[MessagePack.Union( 104, typeof( VFolderDef ) )]
	[MessagePack.Union( 105, typeof( FilePackageDef ) )]
	[MessagePack.Union( 106, typeof( ExpandedVfsNodeDef ) )]
	public abstract class VfsNodeDef : AssocMenuItemDef, IEquatable<VfsNodeDef>
	{
		/// <summary>
		/// Full file path in the real file system, local to the machine where the file/folder resides.
		/// Paths is already resolved, not containing any macros.
		/// Empty for virtual folders having no counterpart in the real file system.
		/// </summary>
		//[MessagePack.Key( 26 )]
		public string? Path = null;

		/// <summary>
		/// Is the node a container for another vfs nodes? False = leaf
		/// </summary>
		//[MessagePack.Key( 27 )]
		public bool IsContainer;

		/// <summary>
		/// Sub-items. Used for folders only.
		/// </summary>
		//[MessagePack.Key( 28 )]
		public List<VfsNodeDef> Children = new List<VfsNodeDef>();

		/// <summary>
		/// Name of the filter script to resolve this item 
		/// </summary>
		//[MessagePack.Key( 30 )]
		public string? Filter;

		/// <summary>
		/// Xml node with attributes passed to filter scripts 
		/// </summary>
		//[MessagePack.Key( 31 )]
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


	//[MessagePack.MessagePackObject]
	public class ExpandedVfsNodeDef : VfsNodeDef, IEquatable<ExpandedVfsNodeDef>
	{
		public long Length;
		public DateTime LastWriteTimeUtc;
		
		public bool ThisEquals( ExpandedVfsNodeDef other ) =>
			base.ThisEquals( other ) &&
			this.Length == other.Length &&
			this.LastWriteTimeUtc == other.LastWriteTimeUtc &&
			true;

		// boilerplate
		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(ExpandedVfsNodeDef? o) => object.Equals(this, o);
		public static bool operator ==(ExpandedVfsNodeDef o1, ExpandedVfsNodeDef o2) => object.Equals(o1, o2);
		public static bool operator !=(ExpandedVfsNodeDef o1, ExpandedVfsNodeDef o2) => !object.Equals(o1, o2);
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
	//[MessagePack.MessagePackObject]
	public class FileDef : VfsNodeDef, IEquatable<FileDef>
	{
		///// <summary>
		///// Folder where to look for the file. Used by the 'Newest' option.
		///// </summary>
		////[MessagePack.Key( 51 )]
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
	//[MessagePack.MessagePackObject]
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
	//[MessagePack.MessagePackObject]
	public class FolderDef : VfsNodeDef, IEquatable<FolderDef>
	{
		/// <summary>
		/// File name mask in Glob style (allowing stuff like "**/*.{jpg,png}".
		/// </summary>
		//[MessagePack.Key( 51 )]
		public string? Mask = String.Empty;

		public override string ToString() =>$"[Folder] {base.ToString()}";

		public bool ThisEquals(FolderDef o) =>
			base.ThisEquals(o) &&
			this.Mask == o.Mask &&
			true;

		public FolderDef() : base() { IsContainer=true; }

		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(FolderDef? o) => object.Equals(this, o);
		public override int GetHashCode() => base.GetHashCode();
	}

	/// <summary>
	/// Definition of a virtual folder.
	/// Title is used as a name of the folder.
	/// Path field is ignored.
	/// </summary>
	//[MessagePack.MessagePackObject]
	public class VFolderDef : VfsNodeDef, IEquatable<VFolderDef>
	{
		public override string ToString() => $"[VFolder] {$"{Title}@{MachineId}.{AppId}"}";

		public VFolderDef() : base() { IsContainer=true; }

		public bool ThisEquals(VFolderDef o) => base.ThisEquals(o);
		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(VFolderDef? o) => object.Equals(this, o);
		public override int GetHashCode() => base.GetHashCode();
	}

	/// <summary>
	/// Definition of file package associated with a machine, with an application on a machine or with no association (a global file package)
	/// </summary>
	//[MessagePack.MessagePackObject]
	public class FilePackageDef : VfsNodeDef, IEquatable<FilePackageDef>
	{
		public override string ToString() =>$"[FilePackage] {$"{Title}@{MachineId}.{AppId}"}";

		public FilePackageDef() : base() { IsContainer=true; }

		public bool ThisEquals(FilePackageDef o) => base.ThisEquals(o);
		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(FilePackageDef? o) => object.Equals(this, o);
		public override int GetHashCode() => base.GetHashCode();
	}

}
