using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Windows.Forms;
using System.Configuration;
using System.Drawing;

using Dirigent.Common;
using Dirigent.Agent.Core;
using Dirigent.Agent.Gui;

using log4net.Appender;
using log4net;

namespace Dirigent.Agent.TrayApp
{
    public class TrayApp : App
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        AppConfig ac;
        frmMain mainForm;
        Dirigent.Net.IClient client;
        NotifyIcon notifyIcon;
        MasterRunner masterRunner;
		List<FolderWatcher> folderWatchers = new List<FolderWatcher>();
        Dirigent.Agent.Core.Agent agent;

        // trying to run the server within the master agent (failing, not communicating..)
        //Net.Server server;
		//CLIServer cliServer;

        class MyApplicationContext : ApplicationContext
        {
        }

        public TrayApp(AppConfig ac)
        {
            this.ac = ac;
        }

        private void InitializeMaster()
        {
            if( AppConfig.BoolFromString(ac.isMaster) )
            {
				masterRunner = new MasterRunner();
				masterRunner.MasterPort = ac.masterPort;
				masterRunner.CLIPort = ac.cliPort;
				masterRunner.StartupPlan = ac.startupPlanName;
				masterRunner.SharedConfigFile = ac.sharedCfgFileName;
                masterRunner.McastIP = ac.mcastIP;
                masterRunner.LocalIP = ac.localIP;
                masterRunner.McastAppStates = ac.mcastAppStates;
                masterRunner.TickPeriod = ac.tickPeriod;
				try
				{
					masterRunner.Launch();
					masterRunner.StartKeepAlive();
				}
				catch (Exception ex)
				{
					log.Error(ex);
					ExceptionDialog.showException(ex, "Dirigent Exception", "");
					masterRunner = null;
				}

				//            // start master server
				//            IEnumerable<ILaunchPlan> planRepo = (ac.scfg != null) ? ac.scfg.Plans : null;
				//server = new Net.Server(ac.masterPort, agent.Control, planRepo, ac.startupPlanName);
				//            // server works through its ServerRemoteObject

				//// start a telnet client server
				//            log.InfoFormat("Command Line Interface running on port {0}", ac.cliPort);
				//cliServer = new CLIServer( "0.0.0.0", ac.cliPort, agent.Control );
				//cliServer.Start();



			}
        }

        private void DeinitializeMaster()
        {
			if (masterRunner != null)
			{
				masterRunner.Dispose();
				masterRunner = null;
			}

			//if( cliServer != null )
			//{
			//    cliServer.Dispose();
			//    cliServer = null;
			//}

			//if( server != null )
			//{
			//    server.Dispose();
			//    server = null;
			//}
		}

        public void run()
        {
            // listen to AppExit messages
            AppMessenger.Instance.Register<Common.AppMessages.ExitApp>( (x) => Exit() );
            AppMessenger.Instance.Register<Common.AppMessages.CheckSharedConfigAndRestartMaster>( (x) => CheckSharedConfigAndRestartMaster() );


            InitializeMaster();
        
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            InitializeTrayIcon();
			try
			{
				InitializeMainForm();
				Application.Run( new MyApplicationContext() );
			}
			catch( Exception ex )
			{
				log.Error( ex );
				ExceptionDialog.showException( ex, "Dirigent Exception", "" );
			}
			finally
			{
				DeinitializeMainForm();
                DeinitializeMaster();
			}
        }

