using Dirigent.Common;
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
		
		public AppRenderer( AppIdTuple id, IDirig ctrl, AppDef? appDef=null )
		{
			_id = id;
			_ctrl = ctrl;
			_appDef = appDef;
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

			string statusText = appState != null ? Tools.GetAppStateText( appState, planState ) : string.Empty;
			string? planName = appState?.PlanName;


			ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0f,1f,1f,1f) );
			bool opened = ImGui.TreeNodeEx( $"{_id}##{_id}", ImGuiTreeNodeFlags.FramePadding);
			ImGui.PopStyleColor();
			if (ImGui.BeginPopupContextItem())
			{
				if (ImGui.MenuItem("Start"))
				{
					_ctrl.Send( new Net.StartAppMessage( _id, planName ) );
				}

				if (ImGui.MenuItem("Kill"))
				{
					_ctrl.Send( new Net.KillAppMessage( _id ) );
				}

				if (ImGui.MenuItem("Restart"))
				{
					_ctrl.Send( new Net.RestartAppMessage( _id ) );
				}
				ImGui.EndPopup();
			}
			ImGui.SameLine();
			ImGui.SetCursorPosX( ImGui.GetWindowWidth()/4f);
			if( ImGui.Button("S") )	_ctrl.Send( new Net.StartAppMessage( _id, planName ) );
			ImGui.SameLine();
			if( ImGui.Button("K") )	_ctrl.Send( new Net.KillAppMessage( _id ) );
			ImGui.SameLine();
			if( ImGui.Button("R") )	_ctrl.Send( new Net.RestartAppMessage( _id ) );

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
				ImGui.TextWrapped(jsonString);

				ImGui.TreePop();
			}

			ImGui.PopID();
		}

		System.Numerics.Vector4 GetAppStateColor( string txt, AppDef? appDef )
		{
			var col = new System.Numerics.Vector4(192, 192, 192, 255);

			if( txt.StartsWith( "Running" ) )
			{
				col = new System.Numerics.Vector4(39, 135, 65, 255);
			}
			else if( txt.StartsWith( "Planned" ) )
			{
				col = new System.Numerics.Vector4(100, 39, 135, 255);
			}
			else if( txt.StartsWith( "Initializing" ) )
			{
				col = new System.Numerics.Vector4(184, 111, 17, 255);
			}
			else if( txt.StartsWith( "Terminated" ) )
			{
				if( appDef is not null )
				{
					if( !appDef.Volatile ) // just non-volatile apps are not supposed to terminate on their own...
					{
						col = new System.Numerics.Vector4(212, 0, 4, 255);
					}
				}
			}
			else if( txt.StartsWith( "Restarting" ) || txt.StartsWith( "Dying" ) )
			{
				col = new System.Numerics.Vector4(8, 0, 252, 255);
			}

			return col;
		}

	}
}
