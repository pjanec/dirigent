using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows.Forms;
using System.Configuration;

using Dirigent.Common;
using Dirigent.Agent.Core;
using Dirigent.Agent.Gui;

using CommandLine;
using CommandLine.Text;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace Dirigent.Agent.TrayApp
{
    // Define a class to receive parsed values
    class Options {
      [Option("masterPort", Required = false, DefaultValue=0, HelpText = "Master's TCP port.")]
      public int MasterPort { get; set; }

      [Option("masterIP", Required = false, DefaultValue = "", HelpText = "Master's IP address.")]
      public string MasterIP { get; set; }

      [Option("machineId", Required = false, DefaultValue="", HelpText = "Machine Id.")]
      public string MachineId { get; set; }

      [Option("sharedConfigFile", Required = false, DefaultValue = "", HelpText = "shared config file name.")]
      public string SharedConfigFile { get; set; }
      

      [ParserState]
      public IParserState LastParserState { get; set; }

      [HelpOption]
      public string GetUsage() {
        return HelpText.AutoBuild(this,
          (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
      }
    }

    public class MyApplicationContext : ApplicationContext
    {
    }
    
    /// <summary>
    /// We initialize the main form but we do not show it until the tray icon is clicked.
    /// The main form stays initialized until the app is closed. We prevent it from closing,
    /// we are hiding it instead.
    /// It does not show in the task bar as the app is run in a separate application context.
    /// </summary>
    static class Program
    {
        static frmMain mainForm;
        static Dirigent.Net.IClient client;
        static NotifyIcon notifyIcon;

        static void InitializeContext()
        {
            var components = new System.ComponentModel.Container();

            notifyIcon = new NotifyIcon(components);
            notifyIcon.Text = "Dirigent";
            notifyIcon.Icon = TrayApp.Properties.Resources.AppIcon;
            notifyIcon.ContextMenu = new ContextMenu(
                                        new MenuItem[] 
                                        {
                                            new MenuItem("Show", new EventHandler( (s,e) => Show() )),
                                            new MenuItem("Exit", new EventHandler( (s,e) => Exit() ))
                                        });
            notifyIcon.Visible = true;
            notifyIcon.DoubleClick += new EventHandler( (s, e) => Show() );
        }

        static void Show()
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

        static void Exit()
        {
            // We must manually tidy up and remove the icon before we exit.
            // Otherwise it will be left behind until the user mouses over.
            notifyIcon.Visible = false;
            DeinitializeMainForm();
            Application.Exit();
        }

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);


        static void InitializeMainForm()
        {
            // start with default settings
            string sharedCfgFileName = Path.Combine(Application.StartupPath, "SharedConfig.xml");
            //string localCfgFileName = Path.Combine(Application.StartupPath, "LocalConfig.xml");
            string machineId = System.Environment.MachineName;
            int masterPort = 5032;
            string masterIP = "127.0.0.1";

            // overwrite with application config
            if (Properties.Settings.Default.MachineId != "") machineId = Properties.Settings.Default.MachineId;
            if (Properties.Settings.Default.MasterIP != "") masterIP = Properties.Settings.Default.MasterIP;
            if (Properties.Settings.Default.MasterPort != 0) masterPort = Properties.Settings.Default.MasterPort;
            if (Properties.Settings.Default.SharedConfigFile != "") sharedCfgFileName = Properties.Settings.Default.SharedConfigFile;

            // overwrite with command line options
            var options = new Options();
            if (CommandLine.Parser.Default.ParseArguments(System.Environment.GetCommandLineArgs(), options))
            {
                if (options.MachineId != "") machineId = options.MachineId;
                if (options.MasterIP != "") masterIP = options.MasterIP;
                if (options.MasterPort != 0) masterPort = options.MasterPort;
                if (options.SharedConfigFile != "") sharedCfgFileName = options.SharedConfigFile;
            }


            try
            {
                SharedConfig scfg = new SharedXmlConfigReader().Load(File.OpenText(sharedCfgFileName));
                //LocalConfig lcfg = new LocalXmlConfigReader().Load(File.OpenText(localCfgFileName));

                //var client = new Dirigent.Net.Client(machineId, masterIP, masterPort);
                //client.Connect(); // FIXME: add reconnection support!

                log.Info(string.Format("Running with machineId={0}, masterIp={1}, mastrePort={2}", machineId, masterIP, masterPort));

                client = new Dirigent.Net.AutoconClient(machineId, masterIP, masterPort);

                var agent = new Dirigent.Agent.Core.Agent(machineId, client);

                mainForm = new frmMain(agent.getControl(), agent.tick, scfg, machineId, client.IsConnected);

                // if form is user-closed, just hide it
                mainForm.onCloseDeleg += (e) =>
                {
                    if (e.CloseReason == CloseReason.UserClosing)
                    {
                        // prevent window closing
                        e.Cancel = true;
                        mainForm.Hide();
                    }
                };
            }
            catch (Exception ex)
            {
                log.Error(ex);
                ExceptionDialog.showException(ex, "Dirigent Exception", "");
            }
        }

        static void DeinitializeMainForm()
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

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            InitializeMainForm();
            InitializeContext();
            //Show();
            Application.Run(new MyApplicationContext());

        }
    }
}