        void InitializeTrayIcon()
        {
            var components = new System.ComponentModel.Container();

            notifyIcon = new NotifyIcon(components);
            notifyIcon.Text = "Dirigent";
            notifyIcon.Icon = Dirigent.Agent.TrayApp.Properties.Resources.AppIcon;
            
            var menuItems = new List<ToolStripMenuItem>();
            menuItems.Add( new ToolStripMenuItem("Show", null, new EventHandler( (s,e) => Show() )) );
            if( masterRunner != null )
            {
                menuItems.Add( new ToolStripMenuItem("Master's Console", null, new EventHandler( (s,e) =>
                {
                    ToolStripMenuItem mi = s as ToolStripMenuItem;
                    if( masterRunner != null )
                    {
                        masterRunner.IsConsoleShown = !mi.Checked;
                        mi.Checked = masterRunner.IsConsoleShown;
                    }
                }
                )) );
            }
            menuItems.Add( new ToolStripMenuItem("Exit", null, new EventHandler( (s,e) =>
            {
                agent.LocalOps.Terminate( new TerminateArgs() { KillApps=true, MachineId=ac.machineId }  );
                //Exit();
            })) );

            //menuItems
            var cms = new ContextMenuStrip();
            foreach( var x in menuItems ) cms.Items.Add( x );
            notifyIcon.ContextMenuStrip = cms;
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += new EventHandler((s, e) => Show());
        }

        void InitializeMainForm()
        {
            log.InfoFormat("Running with machineId={0}, masterIp={1}, masterPort={2}, mcastIP={3}, localIP={4}, useMcast={5}", ac.machineId, ac.masterIP, ac.masterPort, ac.mcastIP, ac.localIP, ac.mcastAppStates);

            bool runningAsRemoteControlGui = (ac.machineId == "none");

            string rootForRelativePaths = System.IO.Path.GetDirectoryName( System.IO.Path.GetFullPath(ac.sharedCfgFileName) );

            if (runningAsRemoteControlGui) // running just as observation GUI?
            {
                // we act like agent with no apps assigned
                // generate unique GUID to avoid matching any machineId in the launch plans
                string machineId = "remoteControlGui-"+Guid.NewGuid().ToString();

                client = new Dirigent.Net.Client(machineId, ac.masterIP, ac.masterPort, ac.mcastIP, ac.masterPort, ac.localIP, autoConn:true);

                agent = new Dirigent.Agent.Core.Agent(machineId, client, false, rootForRelativePaths, false, AppConfig.BoolFromString(ac.mcastAppStates)); // don't go local if not connected
            }
            else // running as local app launcher
            {
                string clientId = "agent-" + ac.machineId;
                
                client = new Dirigent.Net.Client(clientId, ac.masterIP, ac.masterPort, ac.mcastIP, ac.masterPort, ac.localIP, autoConn:true);

                agent = new Dirigent.Agent.Core.Agent(ac.machineId, client, true, rootForRelativePaths, false, AppConfig.BoolFromString(ac.mcastAppStates));

				InitializeFolderWatchers(agent, rootForRelativePaths);
            }


            IEnumerable<ILaunchPlan> planRepo = (ac.scfg != null) ? ac.scfg.Plans : null;

            // if there is some local plan repo defined, use it for local operations
            if (planRepo != null)
            {
                agent.LocalOps.SetPlanRepo(planRepo);
            }

            // start given plan if provided
            if (planRepo != null)
            {
                agent.LocalOps.SelectPlan(ac.startupPlanName);
            }

            var callbacks = new GuiAppCallbacks();
            callbacks.isConnectedDeleg = client.IsConnected;
            callbacks.onTickDeleg = agent.tick;

            mainForm = new frmMain(agent.Control, planRepo, ac.machineId, client.Name, notifyIcon, !runningAsRemoteControlGui, callbacks, ac.tickPeriod);

            // restore saved location if SHIFT not held
            if ((Control.ModifierKeys & Keys.Shift) == 0)
            {
                string initLocation = Properties.Settings.Default.MainFormLocation;
 
                mainForm.RestoreWindowSettings(initLocation);
            }
            else  // for default I just want the form to start in the top-left corner.
            {
                Point topLeftCorner = new Point(0, 0);
                mainForm.Location = topLeftCorner;
            }

            // if form is user-closed, don't destroy it, just hide it
            callbacks.onCloseDeleg += (e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    // prevent window closing
                    e.Cancel = true;
                    mainForm.Hide();
                }
            };

