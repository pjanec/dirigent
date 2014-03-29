using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Configuration;

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


        class MyApplicationContext : ApplicationContext
        {
        }

        public TrayApp(AppConfig ac)
        {
            this.ac = ac;
        }

        public void run()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            InitializeTrayIcon();
            try
            {
                InitializeMainForm();
                Application.Run( new MyApplicationContext() );
            }
            catch (Exception ex)
            {
                log.Error(ex);
                ExceptionDialog.showException(ex, "Dirigent Exception", "");
            }
            finally
            {
                DeinitializeMainForm();
            }
        }

        void InitializeTrayIcon()
        {
            var components = new System.ComponentModel.Container();

            notifyIcon = new NotifyIcon(components);
            notifyIcon.Text = "Dirigent";
            notifyIcon.Icon = Dirigent.Agent.TrayApp.Properties.Resources.AppIcon;
            notifyIcon.ContextMenu = new ContextMenu(
                                        new MenuItem[] 
                                        {
                                            new MenuItem("Show", new EventHandler( (s,e) => Show() )),
                                            new MenuItem("Exit", new EventHandler( (s,e) => Exit() ))
                                        });
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += new EventHandler((s, e) => Show());
        }

        void InitializeMainForm()
        {
            log.InfoFormat("Running with machineId={0}, masterIp={1}, masterPort={2}", ac.machineId, ac.masterIP, ac.masterPort);

            Dirigent.Agent.Core.Agent agent;

            bool runningAsRemoteControlGui = (ac.machineId == "none");

            if (runningAsRemoteControlGui) // running just as observation GUI?
            {
                // we act like agent with no apps assigned
                // generate unique GUID to avoid matching any machineId in the launch plans
                string machineId = "remoteControlGui-"+Guid.NewGuid().ToString();

                client = new Dirigent.Net.AutoconClient(machineId, ac.masterIP, ac.masterPort);

                agent = new Dirigent.Agent.Core.Agent(machineId, client, false); // don't go local if not connected
            }
            else // running as local app launcher
            {
                string clientId = "agent-" + ac.machineId;
                
                client = new Dirigent.Net.AutoconClient(clientId, ac.masterIP, ac.masterPort);

                agent = new Dirigent.Agent.Core.Agent(ac.machineId, client, true);
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
                ILaunchPlan startupPlan = AppHelper.GetPlanByName(planRepo, ac.startupPlanName);
                if (startupPlan != null)
                {
                    agent.LocalOps.LoadPlan(startupPlan);
                }
            }

            var callbacks = new GuiAppCallbacks();
            callbacks.isConnectedDeleg = client.IsConnected;
            callbacks.onTickDeleg = agent.tick;

            mainForm = new frmMain(agent.Control, planRepo, ac.machineId, client.Name, notifyIcon, !runningAsRemoteControlGui, callbacks);

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
            // We must manually tidy up and remove the icon before we exit.
            // Otherwise it will be left behind until the user mouses over.
            notifyIcon.Visible = false;
            Application.Exit();
        }


    }

}
