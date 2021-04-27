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
		private PlanDef _pd;
		ReflectedStateRepo _reflStates;
		private string _uniqueUiId = Guid.NewGuid().ToString();
		Dictionary<AppIdTuple, AppRenderer> _appRenderers = new();
		
		public PlanRenderer( PlanDef pd, ReflectedStateRepo reflStates )
		{
			_pd = pd;
			_reflStates = reflStates;
		}

		public void DrawUI()
		{
			ImGui.PushID(_uniqueUiId);

			PlanState? planState = null;
			_reflStates.PlanStates.TryGetValue( _pd.Name, out planState );

			string statusText = planState != null ? Tools.GetPlanStateText( planState ) : string.Empty;

			ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1f,1f,0f,1f) );
			bool opened = ImGui.TreeNodeEx( $"{_pd.Name}##{_pd.Name}", ImGuiTreeNodeFlags.FramePadding);
			ImGui.PopStyleColor();
			if (ImGui.BeginPopupContextItem())
			{
				if (ImGui.MenuItem("Start"))
				{
					_reflStates.Client.Send( new Net.StartPlanMessage( _pd.Name ) );
				}

				if (ImGui.MenuItem("Kill"))
				{
					_reflStates.Client.Send( new Net.KillPlanMessage( _pd.Name ) );
				}

				if (ImGui.MenuItem("Restart"))
				{
					_reflStates.Client.Send( new Net.RestartPlanMessage( _pd.Name ) );
				}

				ImGui.EndPopup();
			}
			ImGui.SameLine();
			ImGui.TextColored( GetPlanStateColor(statusText), statusText );

			//ImGui.SameLine();
			//ImGui.SetCursorPosX( ImGui.GetWindowWidth()/4f);
			//DrawOptions();

			if( opened )
			{
				//DrawUIBody();

				foreach( var ad in _pd.AppDefs )
				{
					AppRenderer? r;
					if( !_appRenderers.TryGetValue( ad.Id, out r ) )
					{
						r = new AppRenderer( ad.Id, _reflStates );
						_appRenderers[ad.Id] = r;
					}
					r.DrawUI();
				}

				ImGui.TreePop();
			}

			ImGui.PopID();
		}

		//public void DrawUI

		System.Numerics.Vector4 GetPlanStateColor( string txt )
		{
			var col = new System.Numerics.Vector4(192, 192, 192, 255);

			if( txt.StartsWith( "Success" ) )
			{
				col = new System.Numerics.Vector4(39, 135, 65, 255);

				col = new System.Numerics.Vector4(39, 135, 65, 255);
			}
			else if( txt.StartsWith( "Failure" ) )
			{
						col = new System.Numerics.Vector4(212, 0, 4, 255);
			}
			else if( txt.StartsWith( "InProgress" ) || txt.StartsWith( "Killing" ) )
			{
				col = new System.Numerics.Vector4(8, 0, 252, 255);
			}

			return col;
		}


	}
}
