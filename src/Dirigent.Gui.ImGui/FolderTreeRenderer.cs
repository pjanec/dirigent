using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using System.Text.Json;

namespace Dirigent.Gui
{
	public class FolderTreeRenderer
	{
		IDirig _ctrl;
		private string _uniqueUiId = Guid.NewGuid().ToString();
		private ImGuiWindow _wnd;
		NodeDrawDelegate _nodeDrawDeleg;

		// Returns non-null if the node is unfolded; then the caller needs to call the action returned
		// to finish rendering the node.
		public delegate Action? NodeDrawDelegate( TreeNode node );
		
		public FolderTreeRenderer( ImGuiWindow wnd, IDirig ctrl, NodeDrawDelegate? nodeDrawDeleg )
		{
			_wnd = wnd;
			_ctrl = ctrl;
			_nodeDrawDeleg = nodeDrawDeleg ?? DefaultNodeDraw;
		}

		public void DrawUI( TreeNode root )
		{
			ImGui.PushID(_uniqueUiId);
			
			RenderSubnodes( root );

			ImGui.PopID();
		}

		Action? DefaultNodeDraw( TreeNode node )
		{
			ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0f,1f,1f,1f) );
			bool opened = ImGui.TreeNodeEx( $"{node.Name}", ImGuiTreeNodeFlags.FramePadding);
			ImGui.PopStyleColor();
			if( opened ) return () => { ImGui.TreePop(); };
			return null;
		}

		void RenderNode( TreeNode node )
		{
			var closingAction = _nodeDrawDeleg( node );
			if( closingAction != null )	// node is unfolded
			{
				RenderSubnodes( node );
				closingAction();
			}
		}

		void RenderSubnodes( TreeNode node )
		{
			if( node.Children == null ) return;

			// first render the folders
			foreach( var subn in node.Children )
			{
				if( !subn.IsFolder ) continue;
				RenderNode( subn );
			}


			// then render non-folders
			foreach( var subn in node.Children )
			{
				if( subn.IsFolder ) continue;
				RenderNode( subn );
			}
		}
	}
}
