using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using System.Configuration;
using System.Threading;

using Dirigent.Common;
using Dirigent.Agent.Core;
using Dirigent.Agent.Gui;

using log4net.Appender;
using log4net;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace Dirigent.Agent.TrayApp
{
    
    /// <summary>
    /// We initialize the main form but we do not show it until the tray icon is clicked.
    /// The main form stays initialized until the app is closed. We prevent it from closing,
    /// we are hiding it instead.
    /// It does not show in the task bar as the app is run in a separate application context.
    /// </summary>
    static class Program
    {

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);



        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                var ac = new AppConfig();

                App app;
                if (ac.mode.ToLower() == "daemon")
                {
                    if( AppInstanceAlreadyRunning(ac.masterIP, ac.masterPort, ac.machineId, true)) return;
                    app = new DaemonApp(ac);
                    if( ac.HadErrors )
                    {
                        log.Error("Error parsing command line arguments.\n"+ac.GetUsageHelpText());
                    }
                }
                else
                if (ac.mode.ToLower() == "remotecontrolgui")
                {
                    if( ac.HadErrors )
                    {
                        MessageBox.Show( ac.GetUsageHelpText(), "Dirigent - Error parsing command line arguments" );
                    }

                    ac.machineId = "none";
                    app = new TrayApp(ac);
                }
                else // trayApp (the default)
                {
                    if( ac.HadErrors )
                    {
                        MessageBox.Show( ac.GetUsageHelpText(), "Dirigent - Error parsing command line arguments" );
                    }

                    if( AppInstanceAlreadyRunning(ac.masterIP, ac.masterPort, ac.machineId, false)) return;

                    app = new TrayApp(ac);
                }
                app.run();
            }
            catch( Exception ex )
            {
                log.Error(ex);
            }
        }

        static Mutex singleInstanceMutex;

        static bool AppInstanceAlreadyRunning(string masterIp, int masterPort, string machineId, bool quiet)
        {
            bool createdNew;

            singleInstanceMutex = new Mutex(true, String.Format("DirigentAgent_{0}_{1}_{2}", masterIp, masterPort, machineId), out createdNew);

            if (!createdNew)
            {
                // myApp is already running...
                log.Error("Another instance of Dirigent Agent is already running!");
                if(!quiet)
                {
                    MessageBox.Show( "Another instance of Dirigent Agent is already running!", "Dirigent", MessageBoxButtons.OK, MessageBoxIcon.Exclamation );
                }
                return true;
            }
            return false;
        }

    }
}
