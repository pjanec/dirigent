using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Reflection;
using System.Runtime.InteropServices;
using System.IO;
using System.Configuration;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Dirigent.Gui.WinForms
{
	public partial class frmMain : Form
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

		private NotifyIconHandler _notifyIconHandler;

		public System.ComponentModel.IContainer Components => components;

		StatusBarManager _statusBarManager;
		
		GuiCore _core;
		AppConfig _ac;

		private MainAppsTab _tabApps;
		private MainPlansTab _tabPlans;
		private MainScriptsTab _tabScripts;
		private MainMachsTab _tabMachs;
		private MainFilesTab _tabFiles;

		private ContextMenuStrip mnuPlanList;  // context menu for the 'Open' toolbar button

		MenuBuilder _menuBuilder;
		
		IDirig Ctrl => _core.Ctrl;

		public bool ShowJustAppFromCurrentPlan
		{
			get { return btnShowJustAppsFromCurrentPlan.Checked; }
			set	{ btnShowJustAppsFromCurrentPlan.Checked = value; }
		}

		public frmMain(
			AppConfig ac,
			NotifyIconHandler Handler,
			string machineId, // empty if no local agent was started with the GUI
			string rootForRelativePaths
		)
		{
			_ac = ac;
			_core = new GuiCore( ac, machineId, rootForRelativePaths );
		
			_notifyIconHandler = Handler;

			InitializeComponent();

			if( Common.Properties.Settings.Default.GridRowSpacing > 0 ) 
			{
				this.gridPlans.RowTemplate.Height = Common.Properties.Settings.Default.GridRowSpacing;
				this.gridApps.RowTemplate.Height = Common.Properties.Settings.Default.GridRowSpacing;
			}


			HotKeysRegistrator.RegisterHotKeys( this.Handle );

			ShowJustAppFromCurrentPlan = Tools.BoolFromString( Common.Properties.Settings.Default.ShowJustAppsFromCurrentPlan );


			UpdateMainMenu(); // initial menus
			_core.ReflStates.OnActionsReceived += () => UpdateMainMenu(); // when Action arrived from master, we rebuild the menu
			_core.ReflStates.OnReset += () => Reset();


			_tabApps = new MainAppsTab( this, _core, gridApps );
			_tabPlans = new MainPlansTab( this, _core, gridPlans );
			_tabScripts = new MainScriptsTab( this, _core, gridScripts );
			_tabMachs = new MainMachsTab( this, _core, gridMachs );
			_tabFiles = new MainFilesTab( this, _core, gridFiles );

			// start ticking
			log.DebugFormat( "MainForm's timer period: {0}", ac.TickPeriod );
			tmrTick.Interval = ac.TickPeriod;
			tmrTick.Enabled = true;

			_menuBuilder = new MenuBuilder( _core );

			_core.Client.MessageReceived += OnMessage;

		}

		void myDispose()
		{
			_core.Client.MessageReceived -= OnMessage;

			tmrTick.Enabled = false;

			_core.Dispose();

			_statusBarManager?.Dispose();
		}

		private void frmMain_Load( object sender, EventArgs e )
		{
			_statusBarManager = new StatusBarManager( this.statusStrip );

			if( !string.IsNullOrEmpty( _ac.GatewayId) )
			{
				ConnectViaSSH( _ac.GatewayId );
			}
		}

		void OnMessage( Net.Message msg )
		{
			switch( msg )
			{
				case Net.RemoteOperationErrorMessage m:
				{
					MessageBox.Show( m.Message, "Remote Operation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					break;
				}

				case Net.UserNotificationMessage m:
				{
					if( m.HostClientId != _core.Client.Ident.Name ) // ignore if not for us
						break;

					var title = string.IsNullOrEmpty(m.Title) ? "Dirigent" : $"Dirigent - {m.Title}";
					
					if (m.PresentationType == Net.UserNotificationMessage.EPresentationType.MessageBox)
					{
						MessageBoxIcon icon = m.Category switch
						{
							Net.UserNotificationMessage.ECategory.Info => MessageBoxIcon.Information,
							Net.UserNotificationMessage.ECategory.Warning => MessageBoxIcon.Warning,
							Net.UserNotificationMessage.ECategory.Error => MessageBoxIcon.Error,
							_ => throw new Exception( "Unknown notification category" )
						};

						MessageBoxButtons btns = MessageBoxButtons.OK;
						if (m.Action != null) btns = MessageBoxButtons.OKCancel;

						var dlgres = MessageBox.Show( m.Message, title, btns, icon );
						if( m.Action != null && dlgres == DialogResult.OK )
						{
							Ctrl.Send( new Net.RunActionMessage( Ctrl.Name, m.Action, Ctrl.Name, m.Attributes ) );
						}
					}
					else if (m.PresentationType == Net.UserNotificationMessage.EPresentationType.BalloonTip)
					{
						ToolTipIcon icon = m.Category switch
						{
							Net.UserNotificationMessage.ECategory.Info => ToolTipIcon.Info,
							Net.UserNotificationMessage.ECategory.Warning => ToolTipIcon.Warning,
							Net.UserNotificationMessage.ECategory.Error => ToolTipIcon.Error,
							_ => throw new Exception( "Unknown notification category" )
						};
						
						Action onClick = m.Action != null
							? () => Ctrl.Send( new Net.RunActionMessage( Ctrl.Name, m.Action, Ctrl.Name, m.Attributes ) )
							: null;

						int timeout = m.Timeout > 0 ? (int)(m.Timeout*1000) : 5000;

						_notifyIconHandler.ShowBalloonTip( timeout, title, m.Message, icon, onClick );
					}
					else
					{
						throw new Exception( "Unknown notification presentation type" );
					}
					break;
				}

				// note: other messages are handled is done in ReflectedStateRepo...
			}
		}
		

		void Reset()
		{
			_tabApps.Reset();
			_tabFiles.Reset();
			_tabMachs.Reset();
			_tabPlans.Reset();
		}

		void setTitle()
		{
			string planName = "<no plan>";

			var currPlan = _core.CurrentPlan;
			if( currPlan != null )
			{
				planName = currPlan.Name;
			}

			this.Text = string.Format( "Dirigent [{0}] - {1}", _core.MachineId, planName );
			if( this._notifyIconHandler != null )
			{
				this._notifyIconHandler.Text = string.Format( "Dirigent [{0}] - {1}", _core.MachineId, planName );
			}

		}


		private void handleOperationError( Exception ex )
		{
			this._notifyIconHandler.ShowBalloonTip( 5000, "Dirigent Operation Error", ex.Message, ToolTipIcon.Error );
			log.ErrorFormat( "Exception: {0}\n{1}", ex.Message, ex.StackTrace );
		}

		void Tick()
		{
			_core.Tick();
		}

		private void tmrTick_Tick( object sender, EventArgs e )
		{
			try
			{
				Tick();
			}
			catch( RemoteOperationErrorException ex ) // operation exception (not necesarily remote, could be also local
				// as all operational requests always go through the network if
				// connected to master
			{
				// if this GUI was the requestor of the operation that failed
				if( ex.Requestor == _core.Client.Ident.Sender )
				{
					handleOperationError( ex );
				}
			}
			catch( Exception ex ) // local operation exception
			{
				handleOperationError( ex );
			}

			refreshGui();
		}

		public bool IsConnected => _core.Client.IsConnected;

		void refreshStatusBar()
		{
			string text = "";

			if ( IsConnected )
			{
				text = $"Connected to [{_core.Client.MasterIP}:{_core.Client.MasterPort}]";
			}
			else
			{
				text = $"Disconnected from [{_core.Client.MasterIP}:{_core.Client.MasterPort}]";
			}
			AppMessenger.Instance.Send( new AppMessages.StatusText( "", text ) );

			text = "";
			if (_core.GatewayManager.IsConnected)
			{
				var gw = _core.GatewayManager.CurrentSession.Gateway;
				text = $"SSH {gw.Id} [{gw.ExternalIP}:{gw.Port}]";
			}
			AppMessenger.Instance.Send( new AppMessages.StatusText( "SSH", text ) );

		}

		void refreshMenu()
		{
			bool isConnected = IsConnected;
			bool hasPlan = _core.CurrentPlan != null;
			planToolStripMenuItem.Enabled = isConnected || _core.AllowLocalIfDisconnected;
			startPlanToolStripMenuItem.Enabled = hasPlan;
			stopPlanToolStripMenuItem.Enabled = hasPlan;
			killPlanToolStripMenuItem.Enabled = hasPlan;
			restartPlanToolStripMenuItem.Enabled = hasPlan;
		}

		void refreshGui()
		{
			_tabApps.Refresh();
			_tabPlans.Refresh();
			_tabScripts.Refresh();
			_tabMachs.Refresh();
			//_tabFiles.Refresh();
			refreshStatusBar();
			refreshMenu();
			setTitle();
			_statusBarManager?.Tick();
		}

		private void frmMain_Resize( object sender, EventArgs e )
		{
			//if (FormWindowState.Minimized == this.WindowState)
			//{
			//    _callbacks.onMinimizeDeleg();
			//}

			//else if (FormWindowState.Normal == this.WindowState)
			//{
			//}
		}

		private void frmMain_FormClosing( object sender, FormClosingEventArgs e )
		{
			//if( e.CloseReason == CloseReason.UserClosing )
			//{
			//	// prevent window closing
			//	e.Cancel = true;
			//	Hide();
			//}
		}

		private void frmMain_FormClosed(object sender, FormClosedEventArgs e)
		{
			myDispose();
		}


		void OnHotKey( int keyId )
		{
			switch ( keyId )
			{
				case HotKeysRegistrator.HOTKEY_ID_START_CURRENT_PLAN:
				{
					var currPlan = _core.CurrentPlan;
					if( currPlan != null )
					{
						Ctrl.Send( new Net.StartPlanMessage( Ctrl.Name, currPlan.Name ) );
					}
					break;
				}

				case HotKeysRegistrator.HOTKEY_ID_KILL_CURRENT_PLAN:
				{
					var currPlan = _core.CurrentPlan;
					if( currPlan != null )
					{
						Ctrl.Send( new Net.KillPlanMessage( Ctrl.Name, currPlan.Name ) );
					}
					break;
				}

				case HotKeysRegistrator.HOTKEY_ID_RESTART_CURRENT_PLAN:
				{
					var currPlan = _core.CurrentPlan;
					if( currPlan != null )
					{
						Ctrl.Send( new Net.RestartPlanMessage( Ctrl.Name, currPlan.Name ) );
					}
					break;
				}


				case HotKeysRegistrator.HOTKEY_ID_SELECT_PLAN_0:
				{
					_core.SelectPlan( null );
					break;
				}

				case HotKeysRegistrator.HOTKEY_ID_SELECT_PLAN_1:
				case HotKeysRegistrator.HOTKEY_ID_SELECT_PLAN_2:
				case HotKeysRegistrator.HOTKEY_ID_SELECT_PLAN_3:
				case HotKeysRegistrator.HOTKEY_ID_SELECT_PLAN_4:
				case HotKeysRegistrator.HOTKEY_ID_SELECT_PLAN_5:
				case HotKeysRegistrator.HOTKEY_ID_SELECT_PLAN_6:
				case HotKeysRegistrator.HOTKEY_ID_SELECT_PLAN_7:
				case HotKeysRegistrator.HOTKEY_ID_SELECT_PLAN_8:
				case HotKeysRegistrator.HOTKEY_ID_SELECT_PLAN_9:
				{
					int i = keyId - HotKeysRegistrator.HOTKEY_ID_SELECT_PLAN_1; // zero-based index of plan
					List<PlanDef> plans = new List<PlanDef>( Ctrl.GetAllPlanDefs() );
					if( i < plans.Count )
					{
						var planName = plans[i].Name;
						this._notifyIconHandler.ShowBalloonTip( 5000, String.Format( "{0}", planName ), " ", ToolTipIcon.Info );
						_core.SelectPlan( planName );
					}
					break;
				}
			}
		}
		

		protected override void WndProc( ref Message m )
		{
			if( m.Msg == 0x0312 )
			{
				var keyId = m.WParam.ToInt32();
				OnHotKey( keyId );
			}
			base.WndProc( ref m );
		}

		private void killAllWithConfirmation()
		{
			if(	 Common.Properties.Settings.Default.ConfirmKillAll == 0	// do not want to confirm
			      ||
			     MessageBox.Show( "Kill all apps???", "Dirigent",
								 MessageBoxButtons.OKCancel, MessageBoxIcon.Warning ) == DialogResult.OK )
			{
				var args = new KillAllArgs() {};
				Ctrl.Send( new Net.KillAllMessage( Ctrl.Name, args ) );
			}
		}

		private void onlineDocumentationToolStripMenuItem_Click( object sender, EventArgs e )
		{
			var url = "https://github.com/pjanec/dirigent";
			System.Diagnostics.Process.Start( new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
		}

		private void reloadSharedConfigToolStripMenuItem_Click( object sender, EventArgs e )
		{
			var args = new ReloadSharedConfigArgs() { KillApps = false };
			Ctrl.Send( new Net.ReloadSharedConfigMessage( Ctrl.Name, args ) );
		}

		private void terminateAndKillAppsToolStripMenuItem_Click( object sender, EventArgs e )
		{
			if( MessageBox.Show( "Terminate Dirigent on all computers?\n\nThis will also kill all apps!", "Dirigent", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning ) == DialogResult.OK )
			{
				var args = new TerminateArgs() { KillApps = true };
				Ctrl.Send( new Net.TerminateMessage( Ctrl.Name, args ) );
			}
		}

		private void terminateAndLeaveAppsRunningToolStripMenuItem_Click( object sender, EventArgs e )
		{
			if( MessageBox.Show( "Terminate Dirigent on all computers?\n\nThis will leave the already started apps running and you will need to kill them yourselves!)", "Dirigent",
								 MessageBoxButtons.OKCancel, MessageBoxIcon.Warning ) == DialogResult.OK )
			{
				var args = new TerminateArgs() { KillApps = false };
				Ctrl.Send( new Net.TerminateMessage( Ctrl.Name, args ) );
			}
		}

		private void killAllRunningAppsToolStripMenuItem_Click( object sender, EventArgs e )
		{
			killAllWithConfirmation();
		}

		private void rebootAllToolStripMenuItem1_Click( object sender, EventArgs e )
		{
			if( MessageBox.Show( "Reboot all computers where Dirigent is running?", "Dirigent", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning ) == DialogResult.OK )
			{
				var args = new ShutdownArgs() { Mode = EShutdownMode.Reboot };
				Ctrl.Send( new Net.ShutdownMessage( Ctrl.Name, args, null ) );
			}
		}

		private void shutdownAllToolStripMenuItem1_Click( object sender, EventArgs e )
		{
			if( MessageBox.Show( "Shut down all computers where Dirigent is running?", "Dirigent", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning ) == DialogResult.OK )
			{
				var args = new ShutdownArgs() { Mode = EShutdownMode.PowerOff };
				Ctrl.Send( new Net.ShutdownMessage( Ctrl.Name, args, null ) );
			}
		}

		private void reinstallManuallyToolStripMenuItem_Click( object sender, EventArgs e )
		{
			if( MessageBox.Show( "Reinstall Dirigent on all computers?\n\nThis will kills all apps and temporarily terminates the dirigent on all computers!", "Dirigent",
								 MessageBoxButtons.OKCancel, MessageBoxIcon.Warning ) == DialogResult.OK )
			{
				var args = new ReinstallArgs() { DownloadMode = EDownloadMode.Manual };
				Ctrl.Send( new Net.ReinstallMessage( Ctrl.Name, args ) );
			}
		}

		private void exitToolStripMenuItem1_Click( object sender, EventArgs e )
		{
			AppMessenger.Instance.Send( new Dirigent.AppMessages.ExitApp() );	 // handled in GuiApp
		}

		private void bntAppsKillAll_Click( object sender, EventArgs e )
		{
			killAllWithConfirmation();
		}

		private void btnPlansKillAll_Click( object sender, EventArgs e )
		{
			killAllWithConfirmation();
		}

		private void btnScriptsKillAll_Click( object sender, EventArgs e )
		{
			killAllWithConfirmation();
		}

		private void btnMachsKillAll_Click( object sender, EventArgs e )
		{
			killAllWithConfirmation();
		}

		private void btnFilesKillAll_Click( object sender, EventArgs e )
		{
			killAllWithConfirmation();
		}

		// Apps

		private void gridApps_CellFormatting( object sender, DataGridViewCellFormattingEventArgs e )
		{
			_tabApps.CellFormatting( sender, e );
		}

		private void gridApps_MouseClick( object sender, MouseEventArgs e )
		{
			_tabApps.MouseClick( sender, e );
		}

		private void gridApps_MouseDoubleClick( object sender, MouseEventArgs e )
		{
			_tabApps.MouseDoubleClick( sender, e );
		}

		// Plans

		private void gridPlans_CellFormatting( object sender, DataGridViewCellFormattingEventArgs e )
		{
			_tabPlans.CellFormatting( sender, e );
		}

		private void gridPlans_MouseClick( object sender, MouseEventArgs e )
		{
			_tabPlans.MouseClick( sender, e );
		}

		private void gridPlans_MouseDoubleClick( object sender, MouseEventArgs e )
		{
			_tabPlans.MouseDoubleClick( sender, e );
		}

		// Script

		private void gridScripts_CellFormatting( object sender, DataGridViewCellFormattingEventArgs e )
		{
			_tabScripts.CellFormatting( sender, e );
		}

		private void gridScripts_MouseClick( object sender, MouseEventArgs e )
		{
			_tabScripts.MouseClick( sender, e );
		}

		private void gridScripts_MouseDoubleClick( object sender, MouseEventArgs e )
		{
			_tabScripts.MouseDoubleClick( sender, e );
		}

		// Machs

		private void gridMachs_CellFormatting( object sender, DataGridViewCellFormattingEventArgs e )
		{
			_tabMachs.CellFormatting( sender, e );
		}

		private void gridMachs_MouseClick( object sender, MouseEventArgs e )
		{
			_tabMachs.MouseClick( sender, e );
		}

		private void gridMachs_MouseDoubleClick( object sender, MouseEventArgs e )
		{
			_tabMachs.MouseDoubleClick( sender, e );
		}


		// Files

		private void gridFiles_CellFormatting( object sender, DataGridViewCellFormattingEventArgs e )
		{
			_tabFiles.CellFormatting( sender, e );
		}

		private void gridFiles_MouseClick( object sender, MouseEventArgs e )
		{
			_tabFiles.MouseClick( sender, e );
		}

		private void gridFiles_MouseDoubleClick( object sender, MouseEventArgs e )
		{
			_tabFiles.MouseDoubleClick( sender, e );
		}

		// Menus

		private void aboutMenuItem_Click( object sender, EventArgs e )
		{
			var version = Assembly.GetExecutingAssembly().GetName().Version;

			// read the content of versionstamp file next to dirigent binaries
			var verStampPath = System.IO.Path.Combine( Tools.GetExeDir(), "VersionStamp.txt" );
			string verStampText;
			try
			{

				verStampText = File.ReadAllText( verStampPath );
			}
			catch( Exception )
			{
				verStampText = "Version info file not found:\n" + verStampPath;
			}

			MessageBox.Show(
				$"Dirigent by pjanec, MIT license\n"+
				"\n\n" +
				verStampText +
				"\n\n" +
				"Icons https://icons8.com\n"+
				"",
				"About Dirigent",
				MessageBoxButtons.OK,
				MessageBoxIcon.Information );
		}

		private void ShowNoPlanSelectedError()
		{
			MessageBox.Show(
				"No plan selected. Select a plan first.",
				"Dirigent",
				MessageBoxButtons.OK,
				MessageBoxIcon.Information );
		}

		private void startPlanMenuItem_Click( object sender, EventArgs e )
		{
			if( _core.CurrentPlan is null )
			{
				ShowNoPlanSelectedError();
				return;
			}
			WFT.GuardedOp( () => Ctrl.Send( new Net.StartPlanMessage( Ctrl.Name, _core.CurrentPlan.Name ) ) );
		}

		private void stopPlanMenuItem_Click( object sender, EventArgs e )
		{
			if( _core.CurrentPlan is null )
			{
				ShowNoPlanSelectedError();
				return;
			}
			WFT.GuardedOp( () => Ctrl.Send( new Net.StopPlanMessage( Ctrl.Name, _core.CurrentPlan.Name ) ) );
		}

		private void killPlanMenuItem_Click( object sender, EventArgs e )
		{
			if( _core.CurrentPlan is null )
			{
				ShowNoPlanSelectedError();
				return;
			}
			WFT.GuardedOp( () => Ctrl.Send( new Net.KillPlanMessage( Ctrl.Name, _core.CurrentPlan.Name ) ) );
		}

		private void restartPlanMenuItem_Click( object sender, EventArgs e )
		{
			if( _core.CurrentPlan is null )
			{
				ShowNoPlanSelectedError();
				return;
			}
			WFT.GuardedOp( () => Ctrl.Send( new Net.RestartPlanMessage( Ctrl.Name, _core.CurrentPlan.Name ) ) );
		}

		private void selectPlanMenuItem_Click( object sender, EventArgs e )
		{
			//selectPlanToolStripMenuItem.ShowDropDown();
			if( mnuPlanList is not null )
			{
				mnuPlanList.Show( this, this.PointToClient( Cursor.Position ) );
			}
		}

		void connectViaSSH()
		{
			//if( !string.IsNullOrEmpty(_core.MachineId) )
			//{
			//	MessageBox.Show(
			//		$"This GUI is running on top a Dirigent Agent on machine '{_core.MachineId}'.\n" +
			//		$"SSH connection is only possible if the GUI is running standalone, without an agent'.\n" +
			//		$"Please run the GUI without an agent (--mode gui).",
			//		"Dirigent",
			//		MessageBoxButtons.OK,
			//		MessageBoxIcon.Information );
			//	return;
			//}

			if ( _core.GatewayManager.IsConnected )
			{
				var dlg = new SshDisconnectDlg( _core.GatewayManager );
				if (dlg.ShowDialog() == DialogResult.OK)
				{
					_core.GatewayManager.Disconnect();
				}
			}
			else
			{
				var dlg = new SshConnectDlg( _core.GatewayManager );
				if (dlg.ShowDialog() == DialogResult.OK)
				{
					var gw = dlg.SelectedGateway;
					ConnectViaSSH( gw.Id );
				}
			}
		}

		void ConnectViaSSH( string gatewayId )
		{
			var gw = _core.GatewayManager.Gateways.First( x => string.Equals( x.Id, gatewayId, StringComparison.OrdinalIgnoreCase ) );
			if (gw is null)
				throw new Exception( $"Gateway '{gatewayId}' not found." );
			
			try
			{
				_core.GatewayManager.Connect( gw );

				// find the port mapping for the master IP and port
				var gws = _core.GatewayManager.CurrentSession;
				if( gws is null )
					throw new Exception( "Gateway session not loaded." );

				var localFwdIpAndPort = gws.GetPortMapByMachineIP( gws.MasterIP, GatewaySession.DirigentServiceName );
				if (localFwdIpAndPort is null)
					throw new Exception( $"Gateway session does not contain port mapping for service '{GatewaySession.DirigentServiceName}' on machine {gws.MasterIP}." );

				_core.Client.Reconnect( localFwdIpAndPort.IP, localFwdIpAndPort.Port );
						
			}
			catch( Exception ex )
			{
				ExceptionDialog.showException( ex, $"SSH connection to {gw.ExternalIP}:{gw.Port} failed.", "" );
			}
		}

		void addPlanSelectionMenuItem( int index, string planName )
		{
			EventHandler clickHandler = ( sender, args ) => WFT.GuardedOp( () => { _core.SelectPlan( planName); } );

			var itemText = String.Format( "&{0}: {1}", index, string.IsNullOrEmpty(planName)?"<no plan>":planName );
			var menuItem = new System.Windows.Forms.ToolStripMenuItem( itemText, null, clickHandler );
			selectPlanToolStripMenuItem.DropDownItems.Add( menuItem );

			mnuPlanList.Items.Add( itemText, null, clickHandler );
		}


		public void PopulatePlanSelectionMenu()
		{
			mnuPlanList = new ContextMenuStrip();

			selectPlanToolStripMenuItem.DropDownItems.Clear();

			// fill the Plan -> Load menu with items
			int index = 0;
			addPlanSelectionMenuItem( index++, string.Empty ); // no plan

			foreach( var plan in _core.PlanRepo )
			{
				addPlanSelectionMenuItem( index++, plan.Name );
			}
		}


		void UpdateMainMenu()
		{
			var menuItems = new List<MenuTreeNode>();

			// make sure File is the leftmost menu
			menuItems.Add( new MenuTreeNode( "File" ) );
			menuItems.Add( new MenuTreeNode( "File/SSH Gateway...", action: () => connectViaSSH() ) );

			menuItems.Add( new MenuTreeNode( "Plan/Select", action: () => this.selectPlanMenuItem_Click( null, null ) ) );

			//menuItems.Add( new MenuTreeNode( "Plan/Start", action: () => this.startPlanMenuItem_Click( null, null ) ) );
			//menuItems.Add( new MenuTreeNode( "Plan/Stop", action: () => this.stopPlanMenuItem_Click( null, null ) ) );
			//menuItems.Add( new MenuTreeNode( "Plan/Restart", action: () => this.restartPlanMenuItem_Click( null, null ) ) );
			//menuItems.Add( new MenuTreeNode( "Plan/Kill", action: () => this.killPlanMenuItem_Click( null, null ) ) );

			menuItems.Add( new MenuTreeNode( "Tools/Reload/Shared Config", action: () => this.reloadSharedConfigToolStripMenuItem_Click( null, null ) ) );
			menuItems.Add( new MenuTreeNode( "Tools/Kill/All running apps", action: () => this.killAllRunningAppsToolStripMenuItem_Click( null, null ) ) );
			menuItems.Add( new MenuTreeNode( "Tools/Kill/Agents on all computers", action: () => this.terminateAndKillAppsToolStripMenuItem_Click( null, null ) ) );
			menuItems.Add( new MenuTreeNode( "Tools/Power/Reboot All", action: () => this.rebootAllToolStripMenuItem1_Click( null, null ) ) );
			menuItems.Add( new MenuTreeNode( "Tools/Power/Shutdown All", action: () => this.shutdownAllToolStripMenuItem1_Click( null, null ) ) );
			menuItems.Add( new MenuTreeNode( "Tools/---FIRST" ) );


			// user-defined items
			foreach ( var item in _core.ReflStates.MenuItems )
			{
				// start the action on given host (or on local client if host is null)
				var hostClientId = _core.MachineId; // by default where this menu was clicked
				if( item is ActionDef actionDef )
				{
					if( !string.IsNullOrEmpty( actionDef.HostId ) )
					{
						hostClientId = actionDef.HostId;
					}
				}

				// set machine params of THIS machine
				var vars = new Dictionary<string,string>()
				{
					{ "MACHINE_ID", _core.MachineId },
					{ "MACHINE_IP", _core.ReflStates.MachineRegistry.GetMachineIP( _core.MachineId ) },
				};
				
				var menuItem = _menuBuilder.AssocMenuItemDefToMenuItem(item, (x) => WFT.GuardedOp( () => { Ctrl.Send( new Net.RunActionMessage( Ctrl.Name, x, hostClientId, vars )); } ) );

				menuItems.Add( menuItem );
			}

			// closure of the Files menu (before some used define menu items could be added)
			menuItems.Add( new MenuTreeNode( "File/---LAST1" ) );
			menuItems.Add( new MenuTreeNode( "File/Exit", action: () => this.exitToolStripMenuItem1_Click( null, null ) ) );

			// hardcoded help menu items should go last as the rightmost item (unless some user-defined Help menu items are specified)
			menuItems.Add( new MenuTreeNode( "Help/About", action: () => this.aboutMenuItem_Click( null, null ) ) );
			menuItems.Add( new MenuTreeNode( "Help/Online Documentation", action: () => this.onlineDocumentationToolStripMenuItem_Click( null, null ) ) );


			// merge menus into a single tree
			var combinedMenuTree = MenuTreeNode.CombineMenuItems( menuItems ); // just the children matter

			// convert to toolstrips
			var toolStrips = WFT.MenuItemsToToolStrips( combinedMenuTree.Children );

			// replace the main menu with a new one
			this.menuMain.Items.Clear();
			this.menuMain.Items.AddRange( toolStrips.ToArray() );

		}

	}
}
