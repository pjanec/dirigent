using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Drawing;
using Dirigent.Common;
using ImGuiNET;
using System.Threading;

namespace Dirigent.Gui
{
	public class MasterWindow : Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		private AppConfig _ac;
		private Agent.Master _master;
		private string _uniqueUiId = Guid.NewGuid().ToString();

		public MasterWindow( AppConfig ac )
		{
			_ac = ac;

			if( _ac.SharedConfig is null ) throw new Exception("Shared Config not define.");

			log.Info( $"Running with masterIp={_ac.MasterIP}, masterPort={_ac.MasterPort}" );

			_master = new Agent.Master( _ac.LocalIP, _ac.MasterPort, _ac.CliPort, _ac.SharedConfig );
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
		
		//Dictionary<AppIdTuple, AppRenderer> _appRenderers;

		void DrawApps()
		{
			//foreach( var (id, state) in _reflStates.AppStates )
			//{
				
			//	//DrawApp( id, state );
			//}
		}


		void DrawPlans()
		{
			//foreach( var pd in _reflStates.PlanDefs )
			//{
			//	//DrawPlan( pd );
			//}
		}


	}
}
