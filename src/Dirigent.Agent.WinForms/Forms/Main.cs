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

		GuiCore _core;

		private MainAppsTab _tabApps;
		private MainPlansTab _tabPlans;
		private MainScriptsTab _tabScripts;
		private MainMachsTab _tabMachs;
		private MainFilesTab _tabFiles;

		private ContextMenuStrip mnuPlanList;  // context menu for the 'Open' toolbar button

		MenuBuilder _menuBuilder;

		/// <summary>
		/// The one menu builder of this window, shared with the tabs.
		/// </summary>
		/// <remarks>
		/// One, because the status bar listens to its OperationStarted: a tab that built its own got
		/// no progress bar and no cancel button for the very same action, which is how a download
		/// started from the Files tab used to run invisibly.
		/// </remarks>
		public MenuBuilder MenuBuilder => _menuBuilder;
		
		IDirig Ctrl => _core.Ctrl;

		public bool ShowJustAppFromCurrentPlan
		{
			get { return btnShowJustAppsFromCurrentPlan.Checked; }
			set	{ btnShowJustAppsFromCurrentPlan.Checked = value; }
		}

		public frmMain(
			AppConfig ac,
			NotifyIconHandler Handler,
			GuiCore core // Accept GuiCore instance instead of creating it
		)
		{
			_core = core; // Use the provided GuiCore instance
		
			_notifyIconHandler = Handler;

			InitializeComponent();

			if( Common.Properties.Settings.Default.GridRowSpacing > 0 ) 
			{
				this.gridPlans.RowTemplate.Height = Common.Properties.Settings.Default.GridRowSpacing;
				this.gridApps.RowTemplate.Height = Common.Properties.Settings.Default.GridRowSpacing;
			}


			HotKeysRegistrator.RegisterHotKeys( this.Handle );

			ShowJustAppFromCurrentPlan = Tools.BoolFromString( Common.Properties.Settings.Default.ShowJustAppsFromCurrentPlan );


			_core.ReflStates.OnActionsReceived += () => UpdateMainMenu(); // when Action arrived from master, we rebuild the menu


			// before the tabs: they share this instance, see the MenuBuilder property
			_menuBuilder = new MenuBuilder( _core );
			_menuBuilder.OperationStarted += ( instance, title ) => AddOperation( instance, title );

			_tabApps = new MainAppsTab( this, _core, gridApps );
			_tabPlans = new MainPlansTab( this, _core, gridPlans );
			_tabScripts = new MainScriptsTab( this, _core, gridScripts );
			_tabMachs = new MainMachsTab( this, _core, gridMachs );
			_tabFiles = new MainFilesTab( this, _core, gridFiles );

			// start ticking
			log.DebugFormat( "MainForm's timer period: {0}", ac.TickPeriod );
			tmrTick.Interval = ac.TickPeriod;
			tmrTick.Enabled = true;

			_core.Client.MessageReceived += OnMessage;

			UpdateMainMenu(); // initial menus

		}

		void myDispose()
		{
			_core.Client.MessageReceived -= OnMessage;

			tmrTick.Enabled = false;

			// Don't dispose _core here since it's managed by GuiTrayApp now
		}

		/// <summary>
		/// Queues a modal dialog to be shown once this message has been handled, instead of opening
		/// it inside the handler.
		/// </summary>
		/// <remarks>
		/// OnMessage runs on the client's message processing path, and MessageBox.Show is modal: it
		/// pumps its own loop and does not return until the user clicks. Opening one here stops this
		/// client from processing anything more, so every state that arrives meanwhile waits behind
		/// the dialog.
		///
		/// Nothing is lost - the transport is TCP and the messages queue up - but the delay is
		/// unbounded, and it lands in the worst possible place: DownloadZipped broadcasts its
		/// closing notification about 2 ms BEFORE the script's Finished state, so the box opens
		/// first and the Finished sits behind it. The progress indicator stays at whatever it last
		/// showed - 85%, mid-merge - for as long as the box is left open.
		/// </remarks>
		void ShowDialogAfterPumping( System.Windows.Forms.MethodInvoker show )
		{
			if( IsDisposed || !IsHandleCreated ) return;
			// fully qualified: this file also has "using System.Reflection", which since .NET 8
			// brings a second MethodInvoker into scope and makes the bare name ambiguous
			BeginInvoke( (System.Windows.Forms.MethodInvoker) ( () => WFT.GuardedOp( show ) ) );
		}

		void OnMessage( Net.Message msg )
		{
			switch( msg )
			{
				case Net.RemoteOperationErrorMessage m:
				{
					// deferred, see ShowDialogAfterPumping
					ShowDialogAfterPumping( () =>
						MessageBox.Show( m.Message, "Remote Operation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning ) );
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

						// deferred, see ShowDialogAfterPumping - this is the one that was holding
						// up the Finished state of the very script the box is reporting on
						ShowDialogAfterPumping( () =>
						{
							var dlgres = MessageBox.Show( m.Message, title, btns, icon );
							if( m.Action != null && dlgres == DialogResult.OK )
							{
								Ctrl.Send( new Net.RunActionMessage( Ctrl.Name, m.Action, Ctrl.Name, m.Attributes ) );
							}
						} );
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

		private void tmrTick_Tick( object sender, EventArgs e )
		{
			try
			{
				_core.Tick();
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
			if( IsConnected )
			{
				toolStripStatusLabel1.Text = "Connected.";

			}
			else
			{
				toolStripStatusLabel1.Text = "Disconnected.";
			}

			refreshOperations();
		}

		// ---- long operations in the status bar ---------------------------------------

		/// <summary>
		/// A script started from this GUI, shown while it runs: what it is, how far it has got, and a
		/// cross to stop it.
		/// </summary>
		class OperationSlot
		{
			public Guid Instance;
			public string Title = string.Empty;
			public ToolStripStatusLabel Label = null!;
			public ToolStripProgressBar Bar = null!;
			public ToolStripButton Cancel = null!;
			public bool Cancelling;
			public bool Failed;

			/// <summary>
			/// Whether a state for this instance has ever been seen. Until it has, a missing state
			/// means "the start has not been answered yet", not "the script is over". The two are
			/// indistinguishable from the state cache alone, and AddOperation refreshes
			/// synchronously - so without this every slot was removed in the same breath as it was
			/// created and no bar was ever drawn.
			/// </summary>
			public bool SeenState;

			/// <summary>When the operation was started, for the case where nothing ever answers.</summary>
			public DateTime StartedAt = DateTime.Now;
		}

		/// <summary>
		/// How long to wait for the first state of an operation before giving up on it.
		/// </summary>
		/// <remarks>
		/// A start that is never answered - the host is gone, the script name is wrong - produces no
		/// state at all, so a slot waiting for its first one would sit there for ever with nothing
		/// able to clear it. It becomes a failed slot instead, which the user can dismiss.
		/// </remarks>
		static readonly TimeSpan _startTimeout = TimeSpan.FromSeconds( 15 );

		/// <summary>
		/// How many operations get a slot of their own before the rest are only counted. Two fit
		/// beside the connection label on a default sized window.
		/// </summary>
		const int _maxOperationSlots = 2;

		/// <summary>Only the operations this GUI started - the ones the person here asked for.</summary>
		readonly Dictionary<Guid, OperationSlot> _operations = new();

		ToolStripStatusLabel? _moreOperationsLabel;

		void AddOperation( Guid instance, string title )
		{
			// a tool action has no script to follow
			if( instance == Guid.Empty || _operations.ContainsKey( instance ) ) return;

			var slot = new OperationSlot()
			{
				Instance = instance,
				Title = string.IsNullOrEmpty( title ) ? "Working" : LastSegmentOf( title ),
			};

			// Fixed width, NOT auto-sized. A status text such as
			//   "Collecting from 1 machine(s) - 5GB of 5GB - BScene-3901_2026-05-13_14-12-24.log"
			// is ~80 characters, and with the operation title in front the label grows past the
			// width of the window. A StatusStrip is a single row, so an oversized item sends the
			// whole slot - label, bar AND cancel button - into the ToolStrip's hidden overflow, and
			// the operation looks as though it had disappeared while it is in fact still running.
			// The detail belongs in the tooltip, which is what AutoToolTip is for.
			slot.Label = new ToolStripStatusLabel()
			{
				AutoToolTip = true,
				AutoSize = false,
				Width = 170,
			};

			slot.Bar = new ToolStripProgressBar()
			{
				Size = new System.Drawing.Size( 100, 14 ),
				Style = ProgressBarStyle.Marquee,  // until it says how far it has got
				MarqueeAnimationSpeed = 30,
			};

			slot.Cancel = new ToolStripButton()
			{
				Text = "✕",
				DisplayStyle = ToolStripItemDisplayStyle.Text,
				ForeColor = System.Drawing.Color.Firebrick,
				ToolTipText = "Cancel this operation",
				Alignment = ToolStripItemAlignment.Left,
			};
			slot.Cancel.Click += ( s, e ) => CancelOrDismiss( slot );

			_operations[instance] = slot;

			// the slots that do not fit are only counted, so the strip cannot overflow its window
			if( _operations.Count <= _maxOperationSlots )
			{
				statusStrip.Items.Add( slot.Label );
				statusStrip.Items.Add( slot.Bar );
				statusStrip.Items.Add( slot.Cancel );
			}

			refreshOperations();
		}

		/// <summary>
		/// Turns a slot into a failed one: it stays until the user clicks it away, so that an
		/// operation cannot fail unnoticed while they are looking elsewhere.
		/// </summary>
		void MarkFailed( OperationSlot slot, string reason )
		{
			slot.Failed = true;
			slot.Cancelling = false;
			slot.Cancel.Enabled = true;
			slot.Cancel.ToolTipText = "Dismiss";
			slot.Label.ForeColor = System.Drawing.Color.Firebrick;
			slot.Label.Text = $"{slot.Title}: failed";
			slot.Label.ToolTipText = reason;
			slot.Bar.Style = ProgressBarStyle.Continuous;
			slot.Bar.Value = slot.Bar.Maximum;
		}

		void CancelOrDismiss( OperationSlot slot )
		{
			// a failed operation stays until it is clicked away; the cross then only dismisses it
			if( slot.Failed )
			{
				RemoveOperation( slot );
				return;
			}

			slot.Cancelling = true;
			slot.Cancel.Enabled = false;
			slot.Label.Text = $"{slot.Title}: cancelling...";
			slot.Bar.Style = ProgressBarStyle.Marquee;

			WFT.GuardedOp( () => Ctrl.Send( new Net.KillScriptMessage( Ctrl.Name, slot.Instance ) ) );
		}

		void RemoveOperation( OperationSlot slot )
		{
			statusStrip.Items.Remove( slot.Label );
			statusStrip.Items.Remove( slot.Bar );
			statusStrip.Items.Remove( slot.Cancel );

			slot.Label.Dispose();
			slot.Bar.Dispose();
			slot.Cancel.Dispose();

			_operations.Remove( slot.Instance );

			// a slot may have come free for one that was only being counted
			foreach( var waiting in _operations.Values )
			{
				if( statusStrip.Items.Contains( waiting.Label ) ) continue;
				if( VisibleOperationCount() >= _maxOperationSlots ) break;

				statusStrip.Items.Add( waiting.Label );
				statusStrip.Items.Add( waiting.Bar );
				statusStrip.Items.Add( waiting.Cancel );
			}
		}

		int VisibleOperationCount()
			=> _operations.Values.Count( x => statusStrip.Items.Contains( x.Label ) );

		/// <summary>
		/// Follows the operations this GUI started. Runs on the same tick that pumps the client, so the
		/// states it reads have just arrived and no marshalling is needed.
		/// </summary>
		void refreshOperations()
		{
			foreach( var slot in _operations.Values.ToList() )
			{
				var state = _core.ReflStates.GetScriptState( slot.Instance );

				// a state that has disappeared means the script is long gone
				// a state that has disappeared means the script is long gone - but only once one
				// has actually been seen, see OperationSlot.SeenState
				if( state is null )
				{
					if( slot.SeenState && !slot.Failed ) RemoveOperation( slot );

					// nothing ever answered the start - say so rather than spin for ever
					else if( !slot.SeenState && !slot.Failed && DateTime.Now - slot.StartedAt > _startTimeout )
						MarkFailed( slot, "no answer - is the machine hosting the script connected?" );

					continue;
				}
				slot.SeenState = true;

				switch( state.Status )
				{
					case EScriptStatus.Failed:
						MarkFailed( slot, state.Text ?? "failed" );
						break;

					case EScriptStatus.Finished:
					case EScriptStatus.Cancelled:
						RemoveOperation( slot );
						break;

					default:
						// keep the label short so the bar and the cross keep their place; the
						// detail (machine, file, bytes) goes to the tooltip. A percentage is
						// worth the few characters - readable at a glance on a 100 px bar.
						var pct = state.Progress is double pr && !slot.Cancelling
									? $" {(int) ( pr * 100 )}%"
									: string.Empty;
						slot.Label.Text = slot.Cancelling
											? $"{slot.Title}: cancelling..."
											: $"{slot.Title}{pct}";
						slot.Label.ToolTipText = state.Text ?? slot.Title;

						if( state.Progress is double progress && !slot.Cancelling )
						{
							slot.Bar.Style = ProgressBarStyle.Continuous;
							slot.Bar.Value = Math.Clamp( (int) ( progress * slot.Bar.Maximum ), 0, slot.Bar.Maximum );
						}
						else
						{
							// running, but with nothing to say about how far - do not invent a number
							slot.Bar.Style = ProgressBarStyle.Marquee;
						}
						break;
				}
			}

			RefreshMoreOperationsLabel();
		}

		void RefreshMoreOperationsLabel()
		{
			int hidden = _operations.Count - VisibleOperationCount();

			if( hidden <= 0 )
			{
				if( _moreOperationsLabel is not null )
				{
					statusStrip.Items.Remove( _moreOperationsLabel );
					_moreOperationsLabel.Dispose();
					_moreOperationsLabel = null;
				}
				return;
			}

			if( _moreOperationsLabel is null )
			{
				_moreOperationsLabel = new ToolStripStatusLabel() { AutoToolTip = true };
				statusStrip.Items.Add( _moreOperationsLabel );
			}

			_moreOperationsLabel.Text = $"+{hidden} more";
			_moreOperationsLabel.ToolTipText = string.Join( "\n",
				_operations.Values.Where( x => !statusStrip.Items.Contains( x.Label ) ).Select( x => x.Title ) );
		}

		/// <summary>An action title is a menu path; the last segment is what names the operation.</summary>
		static string LastSegmentOf( string title )
			=> title.Split( new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries ).LastOrDefault() ?? title;

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
			_tabFiles.Refresh();
			refreshStatusBar();
			refreshMenu();
			setTitle();
			EnableDisableButtons();
		}


		void EnableDisableButtons()
		{
			// disable start/restart buttons if KillAll is in progress
			bool startOpsEnabled = !_core.KillAllInProgress;
			btnStartPlan.Enabled = startOpsEnabled;
			btnStopPlan.Enabled = startOpsEnabled;
			//btnKillPlan.Enabled = startOpsEnabled; // kill is always enabled, does not interfere with KillAll
			btnRestartPlan.Enabled = startOpsEnabled;
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
					List<PlanDef> plans = new List<PlanDef>( Ctrl.GetAllPlansDef() );
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
			var allAppState = Ctrl.GetAllAppsState();
			bool someAppsRunning = allAppState.Any( x => x.Value.Running || x.Value.Dying );
			if( someAppsRunning )
			{
				MessageBox.Show( "Some apps are still running. Please kill them first.", "Dirigent", MessageBoxButtons.OK, MessageBoxIcon.Warning );
				return;
			}

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
				};

				var machineIp = _core.ReflStates.FileReg.GetMachineIP( _core.MachineId );
				if (!string.IsNullOrEmpty(machineIp))
				{
					vars.Add("MACHINE_IP", machineIp);
				}				

				var menuItem = _menuBuilder.AssocMenuItemDefToMenuItem(item, (x) => WFT.GuardedOp( () =>
					{
						// an action here may be hosted by another client, so the note travels with the
						// message rather than being handed over directly
						if( !_menuBuilder.TryGetComment( x, null, out var comment ) ) return;

						Ctrl.Send( new Net.RunActionMessage( Ctrl.Name, x, hostClientId, vars, comment ) );
					} ) );

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