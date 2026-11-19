using System.Collections.Generic;
using System.Linq;

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

		/// <summary>
		/// Registers a node, or - if a node of the same definition is already there - hands back that one.
		/// The caller must use what is returned.
		/// </summary>
		/// <remarks>
		/// An application is read once as a default and once for every plan that lists it, and each
		/// read produces its nodes afresh, with Guids made up on the spot. Registering all of them
		/// showed the same log folder N times in the node list and to every reference to it. Two nodes
		/// are the same when everything that was written in the config is the same - type, ids, title,
		/// path, the attributes as a whole, the actions, and the same again for the children - and the
		/// Guids are left out of it because they are the one thing that is never written. A plan whose
		/// copy of the app says something else (another MaxFiles) is another node, and stays one.
		/// A Guid written into the config is part of the attributes, so two different explicit Guids
		/// are never merged.
		/// </remarks>
		public VfsNodeDef Add( VfsNodeDef vfsNodeDef )
		{
			var known = _vfsNodes.Find( n => SameDefinition( n, vfsNodeDef ) );
			if( known is not null ) return known;

			_vfsNodes.Add( vfsNodeDef );
			return vfsNodeDef;
		}

		static bool SameDefinition( VfsNodeDef a, VfsNodeDef b )
		{
			if( ReferenceEquals( a, b ) ) return true;

			return a.GetType() == b.GetType()
				&& a.Id == b.Id
				&& a.MachineId == b.MachineId
				&& a.AppId == b.AppId
				&& a.Title == b.Title
				&& a.Path == b.Path
				&& a.Xml == b.Xml
				&& a.Actions.Count == b.Actions.Count
				&& a.Actions.Zip( b.Actions ).All( p => SameAction( p.First, p.Second ) )
				&& a.Children.Count == b.Children.Count
				&& a.Children.Zip( b.Children ).All( p => SameDefinition( p.First, p.Second ) );
		}

		static bool SameAction( ActionDef a, ActionDef b )
			=> a.GetType() == b.GetType()
			&& a.Name == b.Name
			&& a.Args == b.Args
			&& a.Title == b.Title
			&& a.HostId == b.HostId
			&& a.StartupDir == b.StartupDir;


	}
}