            // show the form if it should not stay hidden
            if (!AppConfig.BoolFromString(ac.startHidden))
            {
                Show();
            }
        }

        void DeinitializeMainForm()
        {
            if (mainForm != null)
            {
                // save main form's location and size
                if ((Control.ModifierKeys & Keys.Shift) == 0)
                {
                    mainForm.SaveWindowSettings("MainFormLocation");
                }

                mainForm.Close();
                mainForm.Dispose();
                mainForm = null;
            }

            if (client != null)
            {
                client.Dispose();
                client = null;
            }
        } 

		void InitializeFolderWatchers( Dirigent.Agent.Core.Agent agent, string rootForRelativePaths )
		{
		// no local config file loaded	
			if(ac.lcfg==null) return;

			foreach( var xmlCfg in ac.lcfg.folderWatcherXmls )
			{
				var fw = new FolderWatcher( xmlCfg, agent.Control, rootForRelativePaths );
				if( fw.Initialized )
				{
					folderWatchers.Add( fw );
				}
			}
		}

        void Show()
        {
            // If we are already showing the window, merely focus it.
            if (mainForm.Visible)
            {
                mainForm.Activate();
            }
            else
            {
                mainForm.Show();
                mainForm.WindowState = FormWindowState.Normal;
            }

        }

        void Exit()
        {
            // release all delegates to allow disposal by releasing strong refereces
            AppMessenger.Instance.Dispose();

            // We must manually tidy up and remove the icon before we exit.
            // Otherwise it will be left behind until the user mouses over.
            notifyIcon.Visible = false;
            Application.Exit();
        }

        void CheckSharedConfigAndRestartMaster()
        {
            if( masterRunner != null ) // we are the master
            {
                log.DebugFormat("Dry-loading shared config {0}", ac.sharedCfgFileName);
                // dry-load the configuration to see if any error there
                // it will throw on some error, exception will be sent back to requestor as RemoteOperationError
                try
                {
                    var scfg = new SharedXmlConfigReader().Load(System.IO.File.OpenText(ac.sharedCfgFileName));
                }
                catch( System.Exception ex )
                {
                    log.Error(String.Format("Failed to load shared config {0}", ac.sharedCfgFileName), ex );
                    throw;
                }

                // the above has not thrown => probably success, restart the master
                log.Debug("Restarting master");
                masterRunner.StopKeepAlive();
                masterRunner.Kill();
                System.Threading.Thread.Sleep(1000);
                masterRunner.Launch();
                masterRunner.StartKeepAlive();
            }
        }

    }

    // http://www.codeproject.com/Tips/543631/Save-and-restore-your-form-size-and-location
    static class ExtensionMethods
    {
        public static void RestoreWindowSettings(this Form form, string initLocation)
        {
                Point il = new Point(0, 0);
                Size sz = form.Size;
                if (!string.IsNullOrEmpty(initLocation))
                {
                    string[] parts = initLocation.Split(',');
                    if (parts.Length >= 2)
                    {
                        il = new Point(int.Parse(parts[0]), int.Parse(parts[1]));
                    }
                    if (parts.Length >= 4)
                    {
                        sz = new Size(int.Parse(parts[2]), int.Parse(parts[3]));
                    }
                }
                form.Size = sz;
                form.Location = il;
        }
 
        /// Each window must have its own setting name (e.g. MainFormLocation, etc) in Settings.settings
        public static void SaveWindowSettings(this Form form, string settingsNameForLocation)
        {
            Point location = form.Location;
            Size size = form.Size;
            if (form.WindowState != FormWindowState.Normal)
            {
                location = form.RestoreBounds.Location;
                size = form.RestoreBounds.Size;
            }
            string initLocation = string.Join(",", new string[] { location.X.ToString(), location.Y.ToString(), size.Width.ToString(), size.Height.ToString() } );
            Properties.Settings.Default[settingsNameForLocation] = initLocation;
            Properties.Settings.Default.Save();
        }
    }
}
