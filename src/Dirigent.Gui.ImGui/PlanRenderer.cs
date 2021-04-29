using Dirigent.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;

namespace Dirigent.Gui
{
	public class PlanRenderer
	{
		private string _id;
		IDirig _ctrl;
		private string _uniqueUiId = Guid.NewGuid().ToString();
		Dictionary<AppIdTuple, AppRenderer> _appRenderers = new();
		
		public PlanRenderer( string id, IDirig ctrl )
		{
			_id = id;
			_ctrl = ctrl;
		}

		public void DrawUI()
		{
			ImGui.PushID(_uniqueUiId);

			PlanState? planState = _ctrl.GetPlanState( _id );
			PlanDef? planDef = _ctrl.GetPlanDef( _id );

			string statusText = planState != null ? Tools.GetPlanStateText( planState ) : string.Empty;

			ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1f,1f,0f,1f) );
			bool opened = ImGui.TreeNodeEx( $"{_id}##{_id}", ImGuiTreeNodeFlags.FramePadding);
			ImGui.PopStyleColor();
			if (ImGui.BeginPopupContextItem())
			{
				if (ImGui.MenuItem("Start"))
				{
					_ctrl.Send( new Net.StartPlanMessage( _id ) );
				}

				if (ImGui.MenuItem("Kill"))
				{
					_ctrl.Send( new Net.KillPlanMessage( _id ) );
				}

				if (ImGui.MenuItem("Restart"))
				{
					_ctrl.Send( new Net.RestartPlanMessage( _id ) );
				}

				ImGui.EndPopup();
			}

			ImGui.SameLine();
			ImGui.SetCursorPosX( ImGui.GetWindowWidth()/4.5f);
			if( ImGui.Button("S") )	_ctrl.Send( new Net.StartPlanMessage( _id ) );
			ImGui.SameLine();
			if( ImGui.Button("K") )	_ctrl.Send( new Net.KillPlanMessage( _id ) );
			ImGui.SameLine();
			if( ImGui.Button("R") )	_ctrl.Send( new Net.RestartPlanMessage( _id ) );

			ImGui.SameLine();
			ImGui.SetCursorPosX( ImGui.GetWindowWidth()/4.5f*2f);
			ImGui.TextColored( GetPlanStateColor(statusText), statusText );

			//ImGui.SameLine();
			//ImGui.SetCursorPosX( ImGui.GetWindowWidth()/4f);
			//DrawOptions();

			if( opened )
			{
				//DrawUIBody();

				if( planDef != null )
				{
					foreach( var ad in planDef.AppDefs )
					{
						AppRenderer? r;
						if( !_appRenderers.TryGetValue( ad.Id, out r ) )
						{
							r = new AppRenderer( ad.Id, _ctrl, ad ); // will render appdefs from the plan (not the current one)
							_appRenderers[ad.Id] = r;
						}
						else r.AppDef = ad;	// app def may change so better to update it every time
						r.DrawUI();
					}
				}

				ImGui.TreePop();
			}

			ImGui.PopID();
		}

		//public void DrawUI

		System.Numerics.Vector4 GetPlanStateColor( string txt )
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
