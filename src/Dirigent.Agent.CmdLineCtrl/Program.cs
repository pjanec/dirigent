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
using Dirigent.Agent.Core;

using log4net;
using log4net.Appender;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace Dirigent.Agent.CmdLineCtrl
{
    // Define a class to receive parsed values
    class Options
    {
        [Option("masterPort", Required = false, DefaultValue = 0, HelpText = "Master's TCP port.")]
        public int MasterPort { get; set; }

        [Option("masterIP", Required = false, DefaultValue = "", HelpText = "Master's IP address.")]
        public string MasterIP { get; set; }

        [Option("sharedConfigFile", Required = false, DefaultValue = "", HelpText = "Shared config file name.")]
        public string SharedConfigFile { get; set; }

        [Option("logFile", Required = false, DefaultValue = "", HelpText = "Log file name.")]
        public string LogFile { get; set; }

        [ValueList(typeof(List<string>))]
        public IList<string> Items { get; set; }


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

        class AppConfig
        {
            public string sharedCfgFileName = "SharedConfig.xml";
            public int masterPort = 5032;
            public string masterIP = "127.0.0.1";
            public string logFileName = "";
            public IList<string> nonOptionArgs = null;
            public SharedConfig scfg = null;
        }

        static AppConfig getAppConfig()
        {
            var ac = new AppConfig();

            // overwrite with application config
            if (Properties.Settings.Default.MasterIP != "") ac.masterIP = Properties.Settings.Default.MasterIP;
            if (Properties.Settings.Default.MasterPort != 0) ac.masterPort = Properties.Settings.Default.MasterPort;
            if (Properties.Settings.Default.SharedConfigFile != "") ac.sharedCfgFileName = Properties.Settings.Default.SharedConfigFile;

            // overwrite with command line options
            var options = new Options();
            if (CommandLine.Parser.Default.ParseArguments(System.Environment.GetCommandLineArgs(), options))
            {
                if (options.MasterIP != "") ac.masterIP = options.MasterIP;
                if (options.MasterPort != 0) ac.masterPort = options.MasterPort;
                if (options.SharedConfigFile != "") ac.sharedCfgFileName = options.SharedConfigFile;
                if (options.LogFile != "") ac.logFileName = options.LogFile;
                ac.nonOptionArgs = options.Items.ToList().GetRange(1, options.Items.Count-1); // strip the executable name
            }

            if (ac.sharedCfgFileName != "")
            {
                ac.sharedCfgFileName = Path.GetFullPath(ac.sharedCfgFileName);
                log.DebugFormat("Loading shared config file '{0}'", ac.sharedCfgFileName);
                ac.scfg = new SharedXmlConfigReader().Load(File.OpenText(ac.sharedCfgFileName));
            }

            return ac;
        }

        static int consoleAppMain()
        {
            try
            {
                var ac = getAppConfig();

                if (ac.logFileName != "")
                {
                    SetLogFileName(Path.GetFullPath(ac.logFileName));
                }

                //var planRepo = getPlanRepo(ac);

                log.InfoFormat("Running with masterIp={0}, masterPort={1}", ac.masterIP, ac.masterPort);

                // use unique client id to avoid conflict with any other possible client
                string machineId = Guid.NewGuid().ToString();
                var client = new Dirigent.Net.Client(machineId, ac.masterIP, ac.masterPort);
                
                // first connect
                client.Connect();
                
                // use network-only agent (never local)
                var agent = new Dirigent.Agent.Core.Agent(machineId, client, false);
                
                // let the agent receive the plan repository from master
                agent.tick();

                // process the console command
                MyCommandRepo cmdRepo = new MyCommandRepo(agent.Control);
                cmdRepo.ParseAndExecute(ac.nonOptionArgs);

                return 0; // everything OK

            }
            catch (Exception ex)
            {
                log.Error(ex);
                //Console.WriteLine(string.Format("Error: {0} [{1}]", ex.Message, ex.GetType().ToString()));
                Console.WriteLine(string.Format("Error: {0}", ex.Message));
                //ExceptionDialog.showException(ex, "Dirigent Exception", "");
                return -1;
            }
        }

        static int Main(string[] args)
        {
            return consoleAppMain();
            //Console.WriteLine("Press a key to exit the server.");
            //Console.ReadLine();
        }

    }
}
