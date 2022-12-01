using System.Collections.Generic;

namespace Dirigent
{
	/// <summary>
	/// Gathers the VFS node defs (files, packages...) defs during dirigent's config file parsing
	/// </summary>
	public class FileDefReg
	{
		List<VfsNodeDef> _vfsNodes = new List<VfsNodeDef>();

		/// <summary>
		/// All VfsNodeDefs found across the SharedConfig file
		/// </summary>
		public List<VfsNodeDef> VfsNodes => _vfsNodes;

		public VfsNodeDef Add( VfsNodeDef vfsNodeDef )
		{
			_vfsNodes.Add( vfsNodeDef );
			return vfsNodeDef;
		}


	}
}
