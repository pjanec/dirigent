using System;

using Dirigent;

namespace Dirigent.Scripts.BuiltIn
{
	/// <summary>
	/// Names a VFS node the way a <c>&lt;FileRef&gt;</c> does - by the id it was given in the shared
	/// config, optionally narrowed to one machine and one application.
	/// </summary>
	/// <remarks>
	/// The GUI resolves a node before starting a script and passes the resolved tree. Nobody else
	/// can: resolution is a remote operation. A selector is what a CLI caller, a REST caller or
	/// another script uses instead, leaving the resolution to the script itself.
	/// </remarks>
	public class VfsNodeSelector
	{
		/// <summary>Id of the node, as declared in the shared config. Wildcards allowed.</summary>
		public string? Id;

		/// <summary>Machine to take the node from; empty or "*" means any.</summary>
		public string? MachineId;

		/// <summary>Application to take the node from; empty or "*" means any.</summary>
		public string? AppId;

		/// <summary>
		/// The reference to hand to the resolver. Only top-level nodes are findable - see
		/// "Where nodes can be declared" in docs/Files.md.
		/// </summary>
		public FileRef ToFileRef()
		{
			if( string.IsNullOrEmpty( Id ) )
				throw new ArgumentException( "A VFS node selector needs an Id." );

			return new FileRef()
			{
				// the field default is Guid.Empty, and two nodes sharing it look like a circular
				// reference to the resolver, which then silently resolves to nothing
				Guid = Guid.NewGuid(),
				Id = Id,
				MachineId = string.IsNullOrEmpty( MachineId ) ? "*" : MachineId,
				AppId = string.IsNullOrEmpty( AppId ) ? "*" : AppId,
			};
		}

		public override string ToString()
			=> $"{Id} (machine {( string.IsNullOrEmpty( MachineId ) ? "*" : MachineId )}, app {( string.IsNullOrEmpty( AppId ) ? "*" : AppId )})";
	}
}
