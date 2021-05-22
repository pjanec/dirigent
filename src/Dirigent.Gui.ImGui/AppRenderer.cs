using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;
using System.Text.Json;

namespace Dirigent.Gui
{
	public class AppRenderer
	{
		private AppIdTuple _id;	
		IDirig _ctrl;
		private string _uniqueUiId = Guid.NewGuid().ToString();
		private AppDef? _appDef = null; // if null, app def will be taken from IDirig interface (which is the actual one, not necessarily the same one from the plan)
		public AppDef? AppDef { get { return _appDef; } set { _appDef = value; } }
		private ImGuiWindow _wnd;
		
		private ImageInfo _txStart;
		private ImageInfo _txKill;
		private ImageInfo _txRestart;
		//private System.Numerics.Vector2 _btnSize;
		
		public AppRenderer( ImGuiWindow wnd, AppIdTuple id, IDirig ctrl, AppDef? appDef=null )
		{
			_wnd = wnd;
			_id = id;
			_ctrl = ctrl;
			_appDef = appDef;

			//_btnSize = ImGui.CalcTextSize("XX")*1.4f;
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

		static System.Numerics.Vector4 _redColor = new System.Numerics.Vector4(1f,0,0,1f);
		public void DrawUI()
		{
			ImGui.PushID(_uniqueUiId);
			
			AppDef? appDef = _appDef ?? _ctrl.GetAppDef( _id );

			AppState? appState = _ctrl.GetAppState( _id );

			PlanState? planState = null;
			if( appState != null && !string.IsNullOrEmpty(appState.PlanName))
			{
				planState = _ctrl.GetPlanState( appState.PlanName );
			}

			string statusText = appState != null ? Tools.GetAppStateText( appState, planState, appDef ) : string.Empty;

			string? planName = _appDef?.PlanName ?? appState?.PlanName;	// prefer plan from appdef


			ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0f,1f,1f,1f) );
			bool opened = ImGui.TreeNodeEx( $"{_id}##{_id}", ImGuiTreeNodeFlags.FramePadding);
			ImGui.PopStyleColor();
			if (ImGui.BeginPopupContextItem())
			{
				if (ImGui.MenuItem("Start"))
				{
					_ctrl.Send( new Net.StartAppMessage( _ctrl.Name, _id, planName ) );
				}

				if (ImGui.MenuItem("Kill"))
				{
					_ctrl.Send( new Net.KillAppMessage( _ctrl.Name, _id ) );
				}

				if (ImGui.MenuItem("Restart"))
				{
					_ctrl.Send( new Net.RestartAppMessage( _ctrl.Name, _id ) );
				}
				ImGui.EndPopup();
			}
			ImGui.SameLine();
			ImGui.SetCursorPosX( ImGui.GetWindowWidth()*3/4f);
			//if( ImGui.Button("S") )	_ctrl.Send( new Net.StartAppMessage( _id, planName ) );
			if( ImgBtn( _txStart ) ) _ctrl.Send( new Net.StartAppMessage( _ctrl.Name, _id, planName ) );
			ImGui.SameLine();
			if( ImgBtn( _txKill ) )	_ctrl.Send( new Net.KillAppMessage( _ctrl.Name, _id ) );
			ImGui.SameLine();
			if( ImgBtn( _txRestart ) ) _ctrl.Send( new Net.RestartAppMessage( _ctrl.Name, _id ) );

			// enabled checkbox just for apps from a plan
			if( _appDef is not null && _appDef.PlanName is not null)
			{
				ImGui.SameLine();
				bool enabled = !_appDef.Disabled;
				if( ImGui.Checkbox("##enabled", ref enabled) )
				{
					_ctrl.Send( new Net.SetAppEnabledMessage( _ctrl.Name, _appDef.PlanName, _id, enabled ) );
				}
			}

			ImGui.SameLine();
			ImGui.SetCursorPosX( ImGui.GetWindowWidth()/4*2f);
			ImGui.TextColored( GetAppStateColor(statusText, appDef), statusText );

			//ImGui.SameLine();
			//ImGui.SetCursorPosX( ImGui.GetWindowWidth()/4f);
			//DrawOptions();

			if( opened )
			{
				//DrawUIBody();
				string jsonString = JsonSerializer.Serialize( appDef, new JsonSerializerOptions() { WriteIndented=true, IncludeFields=true } );
				ImGui.TextWrapped(jsonString.Replace("%", "%%")); // TextWrapped doesn't like %s etc, percent signs needs to be doubled

				ImGui.TreePop();
			}

			ImGui.PopID();
		}

		System.Numerics.Vector4 GetAppStateColor( string txt, AppDef? appDef )
		{
			var col = new System.Numerics.Vector4(192, 192, 192, 255)/255f;

			if( txt.StartsWith( "Running" ) )
			{
				col = new System.Numerics.Vector4(39, 135, 65, 255)/255f;
			}
			else if( txt.StartsWith( "Planned" ) )
			{
				col = new System.Numerics.Vector4(100, 39, 135, 255)/255f;
			}
			else if( txt.StartsWith( "Initializing" ) )
			{
				col = new System.Numerics.Vector4(184, 111, 17, 255)/255f;
			}
			else if( txt.StartsWith( "Terminated" ) )
			{
				if( appDef is not null )
				{
					if( !appDef.Volatile ) // just non-volatile apps are not supposed to terminate on their own...
					{
						col = new System.Numerics.Vector4(212, 0, 4, 255)/255f;
					}
				}
			}
			else if( txt.StartsWith( "Restarting" ) || txt.StartsWith( "Dying" ) )
			{
				col = new System.Numerics.Vector4(8, 0, 252, 255)/255f;
			}

			return col;
		}

	}
}
