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
using System.Diagnostics;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace Dirigent.Master
{
    // Define a class to receive parsed values
    class Options
    {
        [Option("masterPort", Required = false, Default = 0, HelpText = "Master's TCP port.")]
        public int MasterPort { get; set; }

        [Option("mcastIP", Required = false, Default = "", HelpText = "Multicast IP")]
        public string McastIP { get; set; }

        [Option("localIP", Required = false, Default = "", HelpText = "Local adapter IP to bind to when multicasting")]
        public string LocalIP { get; set; }

        [Option("mcastAppStates", Required = false, Default = "", HelpText = "Use multical for sharing app states among agents.")]
        public string McastAppStates { get; set; }

        [Option("sharedConfigFile", Required = false, Default = "", HelpText = "Shared config file name.")]
        public string SharedConfigFile { get; set; }

        [Option("localConfigFile", Required = false, Default = "", HelpText = "Local config file name.")]
        public string LocalConfigFile { get; set; }

        [Option("logFile", Required = false, Default = "", HelpText = "Log file name.")]
        public string LogFile { get; set; }

        [Option("startupPlan", Required = false, Default = "", HelpText = "Plan to be started on startup.")]
        public string StartupPlan { get; set; }

        [Option("CLIPort", Required = false, Default = 0, HelpText = "Master's Command Line Interface TCP port.")]
        public int CLIPort { get; set; }

        [Option("ParentAgentPid", Required = false, Default = -1, HelpText = "PID of agent that is running this Dirigent (-1 = standalone)")]
        public int ParentAgentPid { get; set; }

        [Option("tickPeriod", Required = false, Default = 0, HelpText = "Refresh period in msec.")]
        public int TickPeriod { get; set; }

        [Option("CLITickPeriod", Required = false, Default = 0, HelpText = "CLI server refresh period in msec.")]
        public int CLITickPeriod { get; set; }

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
        static int ParentPID = -1;

        static ParserResult<Options> _parserResult;
			
		class AppConfig
        {
            // start with default settings
            public string sharedCfgFileName = "SharedConfig.xml";
            public string localCfgFileName = ""; // empty by default - we won't try to load it
            public int masterPort = 5032;
            public string mcastIP = "239.121.121.121";
            public string localIP = "0.0.0.0";
            public string mcastAppStates = "0";
            public string logFileName = "";
            public string startupPlanName = "";
            public SharedConfig scfg = null;
            public LocalConfig lcfg = null;
            public int CLIPort = 5033;
            public int ParentAgentPid = -1;  // are we startd from an agent, i.e. not standalone?
            public int tickPeriod = 500; // msec
            public int CLITickPeriod = 50; // msec

            public static bool BoolFromString( string boolString )
            {
                return (new List<string>() { "1", "YES", "Y", "TRUE" }.Contains(boolString.ToUpper()));
            }
        }

        static AppConfig getAppConfig()
        {
            var ac = new AppConfig();

            // overwrite with application config
            if (Properties.Settings.Default.MasterPort != 0) ac.masterPort = Properties.Settings.Default.MasterPort;
            if (Properties.Settings.Default.McastIP != "") ac.mcastIP = Properties.Settings.Default.McastIP;
            if (Properties.Settings.Default.LocalIP != "") ac.localIP = Properties.Settings.Default.LocalIP;
            if (Properties.Settings.Default.McastAppStates != "") ac.mcastAppStates = Properties.Settings.Default.McastAppStates;
            if (Properties.Settings.Default.SharedConfigFile != "") ac.sharedCfgFileName = Properties.Settings.Default.SharedConfigFile;
            if (Properties.Settings.Default.LocalConfigFile != "") ac.localCfgFileName = Properties.Settings.Default.LocalConfigFile;
            if (Properties.Settings.Default.StartupPlan != "") ac.startupPlanName = Properties.Settings.Default.StartupPlan;
            if (Properties.Settings.Default.CLIPort != 0) ac.CLIPort = Properties.Settings.Default.CLIPort;
            if (Properties.Settings.Default.TickPeriod != 0) ac.tickPeriod = Properties.Settings.Default.TickPeriod;
            if (Properties.Settings.Default.CLITickPeriod != 0) ac.CLITickPeriod = Properties.Settings.Default.CLITickPeriod;

            // overwrite with command line options

            _parserResult = CommandLine.Parser.Default.ParseArguments<Options>(System.Environment.GetCommandLineArgs());

            _parserResult.WithParsed<Options>( (Options options) =>
            {
                if (options.McastIP != "") ac.mcastIP = options.McastIP;
                if (options.LocalIP != "") ac.localIP = options.LocalIP;
                if (options.McastAppStates != "") ac.mcastAppStates = options.McastAppStates;
                if (options.MasterPort != 0) ac.masterPort = options.MasterPort;
                if (options.SharedConfigFile != "") ac.sharedCfgFileName = options.SharedConfigFile;
                if (options.LocalConfigFile != "") ac.localCfgFileName = options.LocalConfigFile;
                if (options.LogFile != "") ac.logFileName = options.LogFile;
                if (options.StartupPlan != "") ac.startupPlanName = options.StartupPlan;
                if (options.CLIPort != 0) ac.CLIPort = options.CLIPort;
                if (options.ParentAgentPid != -1) ac.ParentAgentPid = options.ParentAgentPid;
                if (options.TickPeriod != 0) ac.tickPeriod = options.TickPeriod;
                if (options.CLITickPeriod != 0) ac.CLITickPeriod = options.CLITickPeriod;
            });

            if (ac.logFileName != "")
            {
                SetLogFileName(Path.GetFullPath(ac.logFileName));
            }

            if( ac.sharedCfgFileName != "" )
            {
                ac.sharedCfgFileName = Path.GetFullPath(ac.sharedCfgFileName);
                log.InfoFormat("Loading shared config file '{0}'", ac.sharedCfgFileName);
                ac.scfg = new SharedXmlConfigReader().Load(File.OpenText(ac.sharedCfgFileName));
            }

            if( ac.localCfgFileName != "" )
            {
                ac.localCfgFileName = Path.GetFullPath(ac.localCfgFileName);
                log.InfoFormat("Loading local config file '{0}'", ac.localCfgFileName);
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

        
        static AppConfig conf;

        static bool Initialize()
        {
            try
            {
                var ac = getAppConfig();
                conf = ac;

                if (AppInstanceAlreadyRunning(ac.masterPort))
                {
                    throw new Exception("Another instance of Dirigent Master is already running!");
                }

                log.InfoFormat("Master running on port {0}", ac.masterPort);

                IEnumerable<ILaunchPlan> planRepo = (ac.scfg != null) ? ac.scfg.Plans : null;
                
                // start a local network-only agent
				// use unique client id to avoid conflict with any other possible client
                string machineId = Guid.NewGuid().ToString();
                var dirigClient = new Dirigent.Net.Client(machineId, "127.0.0.1", ac.masterPort, ac.mcastIP, ac.masterPort, ac.localIP);
                string rootForRelativePaths = System.IO.Path.GetDirectoryName( System.IO.Path.GetFullPath(ac.sharedCfgFileName) );
				bool doNotLaunchReinstaller = ac.ParentAgentPid != -1; // if started by agent, do not reinstall itself (agent will do)
                agent = new Dirigent.Agent.Core.Agent(machineId, dirigClient, false, rootForRelativePaths, doNotLaunchReinstaller, AppConfig.BoolFromString(ac.mcastAppStates));

                // start master server
				var s = new Server(ac.masterPort, agent.Control, planRepo, ac.startupPlanName);
                // server works through its ServerRemoteObject


                dirigClient.Connect(); // connect should succeed immediately (server runs locally)

				// start a telnet client server
                log.InfoFormat("Command Line Interface running on port {0}", ac.CLIPort);
				cliServer = new CLIServer( "0.0.0.0", ac.CLIPort, agent.Control );
				cliServer.Start();

                ParentPID = ac.ParentAgentPid;

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

            int numCliTicksPerMainTick = conf.tickPeriod / conf.CLITickPeriod;
            if( numCliTicksPerMainTick <= 0 ) numCliTicksPerMainTick = 1;
            int cliSleep = conf.tickPeriod / numCliTicksPerMainTick;
            if( cliSleep <= 0 ) cliSleep = 1;
            int extraSleep = conf.tickPeriod - numCliTicksPerMainTick * cliSleep;

            
            // run forever
            while (true)
            {
	            agent.tick();

                // multiple cli ticks per one main tick
                for( int i=0; i < numCliTicksPerMainTick; i++)
                {
	                cliServer.Tick();
                    Thread.Sleep( cliSleep );
                }

                if( parentExited() ) break;
	            
                Thread.Sleep( extraSleep ); // this is already included
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

			while(!parentExited())
            {
                try
                {
                    Run();
                    Console.WriteLine("Terminating...");
                }
                catch (RemoteOperationErrorException ex) // an error from another agent received
                {
                    log.Info("RemoteOp error: "+Tools.JustFirstLine(ex.ToString()));
                }
            }

        }

        static bool parentExited()
        {
            if( ParentPID != -1 )
            {
                try
                {
                    var p = Process.GetProcessById( ParentPID );
                    return false;
                }
                catch( ArgumentException  )
                {
                    return true;
                }
            }
            return false; // we don't have a parent, act as if it still runninh (avoid terminating the master)

        }

    }
}
