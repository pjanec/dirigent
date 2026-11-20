using System;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent.Web
{
	/// <summary>A top-level VFS node as declared; nothing is looked up in any file system.</summary>
	public class FileDefInfo
	{
		public string id { get; set; } = string.Empty;
		public string type { get; set; } = string.Empty;
		public string title { get; set; } = string.Empty;
		public string? description { get; set; }
		public string groups { get; set; } = string.Empty;

		/// <summary>The Component the definition names; null if none.</summary>
		public string? component { get; set; }
		public string? machine { get; set; }
		public string? app { get; set; }
	}

	/// <summary>
	/// The listing behind GET /api/filedefs and the GetFileDefs command: what is there to download.
	/// </summary>
	public static class FileDefListing
	{
		/// <summary>The values of the type filter - <see cref="Scripts.BuiltIn.ListVfsNodes.TypeName"/> of each kind.</summary>
		public static readonly string[] Types = { "File", "Folder", "VFolder", "FilePackage", "FileRef" };

		/// <summary>
		/// Master thread only. The id, machine and app filters take the wildcards of
		/// <see cref="Scripts.BuiltIn.ListVfsNodes.Matches"/>; the type is compared case-insensitively.
		/// </summary>
		public static List<FileDefInfo> List( Master master, string? type, string? id, string? machine, string? app, string? groupName = null )
		{
			// a misspelt type would answer "no such definitions", which reads as a fact about the config
			if( !string.IsNullOrEmpty( type ) && !Types.Contains( type, StringComparer.OrdinalIgnoreCase ) )
				throw new ArgumentException( $"Unknown type '{type}'; one of {string.Join( ", ", Types )}." );

			var selector = new Scripts.BuiltIn.VfsNodeSelector() { Id = id, MachineId = machine, AppId = app };

			return ( from node in master.GetAllVfsNodesDef()
					 where Scripts.BuiltIn.ListVfsNodes.Matches( node, selector )
						&& ( string.IsNullOrEmpty( type )
							|| string.Equals( AppFilesQuery.TypeName( node ), type, StringComparison.OrdinalIgnoreCase ) )
						&& InGroup( node, groupName )
					 orderby node.Id, node.MachineId, node.AppId
					 select new FileDefInfo()
					 {
						 id = node.Id,
						 type = AppFilesQuery.TypeName( node ),
						 title = node.Title,
						 description = node.Description,
						 groups = node.Groups,
						 component = string.IsNullOrEmpty( node.Component ) ? null : node.Component,
						 machine = string.IsNullOrEmpty( node.MachineId ) ? null : node.MachineId,
						 app = string.IsNullOrEmpty( node.AppId ) ? null : node.AppId,
					 } ).ToList();
		}

		/// <summary>
		/// Whether one entry of the ';' separated Groups of the node is the group asked for: case-insensitive, with the
		/// wildcards of the ids. No group asked for means every node.
		/// </summary>
		public static bool InGroup( VfsNodeDef node, string? groupName )
		{
			if( string.IsNullOrEmpty( groupName ) ) return true;

			foreach( var g in ( node.Groups ?? string.Empty ).Split( ';', StringSplitOptions.RemoveEmptyEntries ) )
			{
				if( Scripts.BuiltIn.ListVfsNodes.IsMatch( groupName.Trim(), g.Trim() ) )
					return true;
			}
			return false;
		}

		public static string ToJson( List<FileDefInfo> nodes ) => AppFilesResult.ToJson( nodes );
	}
}
