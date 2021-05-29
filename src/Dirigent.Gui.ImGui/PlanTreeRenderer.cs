using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using System.Text.Json;

namespace Dirigent.Gui
{
	// renders plans sorted by their Groups attribute
	public class PlanTreeRenderer
	{
		IDirig _ctrl;
		private ImGuiWindow _wnd;
		
		FolderTreeRenderer _treeRend;
		FolderTree _treeRoot;
		Dictionary<string, PlanRenderer> _nodeRenderers;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
		public PlanTreeRenderer( ImGuiWindow wnd, IDirig ctrl )
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
			var r = node.Payload as PlanRenderer;
			if ( r != null ) // leaf, i.e. actual script
			{
				r.DrawUI();
				return null; // do NOT render subnodes, none can exist; if having paylod, it must be a leaf because we always put the payload just to the leaves
			}
			else // just intermediate folder
			{
				ImGui.PushStyleColor( ImGuiCol.Text, new System.Numerics.Vector4( 0f, 1f, 1f, 1f ) );
				bool opened = ImGui.TreeNodeEx( $"{node.Name}##folder_{node.Name}", ImGuiTreeNodeFlags.FramePadding );
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

		PlanRenderer GetOrCreateRenderer( string uniqueUiId, string id )
		{
			PlanRenderer? r;
			if (!_nodeRenderers.TryGetValue(uniqueUiId, out r))
			{
				r = new PlanRenderer(_wnd, uniqueUiId, id, _ctrl);  // will render the effective ones
				_nodeRenderers[uniqueUiId] = r;
			}
			return r;
		}


		FolderTree BuildTree()
		{
			var root = new FolderTree();
			foreach( var def in _ctrl.GetAllPlanDefs() )
			{
				var id = def.Name;

				// parse "Groups" attribute into individual group paths
				// use them to build a tree of script groups; the tree node payload = script renderer (null if just an intermediate node)
				var groups = def.Groups.Split( ';' );
				if ( groups.Length > 0 )
				{
					foreach ( var g in groups )
					{
						var path = $"{g.Trim()}/{id}";
						var r = GetOrCreateRenderer( path, id );
						root.InsertNode( path, false, r, null );
					}
				}
				else // no groups defined => put to the root
				{
					var path = id;
					var r = GetOrCreateRenderer(path, id);
					root.InsertNode( path, false, r, null );
				}
			}

			SortTree( root );

			return root;
		}
	}
}
