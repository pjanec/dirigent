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
		private ImGuiWindow _wnd;
		
		private ImageInfo _txStart;
		private ImageInfo _txKill;
		private ImageInfo _txRestart;

		public PlanRenderer( ImGuiWindow wnd, string id, IDirig ctrl )
		{
			_wnd = wnd;
			_id = id;
			_ctrl = ctrl;

			_txStart = _wnd.GetImage("Resources/play.png");
			_txKill = _wnd.GetImage("Resources/delete.png");
			_txRestart = _wnd.GetImage("Resources/refresh.png");
		}

		// uses original texture size and black background, 
		private bool ImgBtn( ImageInfo img )
		{
			return ImGui.ImageButton(
				img.TextureUserId,
				new System.Numerics.Vector2( img.Texture.Width, img.Texture.Height ), // original texture size
				System.Numerics.Vector2.Zero,
				new System.Numerics.Vector2(1,1),
				0, // no padding
				new System.Numerics.Vector4(0,0,0,1) // black background
			); 
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
					_ctrl.Send( new Net.StartPlanMessage( _ctrl.Name, _id ) );
				}

				if (ImGui.MenuItem("Kill"))
				{
					_ctrl.Send( new Net.KillPlanMessage( _ctrl.Name, _id ) );
				}

				if (ImGui.MenuItem("Restart"))
				{
					_ctrl.Send( new Net.RestartPlanMessage( _ctrl.Name, _id ) );
				}

				ImGui.EndPopup();
			}

			ImGui.SameLine();
			ImGui.SetCursorPosX( ImGui.GetWindowWidth()*3/4.5f);
			if( ImgBtn( _txStart ) )	_ctrl.Send( new Net.StartPlanMessage( _ctrl.Name, _id ) );
			ImGui.SameLine();
			if( ImgBtn( _txKill ) )	_ctrl.Send( new Net.KillPlanMessage( _ctrl.Name, _id ) );
			ImGui.SameLine();
			if( ImgBtn( _txRestart ) )	_ctrl.Send( new Net.RestartPlanMessage( _ctrl.Name, _id ) );

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
							r = new AppRenderer( _wnd, ad.Id, _ctrl, ad ); // will render appdefs from the plan (not the current one)
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
