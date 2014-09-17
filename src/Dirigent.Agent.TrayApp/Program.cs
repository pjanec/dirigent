using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using System.Configuration;

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
                        MessageBox.Show( ac.GetUsageHelpText(), "Dirigent - Error parsinbg command line arguments" );
                    }

                    ac.machineId = "none";
                    app = new TrayApp(ac);
                }
                else // trayApp (the default)
                {
                    if( ac.HadErrors )
                    {
                        MessageBox.Show( ac.GetUsageHelpText(), "Dirigent - Error parsinbg command line arguments" );
                    }

                    app = new TrayApp(ac);
                }
                app.run();
            }
            catch( Exception ex )
            {
                log.Error(ex);
            }
        }
    }
}
