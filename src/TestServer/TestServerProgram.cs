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

[assembly: log4net.Config.XmlConfigurator(Watch = true)]

namespace TestServer
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

        static void Main(string[] args)
        {
            Initialize();
            //var s = new Server(12345);
            // server works through its ServerRemoteObject
            Console.WriteLine("Press a key to exit the server.");
            Console.ReadLine();
        }

        static void Initialize()
        {
            // start with default settings
            string sharedCfgFileName = "SharedConfig.xml";
            //string localCfgFileName = Path.Combine(Application.StartupPath, "LocalConfig.xml");
            int masterPort = 5032;

            // overwrite with application config
            if (Properties.Settings.Default.MasterPort != 0) masterPort = Properties.Settings.Default.MasterPort;
            if (Properties.Settings.Default.SharedConfigFile != "") sharedCfgFileName = Properties.Settings.Default.SharedConfigFile;

            // overwrite with command line options
            var options = new Options();
            if (CommandLine.Parser.Default.ParseArguments(System.Environment.GetCommandLineArgs(), options))
            {
                if (options.MasterPort != 0) masterPort = options.MasterPort;
                if (options.SharedConfigFile != "") sharedCfgFileName = options.SharedConfigFile;
            }


            try
            {
                SharedConfig scfg = new SharedXmlConfigReader().Load(File.OpenText(sharedCfgFileName));
                //LocalConfig lcfg = new LocalXmlConfigReader().Load(File.OpenText(localCfgFileName));

                //var client = new Dirigent.Net.Client(machineId, masterIP, masterPort);
                //client.Connect(); // FIXME: add reconnection support!

                log.Info(string.Format("Master running on masterPort={0}", masterPort));

                var s = new Server( masterPort, scfg.Plans );
                // server works through its ServerRemoteObject

            }
            catch (Exception ex)
            {
                log.Error(ex);
                //ExceptionDialog.showException(ex, "Dirigent Exception", "");
            }
        }
    }
}
