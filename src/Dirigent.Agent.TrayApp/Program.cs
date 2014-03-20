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
using log4net.Appender;
using log4net;

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

      [Option("logFile", Required = false, DefaultValue = "", HelpText = "log file name.")]
      public string LogFile { get; set; }

      [Option("startupPlan", Required = false, DefaultValue = "", HelpText = "Plan to be started on startup.")]
      public string StartupPlan { get; set; }

      [Option("startHidden", Required = false, DefaultValue = "" , HelpText = "Start with Dirigent GUI hidden in tray [0|1].")]
      public string StartHidden { get; set; }


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

        static void SetLogFileName(string newName)
        {
            log4net.Repository.Hierarchy.Hierarchy h = (log4net.Repository.Hierarchy.Hierarchy) LogManager.GetRepository();
            foreach (IAppender a in h.Root.Appenders)
            {
                if (a is FileAppender)
                {
                    FileAppender fa = (FileAppender)a;
                    fa.File = newName;
                    fa.ActivateOptions();
                    break;
                }
            }
        }

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
            string sharedCfgFileName = ""; // Path.Combine(Application.StartupPath, "SharedConfig.xml");
            //string localCfgFileName = Path.Combine(Application.StartupPath, "LocalConfig.xml");
            string machineId = System.Environment.MachineName;
            int masterPort = 5032;
            string masterIP = "127.0.0.1";
            string logFileName = "";
            string startupPlanName = "";
            string startHidden = "0"; // "0" or "1"

            // overwrite with application config
            if (Properties.Settings.Default.MachineId != "") machineId = Properties.Settings.Default.MachineId;
            if (Properties.Settings.Default.MasterIP != "") masterIP = Properties.Settings.Default.MasterIP;
            if (Properties.Settings.Default.MasterPort != 0) masterPort = Properties.Settings.Default.MasterPort;
            if (Properties.Settings.Default.SharedConfigFile != "") sharedCfgFileName = Properties.Settings.Default.SharedConfigFile;
            if (Properties.Settings.Default.StartupPlan != "") startupPlanName = Properties.Settings.Default.StartupPlan;
            if (Properties.Settings.Default.StartHidden != "") startHidden = Properties.Settings.Default.StartHidden;

            // overwrite with command line options
            var options = new Options();
            if (CommandLine.Parser.Default.ParseArguments(System.Environment.GetCommandLineArgs(), options))
            {
                if (options.MachineId != "") machineId = options.MachineId;
                if (options.MasterIP != "") masterIP = options.MasterIP;
                if (options.MasterPort != 0) masterPort = options.MasterPort;
                if (options.SharedConfigFile != "") sharedCfgFileName = options.SharedConfigFile;
                if (options.LogFile != "") logFileName = options.LogFile;
                if (options.StartupPlan != "") startupPlanName = options.StartupPlan;
                if (options.StartHidden != "") startHidden = options.StartHidden;
            }


            if (logFileName != "")
            {
                SetLogFileName(Path.GetFullPath(logFileName));
            }

            try
            {
                IEnumerable<ILaunchPlan> planRepo = null;
                // load plan repo only there is any shared config file requested
                if (sharedCfgFileName != "")
                {
                    sharedCfgFileName = Path.GetFullPath(sharedCfgFileName);
                    log.DebugFormat("Loading shared config file '{0}'", sharedCfgFileName);
                    SharedConfig scfg = new SharedXmlConfigReader().Load(File.OpenText(sharedCfgFileName));
                    planRepo = scfg.Plans;
                }

                log.InfoFormat("Running with machineId={0}, masterIp={1}, mastrePort={2}", machineId, masterIP, masterPort);

                client = new Dirigent.Net.AutoconClient(machineId, masterIP, masterPort);

                var agent = new Dirigent.Agent.Core.Agent(machineId, client);
                
                // if there is some local plan repo defined, use it for local operations
                if( planRepo != null )
                {
                    agent.LocalOps.SetPlanRepo(planRepo);
                }

                // start the initial launch plan if specified
                if (planRepo != null && startupPlanName != null && startupPlanName != "")
                {
                    ILaunchPlan startupPlan;
                    try
                    {
                        startupPlan = planRepo.First((i) => i.Name == startupPlanName);
                    }
                    catch
                    {
                        throw new UnknownPlanName(startupPlanName);
                    }

                    agent.LocalOps.LoadPlan( startupPlan );
                }

                mainForm = new frmMain(agent.Control, agent.tick, planRepo, machineId, client.IsConnected);

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

                // show the form if it should not stay hidden
                if (!(new List<string>() { "1", "YES", "Y", "TRUE" }.Contains(startHidden.ToUpper())))
                {
                    Show();
                }

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
