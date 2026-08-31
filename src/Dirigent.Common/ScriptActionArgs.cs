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
	//[MessagePack.MessagePackObject]
	public class ScriptActionArgs
	{
		/// <summary>
		/// Generic string arguments as defined by the ScriptActionDef.Args.
		/// </summary>
		//[MessagePack.Key( 1 )]
		public string? Args;
		
		/// <summary>
		/// Variables associated with the item (file, app, etc.)
		/// </summary>
		//[MessagePack.Key( 2 )]
		public Dictionary<string, string>? Vars;

		/// <summary>
		/// The vfs node this script action is boound to (null of none)
		/// </summary>
		//[MessagePack.Key( 3 )]
		public VfsNodeDef? VfsNode;

		/// <summary>
		/// True when <see cref="VfsNode"/> is still a DEFINITION that the script is expected to
		/// resolve itself; false when it is an already resolved tree handed over by the caller.
		/// </summary>
		/// <remarks>
		/// Resolving a package that spans many apps on several machines costs one remote round trip
		/// per node - measured at 8.4 s for 16 nodes - and while the GUI did that before starting
		/// the script there was no operation to show, so the status bar stayed empty and the whole
		/// thing looked stuck until the last second. Handing the definition over lets the top level
		/// script own the resolve too, count it in its own progress, and keep its internals to
		/// itself.
		///
		/// Tool actions cannot use this: their FILE_PATH is built by walking the resolved tree, so
		/// they still need it resolved up front.
		/// </remarks>
		//[MessagePack.Key( 4 )]
		public bool VfsNodeNeedsResolving;
	}


}
