using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Threading;

namespace Dirigent.Gui.WinForms
{
	public class GuiTrayApp : Disposable, IApp
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

		private AppConfig _ac;
		private frmMain _mainForm;
		private NotifyIcon _notifyIcon;
		private NotifyIconHandler _notifyIconHandler;
        //private ProcRunner _agentRunner;
		private Agent _agent;
		private Thread _agentThread;
		private Master _master;
		private Thread _masterThread;
		private bool _runGui;
		private bool _runAgent;
		private bool _isMaster;
		private string _machineId; // empty if GUI not running as part of local agent
		private AlreadyRunningTester _alreadyRunningTester;
        private ProcRunner _guiRunner;
		private string _guiClientId = Guid.NewGuid().ToString();

		class MyApplicationContext : ApplicationContext
		{
		}

		public GuiTrayApp( AppConfig ac, bool runAgent, bool runGui, bool isMaster )
		{
			_ac = ac;
			_runAgent = runAgent;
			_runGui = runGui;
			_isMaster = isMaster;

			_machineId = _runAgent ? _ac.MachineId : string.Empty;

			_alreadyRunningTester = new AlreadyRunningTester( ac.MasterIP, ac.MasterPort, ac.MachineId );
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if( !disposing ) return;

			_guiRunner?.Dispose();
			_guiRunner = null;

			DeinitializeMainForm();
			DeinitializeTrayIcon();
			DeinitializeAgent();
			DeinitializeMaster();

			AppMessenger.Instance.Dispose();
		}

		public EAppExitCode run()
		{
			var exitCode = EAppExitCode.NoError;

			if( _runAgent )
			{
				InitializeAgent();
			}

			if( _isMaster )
			{
				InitializeMaster();
			}

			//try
			{
				if( _runGui )
				{
					// listen to AppExit messages
					AppMessenger.Instance.Register<Dirigent.AppMessages.ExitApp>( ( x ) => ExitApp() );
					//AppMessenger.Instance.Register<AppMessages.CheckSharedConfigAndRestartMaster>( (x) => CheckSharedConfigAndRestartMaster() );

					Application.EnableVisualStyles();
					Application.SetCompatibleTextRenderingDefault( false );

					InitializeTrayIcon();
					InitializeMainForm();

					// show the form if it should not stay hidden
					if( !Tools.BoolFromString( _ac.StartHidden ) )
					{
						ShowGUI();
					}

				}
				Application.Run( new MyApplicationContext() );
			}
			//catch( Exception ex )
			//{
			//	log.Error( ex );
			//	ExceptionDialog.showExceptionWithStackTrace( ex, "Exception", "" );
			//	exitCode = EAppExitCode.ExceptionError;
			//}

			return exitCode;
		}

		void InitializeTrayIcon()
		{
			var components = new System.ComponentModel.Container();

			_notifyIcon = new NotifyIcon( components );
			_notifyIconHandler = new NotifyIconHandler( _notifyIcon );

			if ( !string.IsNullOrEmpty(_machineId) )
			{
				_notifyIcon.Text = $"Dirigent [{_machineId}]";
			}
			else
			{
				_notifyIcon.Text = $"Dirigent";
			}

			_notifyIcon.Icon = Properties.Resources.AppIcon;

			var menuItems = new List<ToolStripMenuItem>();
			if( _runGui )
			{
				menuItems.Add( new ToolStripMenuItem( "Show", null, new EventHandler( ( s, e ) => ShowGUI() ) ) );
			}
    //        if( _runAgent )
    //        {
				//const string BaseText = "Agent's Console";
    //            menuItems.Add( new ToolStripMenuItem(BaseText, null, new EventHandler( (s,e) =>
    //            {
    //                ToolStripMenuItem mi = s as ToolStripMenuItem;
					
				//	// try to re-run the agent if not running from whatever reson
				//	if( _agentRunner == null )
				//	{
				//		InitializeAgent();
				//	}
    //                if( _agentRunner is { IsRunning: false } )
				//	{
				//		DeinitializeAgent();
				//		InitializeAgent();
				//	}

				//	// 
				//	//bool isRunning = _agentRunner != null && _agentRunner.IsRunning;
				//	//mi.Text = isRunning ? BaseText : BaseText + " (not running)";

    //                if( _agentRunner != null )
    //                {
    //                    _agentRunner.IsConsoleShown = !mi.Checked;
    //                    mi.Checked = _agentRunner.IsConsoleShown;
    //                }
    //            }
    //            )) );
    //        }
			menuItems.Add( new ToolStripMenuItem( "Exit", null, new EventHandler( ( s, e ) =>
			{
				//agent.LocalOps.Terminate( new TerminateArgs() { KillApps=true, MachineId=ac.machineId }  );
				AppMessenger.Instance.Send( new Dirigent.AppMessages.ExitApp() ); // handled in GuiApp
			} ) ) );

			//menuItems
			var cms = new ContextMenuStrip();
			foreach( var x in menuItems ) cms.Items.Add( x );
			_notifyIcon.ContextMenuStrip = cms;
			_notifyIcon.Visible = true;
			_notifyIcon.DoubleClick += new EventHandler( ( s, e ) => {
				if( _runGui )
				{
					ShowGUI();
				}
			});
		}

