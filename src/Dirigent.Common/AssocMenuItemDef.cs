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
	/// An item like a file, file package or script to be shown as a menu on UI,
	/// related to some dirigent-controlled item (app, plan, machine..),
	/// optionally having some actions associated with the item (startable in the context of this item.)
	/// </summary>
	//[MessagePack.MessagePackObject]
	[MessagePack.Union( 101, typeof( ActionDef ) )]
	[MessagePack.Union( 102, typeof( VfsNodeDef ) )]
	[MessagePack.Union( 103, typeof( ScriptActionDef ) )]
	[MessagePack.Union( 104, typeof( ToolActionDef ) )]
	[MessagePack.Union( 105, typeof( ScriptDef ) )]
	
	[MessagePack.Union( 111, typeof( FileDef ) )]
	[MessagePack.Union( 112, typeof( FileRef ) )]
	[MessagePack.Union( 113, typeof( FolderDef ) )]
	[MessagePack.Union( 114, typeof( VFolderDef ) )]
	[MessagePack.Union( 115, typeof( FilePackageDef ) )]
	[MessagePack.Union( 116, typeof( ResolvedVfsNodeDef ) )]

	public abstract class AssocMenuItemDef : IEquatable<AssocMenuItemDef>
	{
		/// <summary>
		/// Unique id within the system (generated when loading the item from config)
		/// </summary>
		//[MessagePack.Key( 1 )]
		public Guid Guid;

		/// <summary>
		/// Display name of the item.
		/// Optional backslash-separated submenu levels, like "Utils\File\My Title".
		/// </summary>
		//[MessagePack.Key( 2 )]
		public string Title = String.Empty;

		/// <summary>
		/// Human readable item id
		/// </summary>
		//[MessagePack.Key( 3 )]
		public string Id = String.Empty;

		/// <summary>
		/// Machine the item belongs to. Null if global.
		/// </summary>
		//[MessagePack.Key( 4 )]
		public string? MachineId = String.Empty;

		/// <summary>
		/// App the item belongs to. Used only if the action is bouond to an app.
		/// </summary>
		//[MessagePack.Key( 5 )]
		public string? AppId = null;

		// semicolon separated list of "paths" like "main/examples;"GUI might use this for showing scripts in a folder tree
		//[MessagePack.Key( 6 )]
		public string Groups = string.Empty;

		/// <summary>
		/// Icon for the menu item 
		/// </summary>
		//[MessagePack.Key( 7 )]
		public string? IconFile;

		/// <summary>
		/// Submenu items
		/// </summary>
		//[MessagePack.Key( 8 )]
		public List<ActionDef> Actions = new List<ActionDef>();

		public override string ToString()
		{
			return $"{Id}";
		}

		public bool ThisEquals( AssocMenuItemDef other ) =>
				this.Guid == other.Guid &&
				this.Title == other.Title &&
				this.Id == other.Id &&
				this.MachineId == other.MachineId &&
				this.AppId == other.AppId &&
				this.Groups == other.Groups &&
				this.IconFile == other.IconFile &&
				this.Actions.SequenceEqual( other.Actions ) &&
				true;

		// boilerplate
		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(AssocMenuItemDef? o) => object.Equals(this, o);
		public static bool operator ==(AssocMenuItemDef o1, AssocMenuItemDef o2) => object.Equals(o1, o2);
		public static bool operator !=(AssocMenuItemDef o1, AssocMenuItemDef o2) => !object.Equals(o1, o2);
		public override int GetHashCode() => Id.GetHashCode();
	}
}
