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
	public class GuiWindow : Disposable
	{
		public 	IDirig Ctrl => _reflStates;

		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		private AppConfig _ac;
		private Net.ClientIdent _clientIdent; // name of the network client; messages are marked with that
		private Net.Client _client;
		private ReflectedStateRepo _reflStates;
		private string _uniqueUiId = Guid.NewGuid().ToString();
		private ImGuiWindow _wnd;
		private ImageInfo _txKillAll;


		public GuiWindow( ImGuiWindow wnd, AppConfig ac )
		{
			_wnd = wnd;
			_ac = ac;
			log.Debug( $"Running with masterIp={_ac.MasterIP}, masterPort={_ac.MasterPort}" );
			_clientIdent = new Net.ClientIdent(	string.Empty, Net.EMsgRecipCateg.Gui ); // client name will be assigned automatically (a guid)
			_client = new Net.Client( _clientIdent, _ac.MasterIP, _ac.MasterPort, autoConn: true );
			_reflStates = new ReflectedStateRepo( _client );
			_txKillAll = _wnd.GetImage("Resources/skull.png");
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			_client.Dispose();
		}

		public void Tick()
		{
			_client.Tick();
		}

		void DrawToolBar()
		{
			if( ImGuiTools.ImgBtn( _txKillAll ) )
			{
				_reflStates.Send( new Net.KillAllMessage( _reflStates.Name, new KillAllArgs() ) );
			}
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

				if( ImGui.BeginTabItem("Scripts") )
				{
					DrawScripts();
					ImGui.EndTabItem();
				}

				var tabOpen = ImGui.BeginTabItem("Clients");

				ImGui.SameLine();
				DrawToolBar();

				if( tabOpen )
				{
					DrawClients();
					ImGui.EndTabItem();
				}


				ImGui.EndTabBar();
			}
		}
		
		Dictionary<string, ClientRenderer> _clientRenderers = new();

		void DrawApps()
		{
			foreach( var (id, state) in _reflStates.GetAllClientStates() )
			{
				ClientRenderer? r;
				if( !_clientRenderers.TryGetValue( id, out r ) )
				{
					r = new ClientRenderer( _wnd, id, _reflStates );	// will render the effective ones
					_clientRenderers[id] = r;
				}
				r.DrawUI();
			}
		}


		Dictionary<string, PlanRenderer> _planRenderers = new();

		void DrawPlans()
		{
			foreach( var pd in _reflStates.GetAllPlanDefs() )
			{
				PlanRenderer? r;
				if( !_planRenderers.TryGetValue( pd.Name, out r ) )
				{
					r = new PlanRenderer( _wnd, pd.Name, _reflStates );
					_planRenderers[pd.Name] = r;
				}
				r.DrawUI();
			}
		}

		void DrawScripts()
		{
		}

		void DrawClients()
		{
			foreach( var (id, state) in _reflStates.GetAllClientStates() )
			{
				ClientRenderer? r;
				if( !_clientRenderers.TryGetValue( id, out r ) )
				{
					r = new ClientRenderer( _wnd, id, _reflStates );	// will render the effective ones
					_clientRenderers[id] = r;
				}
				r.DrawUI();
			}
		}

	}
}