		void DeinitializeTrayIcon()
		{
			// We must manually tidy up and remove the icon before we exit.
			// Otherwise it will be left behind until the user mouses over.
			if( _notifyIcon != null )
			{
				_notifyIcon.Visible = false;
			}
		}

		void InitializeMainForm()
		{
			// nothing needed, will be initialized when first time opened
		}

		void DeinitializeMainForm()
		{
			if( _mainForm != null )
			{
				// save main form's location and size
				if( ( Control.ModifierKeys & Keys.Shift ) == 0 )
				{
					_mainForm.SaveWindowSettings( "MainFormLocation" );
				}

				_mainForm.Close();
				_mainForm.Dispose();
				_mainForm = null;
			}

		}

		void CreateMainForm()
		{
			_mainForm = new frmMain(
				_ac,
				_notifyIconHandler,
				_machineId,
				PathUtils.GetRootForRelativePaths( _ac.SharedCfgFileName, _ac.RootForRelativePaths )
			);

			// restore saved location if SHIFT not held
			if( ( Control.ModifierKeys & Keys.Shift ) == 0 )
			{
				string initLocation = Common.Properties.Settings.Default.MainFormLocation;

				_mainForm.RestoreWindowSettings( initLocation );
			}
			else  // for default I just want the form to start in the top-left corner.
			{
				Point topLeftCorner = new Point( 0, 0 );
				_mainForm.Location = topLeftCorner;
			}

			//AppMessenger.Instance.Register<AppMessages.OnClose>( (x) =>
			//{
			//});
		}

		void ShowMainForm()
		{
			if( _mainForm == null || _mainForm.IsDisposed )
			{
				CreateMainForm();
			}

			// If we are already showing the window, merely focus it.
			if( _mainForm.Visible )
			{
				_mainForm.Activate();
			}
			else
			{
				_mainForm.Show();
				_mainForm.WindowState = FormWindowState.Normal;
			}

		}

		void ShowGUI()
		{
			if( string.IsNullOrEmpty( _ac.GuiAppExe ) )
			{
				// show default GUI built in this app
				ShowMainForm();
			}
			else // run external process
			{
				try
				{
					if( _guiRunner == null )
					{
						var optionReplTab = new Dictionary<string, string>
						{
							["--clientId"] = _guiClientId
						};

						_guiRunner = new ProcRunner( _ac.GuiAppExe, optionReplTab, true );
						_guiRunner.Launch();
					}
					else // was already started
					{
						// re-launch if no longer running
						if( !_guiRunner.IsRunning )
						{
							_guiRunner.Launch();
						}
						else // just tell it to unhide
						{
							_guiRunner.IsShown = true;
						}
					}
				}
				catch ( Exception ex )
				{
					log.Error( ex );
					ExceptionDialog.showExceptionWithStackTrace( ex, "Exception", "" );
				}
			}
		}

        private void InitializeAgent()
        {
			//_agentRunner = new ProcRunner( "Dirigent.Agent.exe", "agent",
			//	killOnDispose:_runGui ); // kill agent on GUi app dispose only if the gui will keep runnin (otherwise we are just the launcher of an agent and terminate immediately)
			//try
			//{
			//	if( _ac.ParentPid == -1 )
			//		_agentRunner.Launch();
			//	else
			//		_agentRunner.Adopt( _ac.ParentPid );

			//	_agentRunner.StartKeepAlive();
			//}
			//catch (Exception ex)
			//{
			//	log.Error(ex);
			//	ExceptionDialog.showExceptionWidthStackTrac(ex, "Exception", "");
			//	_agentRunner = null;
			//}

			if( !_alreadyRunningTester.IsAgentAlreadyRunning() )
			{
				// istantiate the agent and tick it in its own thread
				try
				{
					_agent = new Agent(
						_ac.MachineId,
						_ac.MasterIP,
						_ac.MasterPort,
						PathUtils.GetRootForRelativePaths( _ac.SharedCfgFileName, _ac.RootForRelativePaths ),
						_ac.LocalCfgFileName
					);

					_agentThread = new Thread(() =>
					{
						while( !_agent.WantsQuit )
						{
							_agent.Tick();
							Thread.Sleep( _ac.TickPeriod );
						}
						_agent.Dispose();
						_agent = null;
					});
					_agentThread.Start();
				}
				catch (Exception ex)
				{
					log.Error(ex);
					ExceptionDialog.showExceptionWithStackTrace(ex, "Exception", "");
					_agent = null;
				}
			}
			else
			{
				log.Error( "Another instance of Dirigent Agent is already running!" );
				MessageBox.Show("Another instance of Dirigent Agent is already running on this machine!", "Dirigent", MessageBoxButtons.OK, MessageBoxIcon.Warning);
			}
        }

