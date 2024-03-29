﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Drawing;
using ImGuiNET;
using System.Threading;

namespace Dirigent.Gui
{
	public class AgentWindow : Disposable
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public string MachineId => _machineId;

		private AppConfig _ac;
		private Agent _agent;
		private string _uniqueUiId = Guid.NewGuid().ToString();
		private string _machineId;
		private ImGuiWindow _wnd;

		public AgentWindow( ImGuiWindow wnd, AppConfig ac, string? machineId=null )
		{
			_wnd = wnd;
			_ac = ac;
			log.Debug( $"Running with masterIp={_ac.MasterIP}, masterPort={_ac.MasterPort}" );
			_machineId = machineId ?? _ac.MachineId;
			_agent = new Agent(	_ac, _machineId );
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			_agent.Dispose();
		}

		public void Tick()
		{
			_agent.Tick();
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

				//if( ImGui.BeginTabItem("Plans") )
				//{
				//	DrawPlans();
				//	ImGui.EndTabItem();
				//}

				ImGui.EndTabBar();
			}
		}
		
		Dictionary<AppIdTuple, AppRenderer> _appRenderers = new();

		void DrawApps()
		{
			foreach( var (id, state) in _agent.GetAllAppStates() )
			{
				AppRenderer? r;
				if( !_appRenderers.TryGetValue( id, out r ) )
				{
					r = new AppRenderer( _wnd, id.ToString(), id, _agent );
					_appRenderers[id] = r;
				}
				r.DrawUI();
			}
		}


		Dictionary<string, PlanRenderer> _planRenderers = new();

		void DrawPlans()
		{
			foreach( var pd in _agent.GetAllPlanDefs() )
			{
				PlanRenderer? r;
				if( !_planRenderers.TryGetValue( pd.Name, out r ) )
				{
					r = new PlanRenderer( _wnd, "", pd.Name, _agent );
					_planRenderers[pd.Name] = r;
				}
				r.DrawUI();
			}
		}

	}
}
