using Dirigent.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;

namespace Dirigent.Gui
{
	public class AppRenderer
	{
		private AppIdTuple _id;	
		ReflectedStateRepo _reflStates;
		private string _uniqueUiId = Guid.NewGuid().ToString();
		
		public AppRenderer( AppIdTuple id, ReflectedStateRepo reflStates )
		{
			_id = id;
			_reflStates = reflStates;
		}

		static System.Numerics.Vector4 _redColor = new System.Numerics.Vector4(255,0,0,255);
		public void DrawUI()
		{
			ImGui.PushID(_uniqueUiId);


			
			AppDef? appDef = null;
			_reflStates.AppDefs.TryGetValue( _id, out appDef );

			AppState? appState = null;
			_reflStates.AppStates.TryGetValue( _id, out appState );

			PlanState? planState = null;
			if( appState != null && !string.IsNullOrEmpty(appState.PlanName))
			{
				_reflStates.PlanStates.TryGetValue( appState.PlanName, out planState );
			}

			string statusText = appState != null ? Tools.GetAppStateText( appState, planState ) : string.Empty;
			string? planName = appState?.PlanName;


			ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1f,1f,0f,1f) );
			bool opened = ImGui.TreeNodeEx( $"{_id}##{_id}", ImGuiTreeNodeFlags.FramePadding);
			ImGui.PopStyleColor();
			if (ImGui.BeginPopupContextItem())
			{
				if (ImGui.MenuItem("Start"))
				{
					_reflStates.Client.Send( new Net.StartAppMessage( _id, planName ) );
				}

				if (ImGui.MenuItem("Kill"))
				{
					_reflStates.Client.Send( new Net.KillAppMessage( _id ) );
				}

				if (ImGui.MenuItem("Restart"))
				{
					_reflStates.Client.Send( new Net.RestartAppMessage( _id ) );
				}
				ImGui.EndPopup();
			}
			ImGui.SameLine();
			ImGui.TextColored( GetAppStateColor(statusText, appDef), statusText );

			//ImGui.SameLine();
			//ImGui.SetCursorPosX( ImGui.GetWindowWidth()/4f);
			//DrawOptions();

			if( opened )
			{
				//DrawUIBody();
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
				if( appDef != null )
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
