using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using System.Text.Json;

namespace Dirigent.Gui
{
	public class ClientRenderer
	{
		private string _id;	
		IDirig _ctrl;
		private string _uniqueUiId = Guid.NewGuid().ToString();
		private ImGuiWindow _wnd;
		
		
		public ClientRenderer( ImGuiWindow wnd, string id, IDirig ctrl )
		{
			_wnd = wnd;
			_id = id;
			_ctrl = ctrl;
		}

		public void DrawUI()
		{
			ImGui.PushID(_uniqueUiId);
			
			ClientState? clientState = _ctrl.GetClientState( _id );

			string statusText = clientState != null ? Tools.GetClientStateText( clientState ) : string.Empty;

			ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0f,1f,1f,1f) );
			bool opened = ImGui.TreeNodeEx( $"{_id}##{_id}", ImGuiTreeNodeFlags.FramePadding);
			ImGui.PopStyleColor();
			//if (ImGui.BeginPopupContextItem())
			//{
			//	if (ImGui.MenuItem("Start"))
			//	{
			//		_ctrl.Send( new Net.StartAppMessage( _ctrl.Name, _id, planName ) );
			//	}

			//	if (ImGui.MenuItem("Kill"))
			//	{
			//		_ctrl.Send( new Net.KillAppMessage( _ctrl.Name, _id ) );
			//	}

			//	if (ImGui.MenuItem("Restart"))
			//	{
			//		_ctrl.Send( new Net.RestartAppMessage( _ctrl.Name, _id ) );
			//	}
			//	ImGui.EndPopup();
			//}
			//ImGui.SameLine();
			//ImGui.SetCursorPosX( ImGui.GetWindowWidth()*3/4f);
			////if( ImGui.Button("S") )	_ctrl.Send( new Net.StartAppMessage( _id, planName ) );

			ImGui.SameLine();
			ImGui.SetCursorPosX( ImGui.GetWindowWidth()/4*2f);
			ImGui.TextColored( GetClientStateColor(statusText), statusText );

			//ImGui.SameLine();
			//ImGui.SetCursorPosX( ImGui.GetWindowWidth()/4f);
			//DrawOptions();

			if( opened )
			{
				////DrawUIBody();
				//string jsonString = JsonSerializer.Serialize( appDef, new JsonSerializerOptions() { WriteIndented=true, IncludeFields=true } );
				//ImGui.TextWrapped(jsonString.Replace("%", "%%")); // TextWrapped doesn't like %s etc, percent signs needs to be doubled

				ImGui.TreePop();
			}

			ImGui.PopID();
		}

		System.Numerics.Vector4 GetClientStateColor( string txt )
		{
			var col = new System.Numerics.Vector4(192, 192, 192, 255)/255f;

			if( txt.StartsWith( "Connected" ) )
			{
				col = new System.Numerics.Vector4(39, 135, 65, 255)/255f;
			}
			else if( txt.StartsWith( "Offline" ) )
			{
				col = new System.Numerics.Vector4(212, 0, 4, 255)/255f;
			}

			return col;
		}

	}
}
