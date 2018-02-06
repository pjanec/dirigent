﻿using System;
using System.Collections.Generic;
using System.Linq;
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
			catch( Exception ex )
			{
				log.Error( ex );
				ExceptionDialog.showException( ex, "Dirigent Exception", "" );
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
                agent.LocalOps.SelectPlan(ac.startupPlanName);
            }

            var callbacks = new GuiAppCallbacks();
            callbacks.isConnectedDeleg = client.IsConnected;
            callbacks.onTickDeleg = agent.tick;

            mainForm = new frmMain(agent.Control, planRepo, ac.machineId, client.Name, notifyIcon, !runningAsRemoteControlGui, callbacks);

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
