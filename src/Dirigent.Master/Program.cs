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

        [Option("localConfigFile", Required = false, DefaultValue = "", HelpText = "Local config file name.")]
        public string LocalConfigFile { get; set; }

        [Option("logFile", Required = false, DefaultValue = "", HelpText = "Log file name.")]
        public string LogFile { get; set; }

        [Option("startupPlan", Required = false, DefaultValue = "", HelpText = "Plan to be started on startup.")]
        public string StartupPlan { get; set; }

        [Option("CLIPort", Required = false, DefaultValue = 0, HelpText = "Master's Command Line Interface TCP port.")]
        public int CLIPort { get; set; }

        [Option("ParentAgentPid", Required = false, DefaultValue = -1, HelpText = "PID of agent that is running this Dirigent (-1 = standalone)")]
        public int ParentAgentPid { get; set; }


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

		static CLIServer cliServer;
			
		class AppConfig
        {
            // start with default settings
            public string sharedCfgFileName = "SharedConfig.xml";
            public string localCfgFileName = ""; // empty by default - we won't try to load it
            public int masterPort = 5032;
            public string logFileName = "";
            public string startupPlanName = "";
            public SharedConfig scfg = null;
            public LocalConfig lcfg = null;
            public int CLIPort = 5033;
            public int ParentAgentPid = -1;  // are we startd from an agent, i.e. not standalone?
        }

        static AppConfig getAppConfig()
        {
            var ac = new AppConfig();

            // overwrite with application config
            if (Properties.Settings.Default.MasterPort != 0) ac.masterPort = Properties.Settings.Default.MasterPort;
            if (Properties.Settings.Default.SharedConfigFile != "") ac.sharedCfgFileName = Properties.Settings.Default.SharedConfigFile;
            if (Properties.Settings.Default.LocalConfigFile != "") ac.localCfgFileName = Properties.Settings.Default.LocalConfigFile;
            if (Properties.Settings.Default.StartupPlan != "") ac.startupPlanName = Properties.Settings.Default.StartupPlan;
            if (Properties.Settings.Default.CLIPort != 0) ac.CLIPort = Properties.Settings.Default.CLIPort;

            // overwrite with command line options
            var options = new Options();
            if (CommandLine.Parser.Default.ParseArguments(System.Environment.GetCommandLineArgs(), options))
            {
                if (options.MasterPort != 0) ac.masterPort = options.MasterPort;
                if (options.SharedConfigFile != "") ac.sharedCfgFileName = options.SharedConfigFile;
                if (options.LocalConfigFile != "") ac.localCfgFileName = options.LocalConfigFile;
                if (options.LogFile != "") ac.logFileName = options.LogFile;
                if (options.StartupPlan != "") ac.startupPlanName = options.StartupPlan;
                if (options.CLIPort != 0) ac.CLIPort = options.CLIPort;
                if (options.ParentAgentPid != -1) ac.ParentAgentPid = options.ParentAgentPid;
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

            if( ac.localCfgFileName != "" )
            {
                ac.localCfgFileName = Path.GetFullPath(ac.localCfgFileName);
                log.DebugFormat("Loading local config file '{0}'", ac.localCfgFileName);
                ac.lcfg = new LocalXmlConfigReader().Load(File.OpenText(ac.localCfgFileName));
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

        static bool Initialize()
        {
            try
            {
                var ac = getAppConfig();

                if (AppInstanceAlreadyRunning(ac.masterPort))
                {
                    throw new Exception("Another instance of Dirigent Master is already running!");
                }

                log.InfoFormat("Master running on port {0}", ac.masterPort);

                IEnumerable<ILaunchPlan> planRepo = (ac.scfg != null) ? ac.scfg.Plans : null;
                
                // start a local network-only agent
				// use unique client id to avoid conflict with any other possible client
                string machineId = Guid.NewGuid().ToString();
                var dirigClient = new Dirigent.Net.Client(machineId, "127.0.0.1", ac.masterPort);
                string rootForRelativePaths = System.IO.Path.GetDirectoryName( System.IO.Path.GetFullPath(ac.sharedCfgFileName) );
				bool doNotLaunchReinstaller = ac.ParentAgentPid != -1; // if started by agent, do not reinstall itself (agent will do)
                agent = new Dirigent.Agent.Core.Agent(machineId, dirigClient, false, rootForRelativePaths, doNotLaunchReinstaller);

                // start master server
				var s = new Server(ac.masterPort, agent.Control, planRepo, ac.startupPlanName);
                // server works through its ServerRemoteObject


                dirigClient.Connect(); // connect should succeed immediately (server runs locally)

				// start a telnet client server
                log.InfoFormat("Command Line Interface running on port {0}", ac.CLIPort);
				cliServer = new CLIServer( "0.0.0.0", ac.CLIPort, agent.Control );
				cliServer.Start();

                return true;

            }
            catch (Exception ex)
            {
                log.Error(ex);
                //ExceptionDialog.showException(ex, "Dirigent Exception", "");
                return false;
            }
        }

        static void Run()
        {
            Console.WriteLine("Press Ctr+C to stop the server.");
            // run forever
            while (true)
            {
	            agent.tick();
	            cliServer.Tick();
	            Thread.Sleep(50);
            }
        }

        static Mutex singleInstanceMutex;

        static bool AppInstanceAlreadyRunning(int port)
        {
            bool createdNew;

            singleInstanceMutex = new Mutex(true, String.Format("DirigentMaster_{0}", port), out createdNew);

            if (!createdNew)
            {
                // myApp is already running...
                return true;
            }
            return false;
        }


        static void Main(string[] args)
        {
            if( !Initialize() )
                return;

			while(true)
            {
                try
                {
                    Run();
                }
                catch (RemoteOperationErrorException ex) // an error from another agent received
                {
                    log.Info("RemoteOp error: "+Tools.JustFirstLine(ex.ToString()));
                }
            }

        }

    }
}
