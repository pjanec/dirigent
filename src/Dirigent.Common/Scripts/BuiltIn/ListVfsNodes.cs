using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Dirigent;

namespace Dirigent.Scripts.BuiltIn
{
	/// <summary>
	/// Lists the VFS nodes the shared config declares, optionally filtered. The answer to
	/// "what is there to download" for anyone without a GUI.
	/// </summary>
	/// <remarks>
	/// Only the *declarations* - nothing is looked up in any file system here. Use
	/// BuiltIns/ResolveVfsPath.cs on a node to find out what files it actually stands for.
	/// Only top-level nodes are listed, because only those are the ones a reference can find.
	/// </remarks>
	public class ListVfsNodes : Script
	{
		public static readonly string _Name = "BuiltIns/ListVfsNodes.cs";

		public class TArgs
		{
			/// <summary>Which nodes to list; all of them when not given.</summary>
			public VfsNodeSelector? Filter;

			public override string ToString() => $"{Filter}";
		};

		/// <summary>
		/// A node as declared, flattened to what a caller needs to identify and then resolve it.
		/// </summary>
		public class TNode
		{
			public string Id = "";
			public string Guid = "";

			/// <summary>File, Folder, VFolder, FilePackage or FileRef.</summary>
			public string Type = "";

			public string? MachineId;
			public string? AppId;
			public string? Title;

			/// <summary>The path as declared, variables and masks not yet expanded.</summary>
			public string? Path;

			public override string ToString() => $"{Type} {Id} ({MachineId}.{AppId})";
		}

		public class TResult
		{
			public List<TNode> Nodes = new();
		}

		protected override async Task<string?> Run()
		{
			var args = Tools.Deserialize<TArgs>( Args ) ?? new TArgs();

			var all = await Dirig.GetAllVfsNodesDefAsync();

			var nodes = from node in all
						where Matches( node, args.Filter )
						orderby node.Id, node.MachineId, node.AppId
						select Describe( node );

			return Tools.Serialize( new TResult() { Nodes = nodes.ToList() } );
		}

		static bool Matches( VfsNodeDef node, VfsNodeSelector? filter )
		{
			if( filter is null ) return true;

			return IsMatch( filter.Id, node.Id )
				&& IsMatch( filter.MachineId, node.MachineId )
				&& IsMatch( filter.AppId, node.AppId );
		}

		/// <summary>
		/// Same rules the resolver uses when following a reference: an empty pattern matches
		/// anything, and "*" matches a missing value too.
		/// </summary>
		static bool IsMatch( string? pattern, string? value )
		{
			if( string.IsNullOrEmpty( pattern ) ) return true;
			if( value is null ) return pattern == "*";
			return System.IO.Enumeration.FileSystemName.MatchesSimpleExpression( pattern, value );
		}

		static TNode Describe( VfsNodeDef node ) => new TNode()
		{
			Id = node.Id,
			Guid = node.Guid.ToString(),
			Type = TypeName( node ),
			MachineId = node.MachineId,
			AppId = node.AppId,
			Title = node.Title,
			Path = node.Path,
		};

		static string TypeName( VfsNodeDef node ) => node switch
		{
			FileRef => "FileRef",
			FileDef => "File",
			FolderDef => "Folder",
			VFolderDef => "VFolder",
			FilePackageDef => "FilePackage",
			_ => node.GetType().Name,
		};
	}
}
