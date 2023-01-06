using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent
{
	// Represent a menu item with optional sumbenus
	// Used to build menus.
	public class MenuTreeNode
	{
		public string Title = "";
		public List<MenuTreeNode> Children = new();
		public Action? Action;
		public string Icon = "";
		public MenuTreeNode() {}
		public MenuTreeNode( string title, string icon="", Action? action = null )
		{
			Title = title;
			Icon = icon;
			Action = action;
		}

		public override string ToString()
		{
			return $"Menu {Title}";
		}


		static char[] seps = new char[] { '/', '\\' };

		// Parses the given path like "path/to/leaf" into a list of segments ["path", "to", "leaf"],
		// removing any empty segments
		static List<string> ParsePathToSegments( string path )
		{
			var parts = path.Split( seps );
			if( parts.Length == 0 ) return new List<string>();
			var segments = new List<string>();
			foreach (var p in parts)
			{
				if (!string.IsNullOrEmpty( p ))
				{
					segments.Add( p );
				}
			}
			return segments;
		}

		// From given segments builds a tree, putting given last node at the end o chain.
		// Returns the node created from the first segment (the beginning of the chain).
		static MenuTreeNode BuildItermediateTree( List<string> segments, int count, MenuTreeNode last )
		{
			if (count == 0)
				return last;

			// first segment = root			
			var first = new MenuTreeNode( segments[0] );
			// each next segment is a child of the previous one
			var current = first;
			for( int i = 1; i < count; ++i)
			{
				var node = new MenuTreeNode( segments[i] );
				current.Children.Add( node );
				current = node;
			}

			current.Children.Add( last );

			return first;
		}

		// parses the node's Title, for each of its segments create an intermediate node, adds the node at the end
		// returns the root
		public static MenuTreeNode MakeTreeFromTitle( MenuTreeNode node )
		{
			// parse the title to individual segments
			var segments = ParsePathToSegments( node.Title );
			if (segments.Count == 0) throw new Exception("Empty title!");

			// keep just the leaf segment as the node title
			var leafSegment = segments[segments.Count-1];
			node.Title = leafSegment;

			// build intermediate nodes from the rest
			return BuildItermediateTree( segments, segments.Count-1, node );
		}

		// merges given node with our children
		public void MergeNode( MenuTreeNode node )
		{
			var existing = Children.Find( x => string.Equals( x.Title, node.Title, StringComparison.OrdinalIgnoreCase ) );
			if (existing == null)
			{
				Children.Add( node );
			}
			else
			{
				existing.MergeSubtree( node );
			}
		}

		// merges the children of given node with out children
		public void MergeSubtree( MenuTreeNode subtree )
		{
			foreach( var child in subtree.Children )
			{
				var existingChild = Children.Find( x => string.Equals( x.Title, child.Title, StringComparison.OrdinalIgnoreCase ) );
				if (existingChild == null)
				{
					Children.Add( child );
				}
				else
				{
					existingChild.MergeSubtree( child );
				}
			}
		}

		// From each item create a tree by parsing its Title to the path. Merge resulting trees together.
		public static MenuTreeNode CombineMenuItems( List<MenuTreeNode> items )
		{
			var root = new MenuTreeNode();
			foreach( var item in items )
			{
				var subtree = MakeTreeFromTitle( item );
				root.MergeNode( subtree );
			}
			return root;
		}


	}
}
