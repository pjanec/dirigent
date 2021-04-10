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
        [Option("masterPort", Required = false, Default = 0, HelpText = "Master's TCP port.")]
        public int MasterPort { get; set; }

        [Option("masterIp", Required = false, Default = "", HelpText = "Master's IP address.")]
        public string MasterIP { get; set; }

        [Option("mcastIp", Required = false, Default = "", HelpText = "Multicast IP address.")]
        public string McastIP { get; set; }

        [Option("localIp", Required = false, Default = "", HelpText = "Local addapter IP address to bind to when multicasting.")]
        public string LocalIP { get; set; }

        [Option("mcastAppStates", Required = false, Default = "", HelpText = "Use multical for sharing app states among agents.")]
        public string McastAppStates { get; set; }

        [Option("machineId", Required = false, Default = "", HelpText = "Machine Id.")]
        public string MachineId { get; set; }

        [Option("sharedConfigFile", Required = false, Default = "", HelpText = "shared config file name.")]
        public string SharedConfigFile { get; set; }

        [Option("localConfigFile", Required = false, Default = "", HelpText = "local config file name.")]
        public string LocalConfigFile { get; set; }

        [Option("logFile", Required = false, Default = "", HelpText = "log file name.")]
        public string LogFile { get; set; }

        [Option("startupPlan", Required = false, Default = "", HelpText = "Plan to be started on startup.")]
        public string StartupPlan { get; set; }

        [Option("startHidden", Required = false, Default = "", HelpText = "Start with Dirigent GUI hidden in tray [0|1].")]
        public string StartHidden { get; set; }

        [Option("isMaster", Required = false, Default = "", HelpText = "Start Master process automatically [0|1].")]
        public string IsMaster { get; set; }

        [Option("CLIPort", Required = false, Default = 0, HelpText = "Master's Command Line Interface TCP port (passed to Master process).")]
        public int CLIPort { get; set; }

        [Option("mode", Required = false, Default = "", HelpText = "Mode of operation. [daemon|trayGui|remoteControlGui].")]
        public string Mode { get; set; }

        [Option("tickPeriod", Required = false, Default = 0, HelpText = "Refresh period in msec.")]
        public int TickPeriod { get; set; }

    }

    public class AppConfig
    {
        // start with default settings
        public string sharedCfgFileName = ""; // Path.Combine(Application.StartupPath, "SharedConfig.xml");
        public string localCfgFileName = ""; // empty by default - we won't try to load it
        public string machineId = System.Environment.MachineName;
        public int masterPort = 5032;
        public int cliPort = 5050;
        public string masterIP = "127.0.0.1";
        public string logFileName = "";
        public string startupPlanName = "";
        public string startHidden = "0"; // "0" or "1"
        public string mode = "trayGui"; // "trayGui", "remoteControlGui", "daemon"
        public SharedConfig scfg = null;
        public LocalConfig lcfg = null;
        public string isMaster = "0"; // "1"=run the master process automatically
        public int tickPeriod = 500; // msec
        public string mcastIP = "239.121.121.121";
        public string localIP = "0.0.0.0";
        public string mcastAppStates = "0";

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public bool HadErrors = false;

        ParserResult<Options> _parserResult;
        
        public string GetUsageHelpText()
        {
            return HelpText.AutoBuild( _parserResult ).ToString();
        }

        Options options = new Options();

        public AppConfig()
        {
            // overwrite with application config
            if (Properties.Settings.Default.MachineId != "") machineId = Properties.Settings.Default.MachineId;
            if (Properties.Settings.Default.MasterIP != "") masterIP = Properties.Settings.Default.MasterIP;
            if (Properties.Settings.Default.McastIP != "") mcastIP = Properties.Settings.Default.McastIP;
            if (Properties.Settings.Default.LocalIP != "") localIP = Properties.Settings.Default.LocalIP;
            if (Properties.Settings.Default.McastAppStates != "") mcastAppStates = Properties.Settings.Default.McastAppStates;
            if (Properties.Settings.Default.MasterPort != 0) masterPort = Properties.Settings.Default.MasterPort;
            if (Properties.Settings.Default.SharedConfigFile != "") sharedCfgFileName = Properties.Settings.Default.SharedConfigFile;
            if (Properties.Settings.Default.LocalConfigFile != "") localCfgFileName = Properties.Settings.Default.LocalConfigFile;
            if (Properties.Settings.Default.StartupPlan != "") startupPlanName = Properties.Settings.Default.StartupPlan;
            if (Properties.Settings.Default.StartHidden != "") startHidden = Properties.Settings.Default.StartHidden;
            if (Properties.Settings.Default.Mode != "") mode = Properties.Settings.Default.Mode;
            if (Properties.Settings.Default.Mode != "") mode = Properties.Settings.Default.Mode;
            if (Properties.Settings.Default.IsMaster != "") isMaster = Properties.Settings.Default.IsMaster;
            if (Properties.Settings.Default.CLIPort != 0) cliPort = Properties.Settings.Default.CLIPort;
            if (Properties.Settings.Default.TickPeriod != 0) tickPeriod = Properties.Settings.Default.TickPeriod;

            _parserResult = CommandLine.Parser.Default.ParseArguments<Options>(System.Environment.GetCommandLineArgs());
            
            _parserResult.WithParsed<Options>( (Options options) =>
                {
                    if (options.MachineId != "") machineId = options.MachineId;
                    if (options.MasterIP != "") masterIP = options.MasterIP;
                    if (options.McastIP != "") mcastIP = options.McastIP;
                    if (options.McastAppStates != "") mcastAppStates = options.McastAppStates;
                    if (options.LocalIP != "") localIP = options.LocalIP;
                    if (options.MasterPort != 0) masterPort = options.MasterPort;
                    if (options.SharedConfigFile != "") sharedCfgFileName = options.SharedConfigFile;
                    if (options.LocalConfigFile != "") localCfgFileName = options.LocalConfigFile;
                    if (options.LogFile != "") logFileName = options.LogFile;
                    if (options.StartupPlan != "") startupPlanName = options.StartupPlan;
                    if (options.StartHidden != "") startHidden = options.StartHidden;
                    if (options.Mode != "") mode = options.Mode;
                    if (options.IsMaster != "") isMaster = options.IsMaster;
                    if (options.CLIPort != 0) cliPort = options.CLIPort;
                    if (options.TickPeriod != 0) tickPeriod = options.TickPeriod;
                })
                .WithNotParsed<Options>( (errList) =>
                {
                    HadErrors = true;
                });


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

            if (localCfgFileName != "")
            {
                localCfgFileName = Path.GetFullPath(localCfgFileName);
                log.DebugFormat("Loading local config file '{0}'", localCfgFileName);
                lcfg = new LocalXmlConfigReader().Load(File.OpenText(localCfgFileName));
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
