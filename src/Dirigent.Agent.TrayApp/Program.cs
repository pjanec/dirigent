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

        class AppConfig
        {
            // start with default settings
            public string sharedCfgFileName = ""; // Path.Combine(Application.StartupPath, "SharedConfig.xml");
            //public string localCfgFileName = Path.Combine(Application.StartupPath, "LocalConfig.xml");
            public string machineId = System.Environment.MachineName;
            public int masterPort = 5032;
            public string masterIP = "127.0.0.1";
            public string logFileName = "";
            public string startupPlanName = "";
            public string startHidden = "0"; // "0" or "1"
            public SharedConfig scfg = null;
        }

        static AppConfig getAppConfig()
        {
            var ac = new AppConfig();

            // overwrite with application config
            if (Properties.Settings.Default.MachineId != "") ac.machineId = Properties.Settings.Default.MachineId;
            if (Properties.Settings.Default.MasterIP != "") ac.masterIP = Properties.Settings.Default.MasterIP;
            if (Properties.Settings.Default.MasterPort != 0) ac.masterPort = Properties.Settings.Default.MasterPort;
            if (Properties.Settings.Default.SharedConfigFile != "") ac.sharedCfgFileName = Properties.Settings.Default.SharedConfigFile;
            if (Properties.Settings.Default.StartupPlan != "") ac.startupPlanName = Properties.Settings.Default.StartupPlan;
            if (Properties.Settings.Default.StartHidden != "") ac.startHidden = Properties.Settings.Default.StartHidden;

            // overwrite with command line options
            var options = new Options();
            if (CommandLine.Parser.Default.ParseArguments(System.Environment.GetCommandLineArgs(), options))
            {
                if (options.MachineId != "") ac.machineId = options.MachineId;
                if (options.MasterIP != "") ac.masterIP = options.MasterIP;
                if (options.MasterPort != 0) ac.masterPort = options.MasterPort;
                if (options.SharedConfigFile != "") ac.sharedCfgFileName = options.SharedConfigFile;
                if (options.LogFile != "") ac.logFileName = options.LogFile;
                if (options.StartupPlan != "") ac.startupPlanName = options.StartupPlan;
                if (options.StartHidden != "") ac.startHidden = options.StartHidden;
            }

            if (ac.sharedCfgFileName != "")
            {
                ac.sharedCfgFileName = Path.GetFullPath(ac.sharedCfgFileName);
                log.DebugFormat("Loading shared config file '{0}'", ac.sharedCfgFileName);
                ac.scfg = new SharedXmlConfigReader().Load(File.OpenText(ac.sharedCfgFileName));
            }

            return ac;
        }

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


        // Throws exception if plan not found; returns null if empty input arguments are provided.
        static ILaunchPlan getPlanByName(IEnumerable<ILaunchPlan> planRepo, string planName)
        {
            // start the initial launch plan if specified
            if (planRepo != null && planName != null && planName != "")
            {
                try
                {
                    ILaunchPlan plan = planRepo.First((i) => i.Name == planName);
                    return plan;
                }
                catch
                {
                    throw new UnknownPlanName(planName);
                }
            }
            return null;
        }
 
        static bool InitializeMainForm()
        {
            try
            {
                var ac = getAppConfig();

                if (ac.logFileName != "")
                {
                    SetLogFileName(Path.GetFullPath(ac.logFileName));
                }

                log.InfoFormat("Running with machineId={0}, masterIp={1}, mastrePort={2}", ac.machineId, ac.masterIP, ac.masterPort);

                client = new Dirigent.Net.AutoconClient(ac.machineId, ac.masterIP, ac.masterPort);

                var agent = new Dirigent.Agent.Core.Agent(ac.machineId, client, true);

                
                IEnumerable<ILaunchPlan> planRepo = (ac.scfg != null) ? ac.scfg.Plans : null;

                // if there is some local plan repo defined, use it for local operations
                if( planRepo != null )
                {
                    agent.LocalOps.SetPlanRepo(planRepo);
                }

                // start given plan if provided
                if (planRepo != null)
                {
                    ILaunchPlan startupPlan = getPlanByName(planRepo, ac.startupPlanName);
                    if (startupPlan != null)
                    {
                        agent.LocalOps.LoadPlan(startupPlan);
                    }
                }
                    
                mainForm = new frmMain(agent.Control, agent.tick, planRepo, ac.machineId, client.IsConnected);

                // if form is user-closed, don't destroy it, just hide it
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
                if (!(new List<string>() { "1", "YES", "Y", "TRUE" }.Contains(ac.startHidden.ToUpper())))
                {
                    Show();
                }
                return true;

            }
            catch (Exception ex)
            {
                log.Error(ex);
                ExceptionDialog.showException(ex, "Dirigent Exception", "");
                return false;
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

            if (InitializeMainForm())
            {
                InitializeContext();
                //Show();
                Application.Run(new MyApplicationContext());
            }
        }
    }
}
