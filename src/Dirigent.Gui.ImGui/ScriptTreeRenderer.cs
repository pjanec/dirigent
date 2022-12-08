using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using System.Text.Json;

namespace Dirigent.Gui
{
	// renders scripts sorted by their Groups attribute
	public class ScriptTreeRenderer
	{
		IDirig _ctrl;
		private string _uniqueUiId = Guid.NewGuid().ToString();
		private ImGuiWindow _wnd;
		
		FolderTreeRenderer _treeRend;
		FolderTree _treeRoot;
		Dictionary<Guid, ScriptRenderer> _nodeRenderers;
					
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		public ScriptTreeRenderer( ImGuiWindow wnd, IDirig ctrl )
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		{
			_wnd = wnd;
			_ctrl = ctrl;
			_treeRend = new FolderTreeRenderer( _wnd, ctrl, RenderNode );
			Reset();
		}

		public void Reset()
		{
			_nodeRenderers = new();
			_treeRoot = BuildTree();
		}

		public void DrawUI()
		{
			_treeRend.DrawUI( _treeRoot );
		}

		Action? RenderNode( FolderTree node )
		{
			var r = node.Payload as ScriptRenderer;
			if ( r != null ) // leaf, i.e. actual script
			{
				r.DrawUI();
				return null; // do NOT render subnodes, none can exist; if having paylod, it must be a leaf because we always put the payload just to the leaves
			}
			else // just intermediate folder
			{
				ImGui.PushStyleColor( ImGuiCol.Text, new System.Numerics.Vector4( 0f, 1f, 1f, 1f ) );
				bool opened = ImGui.TreeNodeEx( $"{node.Name}", ImGuiTreeNodeFlags.FramePadding );
				ImGui.PopStyleColor();
				if ( opened ) return () => { ImGui.TreePop(); };
				return null; // do NOT render subnodes (not unfolded)
			}
		}


		void SortTree( FolderTree tree )
		{
			if( tree.Children == null ) return;

			tree.Children.Sort( (a,b) => a.Name.CompareTo( b.Name ) );

			foreach( var subtree in tree.Children )
			{
				SortTree( subtree );
			}
		}


		FolderTree BuildTree()
		{
			var root = new FolderTree();
			foreach( var def in _ctrl.GetAllScriptDefs() )
			{
				var id = def.Guid;
				var title = def.Title;

				// get renderer for a single script
				ScriptRenderer? r;
				if( !_nodeRenderers.TryGetValue( id, out r ) )
				{
					r = new ScriptRenderer( _wnd, id, title, _ctrl );	// will render the effective ones
					_nodeRenderers[id] = r;
				}

				// parse "Groups" attribute into individual group paths
				// use them to build a tree of script groups; the tree node payload = script renderer (null if just an intermediate node)
				var groups = def.Groups.Split( ';' );
				if ( groups.Length > 0 )
				{
					foreach ( var g in groups )
					{
						root.InsertNode( $"{g.Trim()}/{title}", false, r, null );
					}
				}
				else // no groups defined => put to the root
				{
					root.InsertNode( title, false, r, null );
				}
			}

			SortTree( root );

			return root;
		}
	}
}
