using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;
using System.IO;

using CommandLine;
using CommandLine.Text;

using log4net;
using log4net.Appender;

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace Dirigent.CLI.Telnet
{
    // Define a class to receive parsed values
    class Options
    {
        [Option("masterCLIPort", Required = false, DefaultValue = 0, HelpText = "Master's CLI TCP port.")]
        public int MasterCLIPort { get; set; }

        [Option("masterIP", Required = false, DefaultValue = "", HelpText = "Master's IP address.")]
        public string MasterIP { get; set; }

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
            public int masterCLIPort = 5050;
            public string masterIP = "127.0.0.1";
            public string logFileName = "";
            public IList<string> nonOptionArgs = null;
        }

        static AppConfig getAppConfig()
        {
            var ac = new AppConfig();

            // overwrite with application config
            if (Properties.Settings.Default.MasterIP != "") ac.masterIP = Properties.Settings.Default.MasterIP;
            if (Properties.Settings.Default.MasterCLIPort != 0) ac.masterCLIPort = Properties.Settings.Default.MasterCLIPort;

            // overwrite with command line options
            var options = new Options();
            if (CommandLine.Parser.Default.ParseArguments(System.Environment.GetCommandLineArgs(), options))
            {
                if (options.MasterIP != "") ac.masterIP = options.MasterIP;
                if (options.MasterCLIPort != 0) ac.masterCLIPort = options.MasterCLIPort;
                if (options.LogFile != "") ac.logFileName = options.LogFile;
                ac.nonOptionArgs = options.Items.ToList().GetRange(1, options.Items.Count-1); // strip the executable name
            }


            return ac;
        }


        static int consoleAppMain()
        {
			Dirigent.CLI.CommandLineClient client;
            try
            {
                var ac = getAppConfig();

                if (ac.logFileName != "")
                {
                    SetLogFileName(Path.GetFullPath(ac.logFileName));
                }

                //var planRepo = getPlanRepo(ac);

                log.InfoFormat("Running with masterIp={0}, masterCLIPort={1}", ac.masterIP, ac.masterCLIPort);

                client = new Dirigent.CLI.CommandLineClient(ac.masterIP, ac.masterCLIPort);


				
				bool wantExit = false;
				client.StartAsynResponseReading(
					
					// on response
					(string line) =>
					{
						Console.WriteLine(line);
					},

					// on disconnected
					() =>
					{
						Console.WriteLine("[ERROR]: Disconnected from server!");
						wantExit = true;
					}

				);

				while(!wantExit)
				{
					Console.Write(">");
					var input = Console.ReadLine();
					if(string.IsNullOrEmpty(input) ) break;
					client.SendReq( input );
				}
				
				//// use unique client id to avoid conflict with any other possible client
                //string machineId = Guid.NewGuid().ToString();
                //var client = new Dirigent.Net.Client(machineId, ac.masterIP, ac.masterPort);
                
                //// first connect
                //client.Connect();
                
                //// use network-only agent (never local)
                //string rootForRelativePaths = System.IO.Path.GetDirectoryName( System.IO.Path.GetFullPath(ac.sharedCfgFileName) );
                //var agent = new Dirigent.Agent.Core.Agent(machineId, client, false, rootForRelativePaths);
                
                //// let the agent receive the plan repository from master
                //agent.tick();

                //// process the console command
                //MyCommandRepo cmdRepo = new MyCommandRepo(agent.Control);
                //cmdRepo.ParseAndExecute(ac.nonOptionArgs);

                client.Dispose();

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
