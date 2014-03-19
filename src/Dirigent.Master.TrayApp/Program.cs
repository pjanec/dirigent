using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using System.Configuration;

using Dirigent.Net;

namespace Dirigent.Master.TrayApp
{
    static class Program
    {
        static Mutex mutex = new Mutex(true, "{B5AB2D73-19C0-4BEF-B092-25B485C830C4}");

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            if(mutex.WaitOne(TimeSpan.Zero, true))
            {
            
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Show the system tray icon.
                using (ProcessIcon pi = new ProcessIcon())
                { 
                    pi.Display();

                    // start the server
                    // FIXME: read the port number from the configuration!
                    var s = new Server(12345);

                    // Make sure the application runs!
                    Application.Run();
                }

                mutex.ReleaseMutex();
            }
            else
            {
                MessageBox.Show("only one instance at a time", "Dirigent Master");
            }
        }
    }
}
