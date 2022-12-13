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
	[ProtoBuf.ProtoContract]
	[ProtoBuf.ProtoInclude( 101, typeof( ActionDef ) )]
	[ProtoBuf.ProtoInclude( 102, typeof( VfsNodeDef ) )]
	public class AssocMenuItemDef : IEquatable<AssocMenuItemDef>
	{
		/// <summary>
		/// Unique id within the system (generated when loading the item from config)
		/// </summary>
		[ProtoBuf.ProtoMember( 1 )]
		[DataMember]
		public Guid Guid;

		/// <summary>
		/// Display name of the item.
		/// Optional backslash-separated submenu levels, like "Utils\File\My Title".
		/// </summary>
		[ProtoBuf.ProtoMember( 2 )]
		[DataMember]
		public string Title = String.Empty;

		/// <summary>
		/// Human readable item id
		/// </summary>
		[ProtoBuf.ProtoMember( 3 )]
		[DataMember]
		public string Id = String.Empty;

		/// <summary>
		/// Machine the item belongs to. Null if global.
		/// </summary>
		[ProtoBuf.ProtoMember( 4 )]
		[DataMember]
		public string? MachineId = String.Empty;

		/// <summary>
		/// App the item belongs to. Used only if the action is bouond to an app.
		/// </summary>
		[ProtoBuf.ProtoMember( 5 )]
		[DataMember]
		public string? AppId = null;

		// semicolon separated list of "paths" like "main/examples;"GUI might use this for showing scripts in a folder tree
		[ProtoBuf.ProtoMember( 6 )]
		public string Groups = string.Empty;

		/// <summary>
		/// Icon for the menu item 
		/// </summary>
		[ProtoBuf.ProtoMember( 7 )]
		[DataMember]
		public string? IconFile;

		/// <summary>
		/// Submenu items
		/// </summary>
		[ProtoBuf.ProtoMember( 8 )]
		[DataMember]
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
