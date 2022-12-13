using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent
{
	/// <summary>
	/// A node of an arbitrary tree; can be built from multi-segment path  (path segments separated by slash character)
	/// Each node can carry a payload object. Intermediate nodes created from path segments carry no payload (null).
	/// Intermediate nodes can be created via given constructor delegate (otherwise FolderTree objects are instantiated).
	/// Can be used for sorting apps, scripts and plans into groups.
	/// </summary>
	public class FolderTree
	{
		public string Name = string.Empty;
		public List<FolderTree>? Children;
		public bool IsFolder; // folders are drawn before the rest

		public object? Payload;

		static char[] seps = new char[] { '/', '\\' };

		public delegate FolderTree ConstructorDeleg();
		
		/// <summary>
		/// Parses the path to segments, builds intermediate tree levels and stored the payload to the leaf one
		/// </summary>
		/// <param name="path"></param>
		/// <param name="payload"></param>
		/// <param name="constructor">if not null, called to create new tree nodes </param>
		public void InsertNode( string path, bool isFolder, object? payload, ConstructorDeleg? constructor )
		{
			var parts = path.Split( seps, 2 );
			var name = parts[0];
			
			// find/create the next child node having same name as the first path segment
			FolderTree? child = null;
			if(!string.IsNullOrEmpty(name))
			{
				// create subtree if not existing yet
				if( Children == null )
				{
					Children = new List<FolderTree>();
				}
				else // try to find existing
				{
					child = Children.Find( x => string.Equals( x.Name, name ) );
				}
				// create new child not if not existing yet
				if( child == null )
				{
					child = constructor == null ? new FolderTree() : constructor();
					child.Name = name;
					child.IsFolder = true; // automatically created are folders
					Children.Add(child);
				}
			}
			else
			{
				child = this;
			}

			// process recursively the rest of the path
			if( parts.Length > 1 )
			{
				child.InsertNode( parts[1], isFolder, payload, constructor );
			}
			else // it's the leaf of the tree, set the payload
			{
				child.Payload = payload;
				child.IsFolder = isFolder;
			}
		}

		public static string GetNamePart( string path )
		{
			var parts = path.Split( seps, 2 );
			return parts[0];
		}
	}
}
