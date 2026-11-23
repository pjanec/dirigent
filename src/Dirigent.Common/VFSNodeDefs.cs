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
	[MessagePack.MessagePackObject]
	[MessagePack.Union( 101, typeof( FileDef ) )]
	[MessagePack.Union( 102, typeof( FileRef ) )]
	[MessagePack.Union( 103, typeof( FolderDef ) )]
	[MessagePack.Union( 104, typeof( VFolderDef ) )]
	[MessagePack.Union( 105, typeof( FilePackageDef ) )]
	[MessagePack.Union( 106, typeof( ResolvedVfsNodeDef ) )]
	public abstract class VfsNodeDef : AssocMenuItemDef, IEquatable<VfsNodeDef>
	{
		/// <summary>
		/// Full file path in the real file system, local to the machine where the file/folder resides.
		/// Paths is already resolved, not containing any macros.
		/// Empty for virtual folders having no counterpart in the real file system.
		/// </summary>
		[MessagePack.Key( 10 )]
		public string? Path = null;

		/// <summary>
		/// Is the node a container for another vfs nodes? False = leaf
		/// </summary>
		[MessagePack.Key( 11 )]
		public bool IsContainer;

		/// <summary>
		/// Sub-items. Used for folders only.
		/// </summary>
		[MessagePack.Key( 12 )]
		public List<VfsNodeDef> Children = new List<VfsNodeDef>();

		/// <summary>
		/// Name of the filter script to resolve this item 
		/// </summary>
		[MessagePack.Key( 13 )]
		public string? Filter;

		/// <summary>
		/// Xml node with attributes passed to filter scripts
		/// </summary>
		[MessagePack.Key( 14 )]
		public string? Xml;

		/// <summary>
		/// Collect only the last this many bytes of a file bigger than that. 0 = whole files.
		/// Set on a &lt;Folder&gt;, it applies to every file the folder yields.
		/// </summary>
		/// <remarks>
		/// For the log files a never-rotating logger grows to tens of gigabytes: the end of such a
		/// file is what an investigation needs, and the whole of it is not transferable at all.
		/// </remarks>
		[MessagePack.Key( 15 )]
		public long TailBytes = 0;

		/// <summary>
		/// Whether the file may be emptied, or have a line drawn under it, before a test run.
		/// Off unless the configuration says otherwise.
		/// </summary>
		/// <remarks>
		/// The whole permission, of which marking is the gentler half: a file that may be cleared may
		/// also be marked, and a file that may not is always collected whole. Marking is gated too
		/// because a marked configuration file would arrive in the archive empty - no bytes appended
		/// since the mark - which is quieter, and therefore worse, than deleting it.
		///
		/// Off by default, so that a configuration file is safe from any action, argument or package
		/// that names it. Set on a &lt;Folder&gt;, it applies to every file the folder yields.
		/// </remarks>
		[MessagePack.Key( 16 )]
		public bool Clearable = false;

		/// <summary>
		/// What the resolution of this node had to leave out - a file too big for the size budget,
		/// for instance. Filled in during resolution, empty in a definition.
		/// </summary>
		/// <remarks>
		/// The point is that a limit must not silently swallow a part of what the user asked for.
		/// A download writes these into the archive, so that whoever opens it later can tell an
		/// incomplete collection from a complete one.
		/// </remarks>
		[MessagePack.Key( 17 )]
		public List<string>? Notes;

		/// <summary>
		/// The machine each of the <see cref="Notes"/> is about, at the same index; null for a note tied to no
		/// machine. A download writes a note into the part of the archive of that machine only - the tree goes to
		/// every machine, and each used to write every note.
		/// </summary>
		[MessagePack.Key( 18 )]
		public List<string?>? NoteMachines;

		/// <summary>Adds a note about the given machine (null = no machine in particular).</summary>
		public void AddNote( string note, string? machineId )
		{
			// a list that predates NoteMachines (or was filled by hand) gets its gaps filled with "no machine"
			Notes ??= new List<string>();
			NoteMachines ??= new List<string?>();
			while( NoteMachines.Count < Notes.Count ) NoteMachines.Add( null );

			Notes.Add( note );
			NoteMachines.Add( machineId );
		}

		/// <summary>The machine the note at the index is about, or null.</summary>
		public string? NoteMachine( int index )
			=> NoteMachines is not null && index < NoteMachines.Count ? NoteMachines[index] : null;

		/// <summary>
		/// Size in bytes of the file this resolved node stands for, as found when it was looked up
		/// (on the machine that owns it). Null in a definition, for a folder, and where the file
		/// could not be read.
		/// </summary>
		[MessagePack.Key( 19 )]
		public long? Size;

		/// <summary>Last write time (UTC) of the file, found together with <see cref="Size"/>.</summary>
		[MessagePack.Key( 20 )]
		public DateTime? Modified;

		/// <summary>When a line was drawn under the file (UTC), if one was - see the file marks.</summary>
		[MessagePack.Key( 21 )]
		public DateTime? MarkedAt;

		/// <summary>Where the line is: the length of the file when it was marked.</summary>
		[MessagePack.Key( 22 )]
		public long? MarkedAtBytes;

		/// <summary>
		/// On a resolved file: the id of the file definition it came from - the innermost one that has an id, so the
		/// definition a package or an application referred to, not the package. Filled in during resolution.
		/// </summary>
		[MessagePack.Key( 23 )]
		public string? OriginId;

		/// <summary>
		/// The software component this file definition stands for, as its &lt;... Component="..."&gt; says; empty if it says none
		/// (a file of an application then belongs to the application's component).
		/// </summary>
		[MessagePack.Key( 24 )]
		public string Component = string.Empty;

		/// <summary>
		/// A copy of this resolved tree holding only the files of the given machine - and, if <paramref name="includeGlobals"/>,
		/// the files of no machine. The containers all stay, with their notes; the files are shared, not copied.
		/// </summary>
		/// <remarks>
		/// For the scripts a marking or a download starts on each machine: each acts on its own files only, and used to be
		/// sent the whole site's tree - 146 KB per machine on a 7-machine site, at every checkpoint of a running plan.
		/// The test for "its own" is the one those scripts apply.
		/// </remarks>
		public VfsNodeDef ForMachine( string machineId, bool includeGlobals )
		{
			var copy = (VfsNodeDef) MemberwiseClone();
			copy.Children = new List<VfsNodeDef>();

			foreach( var child in Children )
			{
				if( child.IsContainer )
					copy.Children.Add( child.ForMachine( machineId, includeGlobals ) );
				else if( child.MachineId == machineId || ( string.IsNullOrEmpty( child.MachineId ) && includeGlobals ) )
					copy.Children.Add( child );
			}

			return copy;
		}

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
			this.TailBytes == other.TailBytes &&
			this.Clearable == other.Clearable &&
			( ( this.Notes is null && other.Notes is null )
				|| ( this.Notes is not null && other.Notes is not null && this.Notes.SequenceEqual( other.Notes ) ) ) &&
			true;

		// boilerplate
		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(VfsNodeDef? o) => object.Equals(this, o);
		public static bool operator ==(VfsNodeDef o1, VfsNodeDef o2) => object.Equals(o1, o2);
		public static bool operator !=(VfsNodeDef o1, VfsNodeDef o2) => !object.Equals(o1, o2);
		public override int GetHashCode() => Guid.GetHashCode();
	}


	[MessagePack.MessagePackObject]
	public class ResolvedVfsNodeDef : VfsNodeDef, IEquatable<ResolvedVfsNodeDef>
	{
		public bool ThisEquals( ResolvedVfsNodeDef other ) =>
			base.ThisEquals( other ) &&
			true;

		// boilerplate
		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(ResolvedVfsNodeDef? o) => object.Equals(this, o);
		public static bool operator ==(ResolvedVfsNodeDef o1, ResolvedVfsNodeDef o2) => object.Equals(o1, o2);
		public static bool operator !=(ResolvedVfsNodeDef o1, ResolvedVfsNodeDef o2) => !object.Equals(o1, o2);
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
	[MessagePack.MessagePackObject]
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
	[MessagePack.MessagePackObject]
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
	[MessagePack.MessagePackObject]
	public class FolderDef : VfsNodeDef, IEquatable<FolderDef>
	{
		/// <summary>
		/// File name mask in Glob style (allowing stuff like "**/*.{jpg,png}".
		/// </summary>
		[MessagePack.Key( 25 )]
		public string? Mask = String.Empty;

		/// <summary>
		/// Maximum number of files to include. 0 = unlimited.
		/// The newest files are preferred if the limit applies.
		/// </summary>
		[MessagePack.Key( 26 )]
		public int MaxFiles = 0;

		/// <summary>
		/// Maximum age of the files to include, in seconds, based on the last write time. 0 = whatever age.
		/// </summary>
		[MessagePack.Key( 27 )]
		public double MaxSeconds = 0;

		/// <summary>
		/// Maximum total size of the included files, in bytes. 0 = unlimited.
		/// The newest files are preferred if the limit applies. At least one file is always included.
		/// </summary>
		[MessagePack.Key( 28 )]
		public long MaxTotalBytes = 0;

		public override string ToString() =>$"[Folder] {base.ToString()}";

		public bool ThisEquals(FolderDef o) =>
			base.ThisEquals(o) &&
			this.Mask == o.Mask &&
			this.MaxFiles == o.MaxFiles &&
			this.MaxSeconds == o.MaxSeconds &&
			this.MaxTotalBytes == o.MaxTotalBytes &&
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
	[MessagePack.MessagePackObject]
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
	[MessagePack.MessagePackObject]
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
