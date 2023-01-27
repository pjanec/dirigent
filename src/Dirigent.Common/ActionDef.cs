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
	//[MessagePack.MessagePackObject]
	[MessagePack.Union( 201, typeof( ToolAppActionDef ) )]
	[MessagePack.Union( 202, typeof( ScriptActionDef ) )]
	[MessagePack.Union( 203, typeof( ScriptDef ) )]
	public abstract class ActionDef : AssocMenuItemDef, IEquatable<ActionDef>
	{
		/// Name in the library of scripts, tools etc.
		/// </summary>
		//[MessagePack.Key( 21 )]
		public string Name = string.Empty;

		/// <summary>
		/// Args to pass to the action (tool, script etc.)
		/// </summary>
		//[MessagePack.Key( 22 )]
		public string Args = string.Empty;

		/// <summary>
		/// On which node (client/agent/master) to run this script/action. empty=master. Null = not set.
		/// </summary>
		//[MessagePack.Key( 23 )]
		public string? HostId;

		// overrides startup folder for a tool action
		public string? StartupDir;


		public override string ToString()
		{
			return $"\"{Title}\" {Name} {Args}";
		}

		public bool ThisEquals( ActionDef other ) =>
				base.ThisEquals( other ) &&
				this.Name == other.Name &&
				this.Args == other.Args &&
				this.HostId == other.HostId &&
				this.StartupDir == other.StartupDir &&
				true;

		// boilerplate
		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(ActionDef? o) => object.Equals(this, o);
		public static bool operator ==(ActionDef o1, ActionDef o2) => object.Equals(o1, o2);
		public static bool operator !=(ActionDef o1, ActionDef o2) => !object.Equals(o1, o2);
		public override int GetHashCode() => Id.GetHashCode();
	}

	//[MessagePack.MessagePackObject]
	public class ToolAppActionDef : ActionDef, IEquatable<ToolAppActionDef>
	{
		public override string ToString() =>$"[ToolAppAction] {base.ToString()}";

		public bool ThisEquals(ToolAppActionDef o) => base.ThisEquals(o) && true;
		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(ToolAppActionDef? o) => object.Equals(this, o);
		public override int GetHashCode() => base.GetHashCode();
	}

	//[MessagePack.MessagePackObject]
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
