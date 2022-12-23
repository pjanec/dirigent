using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent
{
	/// <summary>
	/// Arguments passed to the script called as a result of user clicking the script-based action menu item.
	/// </summary>
	[MessagePack.MessagePackObject]
	public class ScriptActionArgs
	{
		/// <summary>
		/// Generic string arguments as defined by the ScriptActionDef.Args.
		/// </summary>
		[MessagePack.Key( 1 )]
		public string? Args;
		
		/// <summary>
		/// Variables associated with the item (file, app, etc.)
		/// </summary>
		[MessagePack.Key( 2 )]
		public Dictionary<string, string>? Vars;

		/// <summary>
		/// The vfs node this script action is boound to (null of none)
		/// </summary>
		[MessagePack.Key( 3 )]
		public VfsNodeDef? VfsNode;
	}


}
