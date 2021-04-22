using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using Dirigent.Common;

namespace Dirigent.Gui.WinForms
{
	public class GuiTrayApp : App
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

		private AppConfig _ac;
		private frmMain _mainForm;
		private NotifyIcon _notifyIcon;
        private ProcRunner _agentRunner;
		private bool _runGui;
		private bool _runAgent;

		class MyApplicationContext : ApplicationContext
		{
		}

		public GuiTrayApp( AppConfig ac, bool runAgent, bool runGui )
		{
			_ac = ac;
			_runAgent = runAgent;
			_runGui = runGui;
		}

		public EAppExitCode run()
		{
			log.Info( $"Running with masterIp={_ac.MasterIP}, masterPort={_ac.MasterPort}" );

			// listen to AppExit messages
			AppMessenger.Instance.Register<Common.AppMessages.ExitApp>( ( x ) => Application.Exit() );
			//AppMessenger.Instance.Register<Common.AppMessages.CheckSharedConfigAndRestartMaster>( (x) => CheckSharedConfigAndRestartMaster() );


			Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault( false );

			var exitCode = EAppExitCode.NoError;

			if( _runAgent )
			{
				InitializeAgent();
			}


			InitializeTrayIcon();

			try
			{
				if( _runGui )
				{
					InitializeMainForm();
				}
				Application.Run( new MyApplicationContext() );
			}
			catch( Exception ex )
			{
				log.Error( ex );
				ExceptionDialog.showException( ex, "Dirigent Exception", "" );
				exitCode = EAppExitCode.ExceptionError;
			}
			DeinitializeMainForm();
			DeinitializeTrayIcon();
			DeinitializeAgent();

			AppMessenger.Instance.Dispose();

			return exitCode;
		}

		void InitializeTrayIcon()
		{
			var components = new System.ComponentModel.Container();

			_notifyIcon = new NotifyIcon( components );
			_notifyIcon.Text = "Dirigent";
			_notifyIcon.Icon = Properties.Resources.AppIcon;

			var menuItems = new List<ToolStripMenuItem>();
			if( _runGui )
			{
				menuItems.Add( new ToolStripMenuItem( "Show", null, new EventHandler( ( s, e ) => ShowMainForm() ) ) );
			}
            if( _runAgent )
            {
				const string BaseText = "Agent's Console";
                menuItems.Add( new ToolStripMenuItem(BaseText, null, new EventHandler( (s,e) =>
                {
                    ToolStripMenuItem mi = s as ToolStripMenuItem;
					
					// try to re-run the agent if not running from whatever reson
					if( _agentRunner == null )
					{
						InitializeAgent();
					}
                    if( _agentRunner is { IsRunning: false } )
					{
						DeinitializeAgent();
						InitializeAgent();
					}

					// 
					//bool isRunning = _agentRunner != null && _agentRunner.IsRunning;
					//mi.Text = isRunning ? BaseText : BaseText + " (not running)";

                    if( _agentRunner != null )
                    {
                        _agentRunner.IsConsoleShown = !mi.Checked;
                        mi.Checked = _agentRunner.IsConsoleShown;
                    }
                }
                )) );
            }
			menuItems.Add( new ToolStripMenuItem( "Exit", null, new EventHandler( ( s, e ) =>
			{
				//agent.LocalOps.Terminate( new TerminateArgs() { KillApps=true, MachineId=ac.machineId }  );
				Application.Exit();
			} ) ) );

			//menuItems
			var cms = new ContextMenuStrip();
			foreach( var x in menuItems ) cms.Items.Add( x );
			_notifyIcon.ContextMenuStrip = cms;
			_notifyIcon.Visible = true;
			_notifyIcon.DoubleClick += new EventHandler( ( s, e ) => {
				if( _runGui )
				{
					ShowMainForm();
				}
			});
		}

		void DeinitializeTrayIcon()
		{
			// We must manually tidy up and remove the icon before we exit.
			// Otherwise it will be left behind until the user mouses over.
			_notifyIcon.Visible = false;
		}

		void InitializeMainForm()
		{
			// show the form if it should not stay hidden
			if( !Tools.BoolFromString( _ac.StartHidden ) )
			{
				ShowMainForm();
			}
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
			_mainForm = new frmMain( _ac, _notifyIcon );

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

        private void InitializeAgent()
        {
			_agentRunner = new ProcRunner( "Dirigent.Agent.exe", "agent" );
			try
			{
				_agentRunner.Launch();
				_agentRunner.StartKeepAlive();
			}
			catch (Exception ex)
			{
				log.Error(ex);
				ExceptionDialog.showException(ex, "Dirigent Exception", "");
				_agentRunner = null;
			}
        }

        private void DeinitializeAgent()
        {
			_agentRunner?.Dispose();
			_agentRunner = null;
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
