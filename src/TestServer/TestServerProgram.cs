using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.IO;

using CommandLine;
using CommandLine.Text;

using Dirigent.Net;
using Dirigent.Common;
using log4net;
using log4net.Appender;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace TestServer
{
    // Define a class to receive parsed values
    class Options
    {
        [Option("masterPort", Required = false, DefaultValue = 0, HelpText = "Master's TCP port.")]
        public int MasterPort { get; set; }

        [Option("sharedConfigFile", Required = false, DefaultValue = "", HelpText = "Shared config file name.")]
        public string SharedConfigFile { get; set; }

        [Option("logFile", Required = false, DefaultValue = "", HelpText = "Log file name.")]
        public string LogFile { get; set; }

        [Option("startupPlan", Required = false, DefaultValue = "", HelpText = "Plan to be started on startup.")]
        public string StartupPlan { get; set; }


        [ParserState]
        public IParserState LastParserState { get; set; }

        [HelpOption]
        public string GetUsage()
        {
            return HelpText.AutoBuild(this,
              (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
        }
    }

    class TestServerProgram
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        static void SetLogFileName( string newName )
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

        static void Initialize()
        {
            // start with default settings
            string sharedCfgFileName = "SharedConfig.xml";
            //string localCfgFileName = Path.Combine(Application.StartupPath, "LocalConfig.xml");
            int masterPort = 5032;
            string logFileName = "";
            string startupPlanName = "";


            // overwrite with application config
            if (Properties.Settings.Default.MasterPort != 0) masterPort = Properties.Settings.Default.MasterPort;
            if (Properties.Settings.Default.SharedConfigFile != "") sharedCfgFileName = Properties.Settings.Default.SharedConfigFile;
            if (Properties.Settings.Default.StartupPlan != "") startupPlanName = Properties.Settings.Default.StartupPlan;

            // overwrite with command line options
            var options = new Options();
            if (CommandLine.Parser.Default.ParseArguments(System.Environment.GetCommandLineArgs(), options))
            {
                if (options.MasterPort != 0) masterPort = options.MasterPort;
                if (options.SharedConfigFile != "") sharedCfgFileName = options.SharedConfigFile;
                if (options.LogFile != "") logFileName = options.LogFile;
                if (options.StartupPlan != "") startupPlanName = options.StartupPlan;
            }

            if (logFileName != "")
            {
                SetLogFileName( Path.GetFullPath(logFileName) );
            }

            try
            {
                sharedCfgFileName = Path.GetFullPath(sharedCfgFileName);
                log.DebugFormat("Loading shared config file '{0}'", sharedCfgFileName);
                SharedConfig scfg = new SharedXmlConfigReader().Load(File.OpenText(sharedCfgFileName));

                log.InfoFormat("Master running on port {0}", masterPort);

                var s = new Server( masterPort, scfg.Plans, startupPlanName );
                // server works through its ServerRemoteObject

            }
            catch (Exception ex)
            {
                log.Error(ex);
                //ExceptionDialog.showException(ex, "Dirigent Exception", "");
            }
        }

        static void Main(string[] args)
        {
            Initialize();
            Console.WriteLine("Press a key to exit the server.");
            Console.ReadLine();
        }

    }
}
