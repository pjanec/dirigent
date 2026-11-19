using System;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent.Web
{
	/// <summary>A top-level VFS node as declared; nothing is looked up in any file system.</summary>
	public class NodeInfo
	{
		public string id { get; set; } = string.Empty;
		public string type { get; set; } = string.Empty;
		public string title { get; set; } = string.Empty;
		public string? description { get; set; }
		public string groups { get; set; } = string.Empty;
		public string? machine { get; set; }
		public string? app { get; set; }
	}

	/// <summary>
	/// The listing behind GET /api/nodes and the GetNodes command: what is there to download.
	/// </summary>
	public static class NodeListing
	{
		/// <summary>
		/// Master thread only. The id, machine and app filters take the wildcards of
		/// <see cref="Scripts.BuiltIn.ListVfsNodes.Matches"/>; the type is compared case-insensitively.
		/// </summary>
		public static List<NodeInfo> List( Master master, string? type, string? id, string? machine, string? app )
		{
			var selector = new Scripts.BuiltIn.VfsNodeSelector() { Id = id, MachineId = machine, AppId = app };

			return ( from node in master.GetAllVfsNodesDef()
					 where Scripts.BuiltIn.ListVfsNodes.Matches( node, selector )
						&& ( string.IsNullOrEmpty( type )
							|| string.Equals( AppFilesQuery.TypeName( node ), type, StringComparison.OrdinalIgnoreCase ) )
					 orderby node.Id, node.MachineId, node.AppId
					 select new NodeInfo()
					 {
						 id = node.Id,
						 type = AppFilesQuery.TypeName( node ),
						 title = node.Title,
						 description = node.Description,
						 groups = node.Groups,
						 machine = string.IsNullOrEmpty( node.MachineId ) ? null : node.MachineId,
						 app = string.IsNullOrEmpty( node.AppId ) ? null : node.AppId,
					 } ).ToList();
		}

		public static string ToJson( List<NodeInfo> nodes ) => AppFilesResult.ToJson( nodes );
	}
}
