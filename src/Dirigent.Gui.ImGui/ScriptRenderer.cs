using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using System.Text.Json;

namespace Dirigent.Gui
{
	public class ScriptRenderer
	{
		private string _id;
		IDirig _ctrl;
		private string _uniqueUiId = Guid.NewGuid().ToString();
		private ImGuiWindow _wnd;
		
		private ImageInfo _txStart;
		private ImageInfo _txKill;

		public ScriptRenderer( ImGuiWindow wnd, string id, IDirig ctrl )
		{
			_wnd = wnd;
			_id = id;
			_ctrl = ctrl;

			_txStart = _wnd.GetImage("Resources/play.png");
			_txKill = _wnd.GetImage("Resources/delete.png");
		}

		public void DrawUI()
		{
			ImGui.PushID(_uniqueUiId);

			ScriptState? scriptState = _ctrl.GetScriptState( _id );
			ScriptDef? scriptDef = _ctrl.GetScriptDef( _id );

			string statusText = scriptState != null ? Tools.GetScriptStateText( scriptState ) : string.Empty;

			ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1f,1f,0f,1f) );
			bool opened = ImGui.TreeNodeEx( $"{_id}##{_id}", ImGuiTreeNodeFlags.FramePadding);
			ImGui.PopStyleColor();
			if (ImGui.BeginPopupContextItem())
			{
				if (ImGui.MenuItem("Start"))
				{
					_ctrl.Send( new Net.StartScriptMessage( _ctrl.Name, _id, null ) );
				}

				if (ImGui.MenuItem("Kill"))
				{
					_ctrl.Send( new Net.KillScriptMessage( _ctrl.Name, _id ) );
				}

				ImGui.EndPopup();
			}

			ImGui.SameLine();
			ImGui.SetCursorPosX( ImGui.GetWindowWidth()*3/4.5f);
			if( ImGuiTools.ImgBtn( _txStart ) )	_ctrl.Send( new Net.StartScriptMessage( _ctrl.Name, _id, null ) );
			ImGui.SameLine();
			if( ImGuiTools.ImgBtn( _txKill ) )	_ctrl.Send( new Net.KillScriptMessage( _ctrl.Name, _id ) );

			ImGui.SameLine();
			ImGui.SetCursorPosX( ImGui.GetWindowWidth()/4.5f*2f);
			ImGui.TextColored( GetScriptStateColor(statusText), statusText );

			//ImGui.SameLine();
			//ImGui.SetCursorPosX( ImGui.GetWindowWidth()/4f);
			//DrawOptions();

			if( opened )
			{
				//DrawUIBody();

				string jsonString = JsonSerializer.Serialize( scriptDef, new JsonSerializerOptions() { WriteIndented=true, IncludeFields=true } );
				ImGui.TextWrapped(jsonString.Replace("%", "%%")); // TextWrapped doesn't like %s etc, percent signs needs to be doubled

				ImGui.TreePop();
			}

			ImGui.PopID();
		}

		//public void DrawUI

		System.Numerics.Vector4 GetScriptStateColor( string txt )
		{
			var col = new System.Numerics.Vector4(192, 192, 192, 255)/255f;

			if( txt.StartsWith( "Success" ) )
			{
				col = new System.Numerics.Vector4(39, 135, 65, 255)/255f;

				col = new System.Numerics.Vector4(39, 135, 65, 255)/255f;
			}
			else if( txt.StartsWith( "Failure" ) )
			{
				col = new System.Numerics.Vector4(212, 0, 4, 255)/255f;
			}
			else if( txt.StartsWith( "InProgress" ) || txt.StartsWith( "Killing" ) )
			{
				col = new System.Numerics.Vector4(8, 0, 252, 255)/255f;
			}

			return col;
		}


	}
}
