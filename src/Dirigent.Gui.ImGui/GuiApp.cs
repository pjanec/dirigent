using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Drawing;
using ImGuiNET;
using System.Threading;
using ImVec2 = System.Numerics.Vector2;

namespace Dirigent.Gui
{
	public class GuiApp : Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		private AppConfig _ac;
		private ImGuiWindow _wnd;
		private string _uniqueUiId = Guid.NewGuid().ToString();
		private GuiWindow? _guiWin;
		private IDirig _ctrl;

		public GuiApp( AppConfig ac )
		{
			_ac = ac;
			log.Debug( $"Running with masterIp={_ac.MasterIP}, masterPort={_ac.MasterPort}" );

			_wnd = new ImGuiWindow("Dirigent Gui", width:400, height:650);
			_wnd.OnDrawUI += DrawUI;

			_guiWin = new GuiWindow( _wnd, _ac );
			_ctrl = _guiWin.Ctrl;


		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if( !disposing ) return;

			_guiWin?.Dispose();
			_wnd.Dispose();
		}

		public EAppExitCode run()
		{
			var exitCode = EAppExitCode.NoError;
			while( _wnd.Exists )
			{
				_wnd.Tick();

				_guiWin?.Tick();

				Thread.Sleep( 50 );
			}

			return exitCode;
		}

		void DrawUI()
		{
			//// main menu
			//float clientTop = 0;
			//if( ImGui.BeginMainMenuBar() )
			//{
			//	if( ImGui.BeginMenu("File") )
			//	{
			//		if( ImGui.MenuItem("Exit") )
			//		{
			//			_wnd.Close();
			//		}
			//		ImGui.EndMenu();
			//	}

			//	if( ImGui.BeginMenu("Tools") )
			//	{
			//		if( ImGui.MenuItem("Reload Shared Config") )
			//		{
			//			_ctrl.Send( new Net.ReloadSharedConfigMessage() );
			//		}

			//		if( ImGui.MenuItem("Kill All") )
			//		{
			//			_ctrl.Send( new Net.KillAllMessage( _ctrl.Name, new KillAllArgs() ) );
			//		}

			//		ImGui.EndMenu();
			//	}

			//	var mainMenuSize = ImGui.GetWindowSize();
			//	clientTop = mainMenuSize.Y;
			//	ImGui.EndMainMenuBar();
			//}

			// client area
			//ImGui.SetNextWindowPos(new ImVec2(0, clientTop));
			//ImGui.SetNextWindowSize(new ImVec2(_wnd.Size.X, _wnd.Size.Y - clientTop));
			ImGui.SetNextWindowPos(new ImVec2(0, 0));
			ImGui.SetNextWindowSize(new ImVec2(_wnd.Size.X, _wnd.Size.Y));
			if (_guiWin != null)
			{
				if (ImGui.Begin("Gui", ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoTitleBar | (_guiWin.HasMenu?ImGuiWindowFlags.MenuBar:0) ))
				{
					_guiWin?.DrawUI();
					ImGui.End();
				}
			}
		}
		
	}
}
