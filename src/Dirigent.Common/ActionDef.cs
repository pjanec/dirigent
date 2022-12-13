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
	/// Definition of an action associated with some dirigent item (app, file, machine..)
	/// </summary>
	[ProtoBuf.ProtoContract]
	[ProtoBuf.ProtoInclude( 101, typeof( ToolActionDef ) )]
	[ProtoBuf.ProtoInclude( 102, typeof( ScriptActionDef ) )]
	public class ActionDef : AssocMenuItemDef, IEquatable<ActionDef>
	{
		/// Name in the library of scripts, tools etc.
		/// </summary>
		[ProtoBuf.ProtoMember( 1 )]
		public string Name = string.Empty;

		/// <summary>
		/// Args to pass to the action (tool, script etc.)
		/// </summary>
		[ProtoBuf.ProtoMember( 2 )]
		public string Args = string.Empty;

		/// <summary>
		/// On which node (client/agent/master) to run this script. empty=master. Null = not set.
		/// </summary>
		[ProtoBuf.ProtoMember( 3 )]
		public string? HostId;

		public override string ToString()
		{
			return $"\"{Title}\" {Name} {Args}";
		}

		public bool ThisEquals( ActionDef other ) =>
				base.ThisEquals( other ) &&
				this.Name == other.Name &&
				this.Args == other.Args &&
				this.HostId == other.HostId &&
				true;

		// boilerplate
		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(ActionDef? o) => object.Equals(this, o);
		public static bool operator ==(ActionDef o1, ActionDef o2) => object.Equals(o1, o2);
		public static bool operator !=(ActionDef o1, ActionDef o2) => !object.Equals(o1, o2);
		public override int GetHashCode() => Id.GetHashCode();
	}

	[ProtoBuf.ProtoContract]
	public class ToolActionDef : ActionDef, IEquatable<ToolActionDef>
	{
		public override string ToString() =>$"[ToolAction] {base.ToString()}";

		public bool ThisEquals(ToolActionDef o) => base.ThisEquals(o) && true;
		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(ToolActionDef? o) => object.Equals(this, o);
		public override int GetHashCode() => base.GetHashCode();
	}

	[ProtoBuf.ProtoContract]
	[ProtoBuf.ProtoInclude( 101, typeof( ScriptDef ) )]
	public class ScriptActionDef : ActionDef, IEquatable<ScriptActionDef>
	{
		public override string ToString() =>$"[ScriptAction] {base.ToString()}";

		public bool ThisEquals( ScriptActionDef other ) =>
				base.ThisEquals( other ) &&
				true;

		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(ScriptActionDef? o) => object.Equals(this, o);
		public override int GetHashCode() => base.GetHashCode();
	}
}