        private void DeinitializeAgent()
        {
			if( _agentThread != null )
			{
				_agent.WantsQuit = true;
				_agentThread.Join( 4*_ac.TickPeriod );
			}

			_agent?.Dispose();
			_agent = null;
		}

		private void InitializeMaster()
		{
			if( _isMaster )
			{
				if( !_alreadyRunningTester.IsMasterAlreadyRunning() )
				{
					if( string.IsNullOrEmpty( _ac.SharedCfgFileName ) )
					{
						var ex = new ConfigurationErrorException("SharedConfig not defined");
						log.Error(ex);
						ExceptionDialog.showExceptionWithStackTrace(ex, "Exception", "");
					}

					// instantiate the master and tick it in its own thread
					_master = new Master(
						_ac,
						PathUtils.GetRootForRelativePaths( _ac.SharedCfgFileName, _ac.RootForRelativePaths )
					);
					_masterThread = new Thread(() =>
					{
						while( !_master.WantsQuit )
						{
							_master.Tick();
							Thread.Sleep( _ac.MasterTickPeriod );
						}
						_master.Dispose();
						_master = null;
					});
					_masterThread.Start();
				}
				else
				{
					log.Error( "Another instance of Dirigent Master is already running!" );
					MessageBox.Show("Another instance of Dirigent Master is already running on this machine!", "Dirigent", MessageBoxButtons.OK, MessageBoxIcon.Warning);
				}
			}
		}

        private void DeinitializeMaster()
        {
			if( _masterThread != null )
			{
				_master.WantsQuit = true;
				_masterThread.Join( 4*_ac.TickPeriod );
			}

			_master?.Dispose();
			_master = null;
		}


		private void ExitApp()
		{
			//if( !string.IsNullOrEmpty( _machineId ) ) // if we were started together with a local agent
			//{
			//	if( MessageBox.Show( "Exit Dirigent and kill apps on this computer?", "Dirigent", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning ) == DialogResult.OK )
			//	{
			//		// send Terminate message to the agent on this machine via a temporary client
			//		var clientIdent = new Net.ClientIdent() { Sender = Guid.NewGuid().ToString(), SubscribedTo = Net.EMsgRecipCateg.Gui };
			//		using var client = new Net.Client( clientIdent, _ac.MasterIP, _ac.MasterPort, autoConn: false );
			//		if( client.Connect() )
			//		{
			//			var args = new TerminateArgs() { KillApps = true, MachineId = _machineId };
			//			client.Send( new Net.TerminateMessage( client.Ident.Name, args ) );
			//		}
			//		client.Disconnect();
			//		client.Dispose();
			//	}
			//	Thread.Sleep( 2 * _ac.TickPeriod );
			//	Application.Exit();
			//}
			//else // not tied to any agent - simply quit the gui
			//{
			//	Application.Exit();
			//}

			// both agent and master are integrated in this app so we can simply exit
			// no talking to external agent/master apps necessary unless we want to kill all apps managed by the dirigent
			Application.Exit();
		}

	}

	// http://www.codeproject.com/Tips/543631/Save-and-restore-your-form-size-and-location
	static class ExtensionMethods
	{
		public static void RestoreWindowSettings( this Form form, string initLocation )
		{
			Point il = new Point( 0, 0 );
			Size sz = form.Size;
			if( !string.IsNullOrEmpty( initLocation ) )
			{
				string[] parts = initLocation.Split( ',' );
				if( parts.Length >= 2 )
				{
					il = new Point( int.Parse( parts[0] ), int.Parse( parts[1] ) );
				}
				if( parts.Length >= 4 )
				{
					sz = new Size( int.Parse( parts[2] ), int.Parse( parts[3] ) );
				}
			}
			form.Size = sz;
			form.Location = il;
		}

		/// Each window must have its own setting name (e.g. MainFormLocation, etc) in Settings.settings
		public static void SaveWindowSettings( this Form form, string settingsNameForLocation )
		{
			Point location = form.Location;
			Size size = form.Size;
			if( form.WindowState != FormWindowState.Normal )
			{
				location = form.RestoreBounds.Location;
				size = form.RestoreBounds.Size;
			}
			string initLocation = string.Join( ",", new string[] { location.X.ToString(), location.Y.ToString(), size.Width.ToString(), size.Height.ToString() } );
			Common.Properties.Settings.Default[settingsNameForLocation] = initLocation;
			Common.Properties.Settings.Default.Save();
		}
	}
}
