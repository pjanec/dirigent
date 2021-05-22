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
	public class MasterWindow : Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		private AppConfig _ac;
		private Master _master;
		private string _uniqueUiId = Guid.NewGuid().ToString();
		private ImGuiWindow _wnd;

		public MasterWindow( ImGuiWindow wnd, AppConfig ac )
		{
			_wnd = wnd;

			_ac = ac;

			if( string.IsNullOrEmpty(_ac.SharedCfgFileName) ) throw new Exception("Shared Config not define.");

			log.Debug( $"Running with masterIp={_ac.MasterIP}, masterPort={_ac.MasterPort}" );

			_master = new Master( _ac.LocalIP, _ac.MasterPort, _ac.CliPort, _ac.SharedCfgFileName );
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			_master.Dispose();
		}

		public void Tick()
		{
			_master.Tick();
		}

		public void DrawUI()
		{
			float ww = ImGui.GetWindowWidth();

			if( ImGui.BeginTabBar("MainTabBar") )
			{
				if( ImGui.BeginTabItem("Apps") )
				{
					DrawApps();
					ImGui.EndTabItem();
				}

				if( ImGui.BeginTabItem("Plans") )
				{
					DrawPlans();
					ImGui.EndTabItem();
				}

				ImGui.EndTabBar();
			}
		}
		
		Dictionary<AppIdTuple, AppRenderer> _appRenderers = new();

		void DrawApps()
		{
			foreach( var (id, state) in _master.GetAllAppStates() )
			{
				AppRenderer? r;
				if( !_appRenderers.TryGetValue( id, out r ) )
				{
					r = new AppRenderer( _wnd, id, _master );
					_appRenderers[id] = r;
				}
				r.DrawUI();
			}
		}


		Dictionary<string, PlanRenderer> _planRenderers = new();

		void DrawPlans()
		{
			foreach( var pd in _master.GetAllPlanDefs() )
			{
				PlanRenderer? r;
				if( !_planRenderers.TryGetValue( pd.Name, out r ) )
				{
					r = new PlanRenderer( _wnd, pd.Name, _master );
					_planRenderers[pd.Name] = r;
				}
				r.DrawUI();
			}
		}


	}
}
