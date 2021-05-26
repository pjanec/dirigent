using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Drawing;
using ImGuiNET;
using System.Threading;
using System.Diagnostics.CodeAnalysis;

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

		AppTreeRenderer _appTreeRenderer;
		PlanTreeRenderer _planTreeRenderer;
		ScriptTreeRenderer _scriptTreeRenderer;

		public GuiWindow( ImGuiWindow wnd, AppConfig ac )
		{
			_wnd = wnd;
			_ac = ac;
			log.Debug( $"Running with masterIp={_ac.MasterIP}, masterPort={_ac.MasterPort}" );
			_clientIdent = new Net.ClientIdent(	_ac.ClientId, Net.EMsgRecipCateg.Gui ); // client name will be assigned automatically (a guid)
			_client = new Net.Client( _clientIdent, _ac.MasterIP, _ac.MasterPort, autoConn: true );
			_client.MessageReceived += OnMessage;
			_reflStates = new ReflectedStateRepo( _client );
			_txKillAll = _wnd.GetImage("Resources/skull.png");

			_appTreeRenderer = new AppTreeRenderer( _wnd, _reflStates );
			_planTreeRenderer = new PlanTreeRenderer( _wnd, _reflStates );
			_scriptTreeRenderer = new ScriptTreeRenderer( _wnd, _reflStates );

			_reflStates.OnReset += Reset;
			_reflStates.OnAppsReceived += _appTreeRenderer.Reset;
			_reflStates.OnPlansReceived += _planTreeRenderer.Reset;
			_reflStates.OnScriptsReceived += _scriptTreeRenderer.Reset;

			Reset();
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			_client.MessageReceived -= OnMessage;
			_client.Dispose();
			_reflStates.OnReset -= Reset;
			_reflStates.OnAppsReceived -= _appTreeRenderer.Reset;
			_reflStates.OnPlansReceived -= _planTreeRenderer.Reset;
			_reflStates.OnScriptsReceived -= _scriptTreeRenderer.Reset;
		}

		Dictionary<string, ClientRenderer> _clientRenderers = new();

		void Reset()
		{
			_clientRenderers = new();

			_appTreeRenderer.Reset();
			_planTreeRenderer.Reset();
			_scriptTreeRenderer.Reset();
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
		
		void DrawApps()
		{
			//foreach( var (id, state) in _reflStates.GetAllAppStates() )
			//{
			//	AppRenderer? r;
			//	if( !_appRenderers.TryGetValue( id, out r ) )
			//	{
			//		r = new AppRenderer( _wnd, id, _reflStates );	// will render the effective ones
			//		_appRenderers[id] = r;
			//	}
			//	r.DrawUI();
			//}

			_appTreeRenderer.DrawUI();
		}



		void DrawPlans()
		{
			_planTreeRenderer.DrawUI();
		}




		void DrawScripts()
		{
			_scriptTreeRenderer.DrawUI();
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

		void OnMessage( Net.Message msg )
		{
			switch( msg )
			{
				case Net.RemoteOperationErrorMessage m:
				{
					//MessageBox.Show( m.Message, "Remote Operation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					break;
				}
			}
		}


	}
}
