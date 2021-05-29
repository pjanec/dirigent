using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ImGuiNET;

namespace Dirigent.Gui
{
	public class GuiWinMenuRenderer
	{
		//public float ClientTop { get; private set; }
	
		IDirig _ctrl;
		private ImGuiWindow _wnd;

		
		public GuiWinMenuRenderer( ImGuiWindow wnd, IDirig ctrl )
		{
			_wnd = wnd;
			_ctrl = ctrl;
		}

		public void DrawUI()
		{
			//ClientTop = 0;
			if (ImGui.BeginMenuBar())
			{
				if (ImGui.BeginMenu("File"))
				{
					if (ImGui.MenuItem("Exit"))
					{
						_wnd.Close();
					}
					ImGui.EndMenu();
				}

				if (ImGui.BeginMenu("Tools"))
				{
					if (ImGui.MenuItem("Reload Shared Config"))
					{
						_ctrl.Send(new Net.ReloadSharedConfigMessage());
					}

					if (ImGui.MenuItem("Kill All"))
					{
						_ctrl.Send(new Net.KillAllMessage(_ctrl.Name, new KillAllArgs()));
					}

					ImGui.EndMenu();
				}

				//var mainMenuSize = ImGui.GetWindowSize();
				//ClientTop = mainMenuSize.Y;
				ImGui.EndMenuBar();
			}
		}

	}
}
