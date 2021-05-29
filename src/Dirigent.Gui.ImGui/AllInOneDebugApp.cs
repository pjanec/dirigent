using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Drawing;
using ImGuiNET;
using System.Threading;

namespace Dirigent.Gui
{
	public class AllInOneDebugApp : Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		private AppConfig _ac;
		private ImGuiWindow _wnd;
		private string _uniqueUiId = Guid.NewGuid().ToString();
		private MasterWindow? _masterWin;
		private GuiWindow? _guiWin;
		private AgentWindow? _agentWin1;
		private AgentWindow? _agentWin2;

		public AllInOneDebugApp( AppConfig ac )
		{
			_ac = ac;
			log.Debug( $"Running with masterIp={_ac.MasterIP}, masterPort={_ac.MasterPort}" );

			_wnd = new ImGuiWindow("Dirigent Debug All-In-One", width:1000, height:700);
			_wnd.OnDrawUI += DrawUI;

			_masterWin = new MasterWindow( _wnd, _ac );
			_guiWin = new GuiWindow( _wnd, _ac );
			_agentWin1 = new AgentWindow( _wnd, _ac, "m1" );
			_agentWin2 = new AgentWindow( _wnd, _ac, "m2" );
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if( !disposing ) return;

			_masterWin?.Dispose();
			_guiWin?.Dispose();
			_agentWin1?.Dispose();
			_agentWin2?.Dispose();

			_wnd.Dispose();
		}

		public EAppExitCode run()
		{
			var exitCode = EAppExitCode.NoError;
			while( _wnd.Exists )
			{
				_wnd.Tick();

				_masterWin?.Tick();
				_guiWin?.Tick();
				_agentWin1?.Tick();
				_agentWin2?.Tick();

				//Thread.Sleep( _ac.TickPeriod );
				Thread.Sleep( 50 );
			}

			return exitCode;
		}

		void DrawUI()
		{
			ImGui.SetNextWindowPos( new System.Numerics.Vector2( 0, 0 ) );
			ImGui.SetNextWindowSize( new System.Numerics.Vector2( _wnd.Size.X/2, _wnd.Size.Y/2 ));
			if( _masterWin != null )
			{
				if ( ImGui.Begin( "Master" ) )
				{
					_masterWin.DrawUI();
					ImGui.End();
				}
			}

			ImGui.SetNextWindowPos( new System.Numerics.Vector2( _wnd.Size.X/2, 0 ) );
			ImGui.SetNextWindowSize( new System.Numerics.Vector2( _wnd.Size.X/2, _wnd.Size.Y/2 ));
			if( _guiWin != null )
			{
				if ( ImGui.Begin( "Gui", (_guiWin.HasMenu ? ImGuiWindowFlags.MenuBar : 0)) )
				{
					_guiWin?.DrawUI();
					ImGui.End();
				}
			}

			ImGui.SetNextWindowPos( new System.Numerics.Vector2( 0, _wnd.Size.Y/2 ) );
			ImGui.SetNextWindowSize( new System.Numerics.Vector2( _wnd.Size.X/2, _wnd.Size.Y/2 ));
			if( _agentWin1 != null )
			{
				if ( ImGui.Begin( $"Agent-{_agentWin1.MachineId}" ) )
				{
					_agentWin1?.DrawUI();
				}
			}

			ImGui.SetNextWindowPos( new System.Numerics.Vector2( _wnd.Size.X/2, _wnd.Size.Y/2 ) );
			ImGui.SetNextWindowSize( new System.Numerics.Vector2( _wnd.Size.X/2, _wnd.Size.Y/2 ));
			if( _agentWin2 != null )
			{
				if ( ImGui.Begin( $"Agent-{_agentWin2.MachineId}" ) )
				{
					_agentWin2?.DrawUI();
				}
			}
		}
		

	}
}
