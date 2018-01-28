using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.IO;
using System.Threading;

using CommandLine;
using CommandLine.Text;

using Dirigent.Net;
using Dirigent.Common;
using Dirigent.Agent.Core;

using log4net;
using log4net.Appender;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace Dirigent.Master
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

    class Program
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

		/// <summary>
		/// Local agent used to gather apps and plan state and server TCP telnet requests
		/// </summary>
		static Dirigent.Agent.Core.Agent agent;
			
		class AppConfig
        {
            // start with default settings
            public string sharedCfgFileName = "SharedConfig.xml";
            //public string localCfgFileName = Path.Combine(Application.StartupPath, "LocalConfig.xml");
            public int masterPort = 5032;
            public string logFileName = "";
            public string startupPlanName = "";
            public SharedConfig scfg = null;
        }

        static AppConfig getAppConfig()
        {
            var ac = new AppConfig();

            // overwrite with application config
            if (Properties.Settings.Default.MasterPort != 0) ac.masterPort = Properties.Settings.Default.MasterPort;
            if (Properties.Settings.Default.SharedConfigFile != "") ac.sharedCfgFileName = Properties.Settings.Default.SharedConfigFile;
            if (Properties.Settings.Default.StartupPlan != "") ac.startupPlanName = Properties.Settings.Default.StartupPlan;

            // overwrite with command line options
            var options = new Options();
            if (CommandLine.Parser.Default.ParseArguments(System.Environment.GetCommandLineArgs(), options))
            {
                if (options.MasterPort != 0) ac.masterPort = options.MasterPort;
                if (options.SharedConfigFile != "") ac.sharedCfgFileName = options.SharedConfigFile;
                if (options.LogFile != "") ac.logFileName = options.LogFile;
                if (options.StartupPlan != "") ac.startupPlanName = options.StartupPlan;
            }

            if (ac.logFileName != "")
            {
                SetLogFileName(Path.GetFullPath(ac.logFileName));
            }

            if( ac.sharedCfgFileName != "" )
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

        static void Initialize()
        {
            try
            {
                var ac = getAppConfig();

                log.InfoFormat("Master running on port {0}", ac.masterPort);

                IEnumerable<ILaunchPlan> planRepo = (ac.scfg != null) ? ac.scfg.Plans : null;
                
                var s = new Server(ac.masterPort, planRepo, ac.startupPlanName);
                // server works through its ServerRemoteObject

                // start a local network-only agent
				// use unique client id to avoid conflict with any other possible client
                string machineId = Guid.NewGuid().ToString();
                var dirigClient = new Dirigent.Net.Client(machineId, "127.0.0.1", ac.masterPort);
                dirigClient.Connect(); // connect should succeed immediately (server runs locally)
				agent = new Dirigent.Agent.Core.Agent(machineId, dirigClient, false);

				// TODO: start a telnet client server
				// ...

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
            Console.WriteLine("Press Ctr+C to stop the server.");
			
			// run forever
			while (true)
			{
				agent.tick();
				Thread.Sleep(500);
			}
        }

    }
}
