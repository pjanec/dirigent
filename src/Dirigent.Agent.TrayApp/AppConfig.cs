using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Dirigent.Common;

using CommandLine;
using CommandLine.Text;
using log4net.Appender;
using log4net;

namespace Dirigent.Agent.TrayApp
{
    // Define a class to receive parsed values
    class Options
    {
        [Option("masterPort", Required = false, DefaultValue = 0, HelpText = "Master's TCP port.")]
        public int MasterPort { get; set; }

        [Option("masterIP", Required = false, DefaultValue = "", HelpText = "Master's IP address.")]
        public string MasterIP { get; set; }

        [Option("machineId", Required = false, DefaultValue = "", HelpText = "Machine Id.")]
        public string MachineId { get; set; }

        [Option("sharedConfigFile", Required = false, DefaultValue = "", HelpText = "shared config file name.")]
        public string SharedConfigFile { get; set; }

        [Option("logFile", Required = false, DefaultValue = "", HelpText = "log file name.")]
        public string LogFile { get; set; }

        [Option("startupPlan", Required = false, DefaultValue = "", HelpText = "Plan to be started on startup.")]
        public string StartupPlan { get; set; }

        [Option("startHidden", Required = false, DefaultValue = "", HelpText = "Start with Dirigent GUI hidden in tray [0|1].")]
        public string StartHidden { get; set; }

        [Option("mode", Required = false, DefaultValue = "", HelpText = "Mode of operation. [daemon|trayGui|remoteControlGui].")]
        public string Mode { get; set; }

        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }

    public class AppConfig
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
        public string mode = "trayGui"; // "trayGui", "remoteControlGui", "daemon"
        public SharedConfig scfg = null;

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public AppConfig()
        {
            // overwrite with application config
            if (Properties.Settings.Default.MachineId != "") machineId = Properties.Settings.Default.MachineId;
            if (Properties.Settings.Default.MasterIP != "") masterIP = Properties.Settings.Default.MasterIP;
            if (Properties.Settings.Default.MasterPort != 0) masterPort = Properties.Settings.Default.MasterPort;
            if (Properties.Settings.Default.SharedConfigFile != "") sharedCfgFileName = Properties.Settings.Default.SharedConfigFile;
            if (Properties.Settings.Default.StartupPlan != "") startupPlanName = Properties.Settings.Default.StartupPlan;
            if (Properties.Settings.Default.StartHidden != "") startHidden = Properties.Settings.Default.StartHidden;
            if (Properties.Settings.Default.Mode != "") mode = Properties.Settings.Default.Mode;

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
                if (options.Mode != "") mode = options.Mode;
            }

            if (logFileName != "")
            {
                SetLogFileName(Path.GetFullPath(logFileName));
            }

            if (sharedCfgFileName != "")
            {
                sharedCfgFileName = Path.GetFullPath(sharedCfgFileName);
                log.DebugFormat("Loading shared config file '{0}'", sharedCfgFileName);
                scfg = new SharedXmlConfigReader().Load(File.OpenText(sharedCfgFileName));
            }
        }

        static void SetLogFileName(string newName)
        {
            log4net.Repository.Hierarchy.Hierarchy h = (log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository();
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

        public static bool BoolFromString( string boolString )
        {
            return (new List<string>() { "1", "YES", "Y", "TRUE" }.Contains(boolString.ToUpper()));
        }

    }
}
