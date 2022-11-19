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

		private NotifyIcon _notifyIcon;
		public bool AllowLocalIfDisconnected { get; private set; }
		private AppConfig _ac;

		public System.ComponentModel.IContainer Components => components;

		public IDirig Ctrl { get; private set; }

		private string _machineId; // empty if GUI not running as part of local agent
		private Net.ClientIdent _clientIdent; // name of the network client; messages are marked with that

		public List<PlanDef> PlanRepo { get; private set; }

		public List<ScriptDef> ScriptRepo { get; private set; }

		public Net.Client Client { get; private set; }

		public ReflectedStateRepo ReflStates { get; private set; }

		public PlanDef CurrentPlan { get; set; }

		private MainAppsTab _tabApps;
		private MainPlansTab _tabPlans;
		private MainScriptsTab _tabScripts;
		private MainMachsTab _tabMachs;
		private MainFilesTab _tabFiles;

		ContextMenuStrip mnuPlanList;  // context menu for the 'Open' toolbar button

		public bool ShowJustAppFromCurrentPlan
		{
			get { return btnShowJustAppsFromCurrentPlan.Checked; }
			set	{ btnShowJustAppsFromCurrentPlan.Checked = value; }
		}

		public frmMain(
			AppConfig ac,
			NotifyIcon notifyIcon,
			string machineId // empty if no local agent was started with the GUI
		)
		{
			_ac = ac;
			_machineId = machineId; // FIXME: this is only valid if we are running a local agent! How do we know??
			_clientIdent = new Net.ClientIdent() { Sender = Guid.NewGuid().ToString(), SubscribedTo = Net.EMsgRecipCateg.Gui };
			_notifyIcon = notifyIcon;
			AllowLocalIfDisconnected = true;

			log.Debug( $"Running with masterIp={_ac.MasterIP}, masterPort={_ac.MasterPort}" );

			InitializeComponent();

			if( Common.Properties.Settings.Default.GridRowSpacing > 0 ) 
			{
				this.gridPlans.RowTemplate.Height = Common.Properties.Settings.Default.GridRowSpacing;
				this.gridApps.RowTemplate.Height = Common.Properties.Settings.Default.GridRowSpacing;
			}


			registerHotKeys();

			ShowJustAppFromCurrentPlan = Tools.BoolFromString( Common.Properties.Settings.Default.ShowJustAppsFromCurrentPlan );

			PlanRepo = new List<PlanDef>();
			ScriptRepo = new List<ScriptDef>();

			Client = new Net.Client( _clientIdent, ac.MasterIP, ac.MasterPort, autoConn: true );
			Client.MessageReceived += OnMessage;
			ReflStates = new ReflectedStateRepo( Client );
			
			bool firstGotPlans = true;
			ReflStates.OnPlansReceived += () =>
			{
				if( firstGotPlans )
				{
					selectPlan( ac.StartupPlan );
				}
				firstGotPlans = false;

				// udate current plan reference in case the plan def has changed
				if( CurrentPlan is not null )
				{
					CurrentPlan = ReflStates.GetPlanDef( CurrentPlan.Name );
				}
			};


			ReflStates.OnScriptsReceived += () =>
			{
			};

			Ctrl = ReflStates;

			_tabApps = new MainAppsTab( this, gridApps );
			_tabPlans = new MainPlansTab( this, gridPlans );
			_tabScripts = new MainScriptsTab( this, gridScripts );
			_tabMachs = new MainMachsTab( this, gridMachs );
			_tabFiles = new MainFilesTab( this, gridFiles );

			// start ticking
			log.DebugFormat( "MainForm's timer period: {0}", ac.TickPeriod );
			tmrTick.Interval = ac.TickPeriod;
			tmrTick.Enabled = true;

		}

		void myDispose()
		{
			tmrTick.Enabled = false;
			if( Client is not null )
			{
				Client.MessageReceived -= OnMessage;
				Client.Dispose();
				Client = null;
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
			}
		}


		const int HOTKEY_ID_START_CURRENT_PLAN = 1;
		const int HOTKEY_ID_KILL_CURRENT_PLAN = 2;
		const int HOTKEY_ID_RESTART_CURRENT_PLAN = 3;
		const int HOTKEY_ID_SELECT_PLAN_0 = 4; 
		const int HOTKEY_ID_SELECT_PLAN_1 = HOTKEY_ID_SELECT_PLAN_0 + 1;
		const int HOTKEY_ID_SELECT_PLAN_2 = HOTKEY_ID_SELECT_PLAN_0 + 2;
		const int HOTKEY_ID_SELECT_PLAN_3 = HOTKEY_ID_SELECT_PLAN_0 + 3;
		const int HOTKEY_ID_SELECT_PLAN_4 = HOTKEY_ID_SELECT_PLAN_0 + 4;
		const int HOTKEY_ID_SELECT_PLAN_5 = HOTKEY_ID_SELECT_PLAN_0 + 5;
		const int HOTKEY_ID_SELECT_PLAN_6 = HOTKEY_ID_SELECT_PLAN_0 + 6;
		const int HOTKEY_ID_SELECT_PLAN_7 = HOTKEY_ID_SELECT_PLAN_0 + 7;
		const int HOTKEY_ID_SELECT_PLAN_8 = HOTKEY_ID_SELECT_PLAN_0 + 8;
		const int HOTKEY_ID_SELECT_PLAN_9 = HOTKEY_ID_SELECT_PLAN_0 + 9;

		// DLL libraries used to manage hotkeys
		[DllImport( "user32.dll" )]
		public static extern bool RegisterHotKey( IntPtr hWnd, int id, int fsModifiers, int vlc );
		[DllImport( "user32.dll" )]
		public static extern bool UnregisterHotKey( IntPtr hWnd, int id );

		void registerHotKeys()
		{
			var exeConfigFileName = System.Reflection.Assembly.GetEntryAssembly().Location + ".config";
			XDocument document = XDocument.Load( exeConfigFileName );
			var templ = "/configuration/userSettings/Dirigent.Common.Properties.Settings/setting[@name='{0}']/value";
			{
				var x = document.XPathSelectElement( String.Format( templ, "StartPlanHotKey" ) );
				string hotKeyStr = ( x != null ) ? x.Value : "Control + Shift + Alt + S";
				if( !String.IsNullOrEmpty( hotKeyStr ) )
				{
					var key = ( HotKeys.Keys )HotKeys.HotKeyShared.ParseShortcut( hotKeyStr ).GetValue( 1 );
					var modifier = ( HotKeys.Modifiers )HotKeys.HotKeyShared.ParseShortcut( hotKeyStr ).GetValue( 0 );
					RegisterHotKey( this.Handle, HOTKEY_ID_START_CURRENT_PLAN, ( int )modifier, ( int )key );
				}
			}
			{
				var x = document.XPathSelectElement( String.Format( templ, "KillPlanPlanHotKey" ) );
				string hotKeyStr = ( x != null ) ? x.Value : "Control + Shift + Alt + K";
				if( !String.IsNullOrEmpty( hotKeyStr ) )
				{
					var key = ( HotKeys.Keys )HotKeys.HotKeyShared.ParseShortcut( hotKeyStr ).GetValue( 1 );
					var modifier = ( HotKeys.Modifiers )HotKeys.HotKeyShared.ParseShortcut( hotKeyStr ).GetValue( 0 );
					RegisterHotKey( this.Handle, HOTKEY_ID_KILL_CURRENT_PLAN, ( int )modifier, ( int )key );
				}
			}

			{
				var x = document.XPathSelectElement( String.Format( templ, "RestartPlanPlanHotKey" ) );
				string hotKeyStr = ( x != null ) ? x.Value : "Control + Shift + Alt + R";
				if( !String.IsNullOrEmpty( hotKeyStr ) )
				{
					var key = ( HotKeys.Keys )HotKeys.HotKeyShared.ParseShortcut( hotKeyStr ).GetValue( 1 );
					var modifier = ( HotKeys.Modifiers )HotKeys.HotKeyShared.ParseShortcut( hotKeyStr ).GetValue( 0 );
					RegisterHotKey( this.Handle, HOTKEY_ID_RESTART_CURRENT_PLAN, ( int )modifier, ( int )key );
				}
			}

			for( int i = 0; i <= 9; i++ )
			{
				var x = document.XPathSelectElement( String.Format( templ, String.Format( "SelectPlan{0}HotKey", i ) ) );
				string hotKeyStr = ( x != null ) ? x.Value : String.Format( "Control + Shift + Alt + {0}", i );
				if( !String.IsNullOrEmpty( hotKeyStr ) )
				{
					var key = ( HotKeys.Keys )HotKeys.HotKeyShared.ParseShortcut( hotKeyStr ).GetValue( 1 );
					var modifier = ( HotKeys.Modifiers )HotKeys.HotKeyShared.ParseShortcut( hotKeyStr ).GetValue( 0 );
					RegisterHotKey( this.Handle, HOTKEY_ID_SELECT_PLAN_0 + i, ( int )modifier, ( int )key );
				}
			}

			//var hk = HotKeys.HotKeyShared.CombineShortcut(HotKeys.Modifiers.Control | HotKeys.Modifiers.Alt | HotKeys.Modifiers.Shift, HotKeys.Keys.B);

			//string shortcut = "Shift + Alt + H";
			//Keys Key = (Keys)HotKeys.HotKeyShared.ParseShortcut(shortcut).GetValue(1);
			//HotKeys.Modifiers Modifier = (HotKeys.Modifiers)HotKeys.HotKeyShared.ParseShortcut(shortcut).GetValue(0);


			//if (hotKeysEnabled)
			//{

			//	// Modifier keys codes: Alt = 1, Ctrl = 2, Shift = 4, Win = 8
			//	// Compute the addition of each combination of the keys you want to be pressed
			//	// ALT+CTRL = 1 + 2 = 3 , CTRL+SHIFT = 2 + 4 = 6...
			//	RegisterHotKey(this.Handle, HOTKEY_ID_START_CURRENT_PLAN, 1+2+4, (int)Keys.R); // CTRL+SHIFT+ALT+R
			//	RegisterHotKey(this.Handle, HOTKEY_ID_KILL_CURRENT_PLAN, 1 + 2 + 4, (int)Keys.K); // CTRL+SHIFT+ALT+K
			//}
		}

		void setTitle()
		{
			string planName = "<no plan>";

			var currPlan = CurrentPlan;
			if( currPlan != null )
			{
				planName = currPlan.Name;
			}

			this.Text = string.Format( "Dirigent [{0}] - {1}", _machineId, planName );
			if( this._notifyIcon != null )
			{
				this._notifyIcon.Text = string.Format( "Dirigent [{0}] - {1}", _machineId, planName );
			}
		}


		private void handleOperationError( Exception ex )
		{
			this._notifyIcon.ShowBalloonTip( 5000, "Dirigent Operation Error", ex.Message, ToolTipIcon.Error );
			log.ErrorFormat( "Exception: {0}\n{1}", ex.Message, ex.StackTrace );
		}

		private void tmrTick_Tick( object sender, EventArgs e )
		{
			try
			{
				Client.Tick();
			}
			catch( RemoteOperationErrorException ex ) // operation exception (not necesarily remote, could be also local
				// as all operational requests always go through the network if
				// connected to master
			{
				// if this GUI was the requestor of the operation that failed
				if( ex.Requestor == _clientIdent.Sender )
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

		public bool IsConnected => Client.IsConnected;

		void refreshStatusBar()
		{
			if( IsConnected )
			{
				toolStripStatusLabel1.Text = "Connected.";

			}
			else
			{
				toolStripStatusLabel1.Text = "Disconnected.";
			}

		}

		void refreshMenu()
		{
			bool isConnected = IsConnected;
			bool hasPlan = CurrentPlan != null;
			planToolStripMenuItem.Enabled = isConnected || AllowLocalIfDisconnected;
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
			_tabFiles.Refresh();
			refreshStatusBar();
			refreshMenu();
			setTitle();
		}

		void selectPlan( string planName )
		{
			CurrentPlan = Ctrl.GetPlanDef( planName );
			Ctrl.Send( new Net.SelectPlanMessage( Ctrl.Name, CurrentPlan is null ? string.Empty : CurrentPlan.Name ) );
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

		protected override void WndProc( ref Message m )
		{
			if( m.Msg == 0x0312 )
			{
				var keyId = m.WParam.ToInt32();
				switch( keyId )
				{
					case HOTKEY_ID_START_CURRENT_PLAN:
					{
						var currPlan = CurrentPlan;
						if( currPlan != null )
						{
							Ctrl.Send( new Net.StartPlanMessage( Ctrl.Name, currPlan.Name ) );
						}
						break;
					}

					case HOTKEY_ID_KILL_CURRENT_PLAN:
					{
						var currPlan = CurrentPlan;
						if( currPlan != null )
						{
							Ctrl.Send( new Net.KillPlanMessage( Ctrl.Name, currPlan.Name ) );
						}
						break;
					}

					case HOTKEY_ID_RESTART_CURRENT_PLAN:
					{
						var currPlan = CurrentPlan;
						if( currPlan != null )
						{
							Ctrl.Send( new Net.RestartPlanMessage( Ctrl.Name, currPlan.Name ) );
						}
						break;
					}


					case HOTKEY_ID_SELECT_PLAN_0:
					{
						selectPlan( null );
						break;
					}

					case HOTKEY_ID_SELECT_PLAN_1:
					case HOTKEY_ID_SELECT_PLAN_2:
					case HOTKEY_ID_SELECT_PLAN_3:
					case HOTKEY_ID_SELECT_PLAN_4:
					case HOTKEY_ID_SELECT_PLAN_5:
					case HOTKEY_ID_SELECT_PLAN_6:
					case HOTKEY_ID_SELECT_PLAN_7:
					case HOTKEY_ID_SELECT_PLAN_8:
					case HOTKEY_ID_SELECT_PLAN_9:
					{
						int i = keyId - HOTKEY_ID_SELECT_PLAN_1; // zero-based index of plan
						List<PlanDef> plans = new List<PlanDef>( Ctrl.GetAllPlanDefs() );
						if( i < plans.Count )
						{
							var planName = plans[i].Name;
							this._notifyIcon.ShowBalloonTip( 1000, String.Format( "{0}", planName ), " ", ToolTipIcon.Info );
							selectPlan( planName );
						}
						break;
					}
				}
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
				Ctrl.Send( new Net.ShutdownMessage( Ctrl.Name, args ) );
			}
		}

		private void shutdownAllToolStripMenuItem1_Click( object sender, EventArgs e )
		{
			if( MessageBox.Show( "Shut down all computers where Dirigent is running?", "Dirigent", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning ) == DialogResult.OK )
			{
				var args = new ShutdownArgs() { Mode = EShutdownMode.PowerOff };
				Ctrl.Send( new Net.ShutdownMessage( Ctrl.Name, args ) );
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
				"Dirigent app launcher\nby pjanec\nMIT license\n\nver." + version + "\n\n" + verStampText,
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
			if( CurrentPlan is null )
			{
				ShowNoPlanSelectedError();
				return;
			}
			WFT.GuardedOp( () => Ctrl.Send( new Net.StartPlanMessage( Ctrl.Name, CurrentPlan.Name ) ) );
		}

		private void stopPlanMenuItem_Click( object sender, EventArgs e )
		{
			if( CurrentPlan is null )
			{
				ShowNoPlanSelectedError();
				return;
			}
			WFT.GuardedOp( () => Ctrl.Send( new Net.StopPlanMessage( Ctrl.Name, CurrentPlan.Name ) ) );
		}

		private void killPlanMenuItem_Click( object sender, EventArgs e )
		{
			if( CurrentPlan is null )
			{
				ShowNoPlanSelectedError();
				return;
			}
			WFT.GuardedOp( () => Ctrl.Send( new Net.KillPlanMessage( Ctrl.Name, CurrentPlan.Name ) ) );
		}

		private void restartPlanMenuItem_Click( object sender, EventArgs e )
		{
			if( CurrentPlan is null )
			{
				ShowNoPlanSelectedError();
				return;
			}
			WFT.GuardedOp( () => Ctrl.Send( new Net.RestartPlanMessage( Ctrl.Name, CurrentPlan.Name ) ) );
		}

		private void selectPlanMenuItem_Click( object sender, EventArgs e )
		{
			//selectPlanToolStripMenuItem.ShowDropDown();
			if( mnuPlanList is not null )
			{
				mnuPlanList.Show( this, this.PointToClient( Cursor.Position ) );
			}
		}

		void addPlanSelectionMenuItem( int index, string planName )
		{
			EventHandler clickHandler = ( sender, args ) => WFT.GuardedOp( () => { selectPlan( planName); } );

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

			foreach( var plan in PlanRepo )
			{
				addPlanSelectionMenuItem( index++, plan.Name );
			}
		}


	}
}
