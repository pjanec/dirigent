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
      

      [ParserState]
      public IParserState LastParserState { get; set; }

      [HelpOption]
      public string GetUsage() {
        return HelpText.AutoBuild(this,
          (HelpText current) => HelpText.DefaultParsingErrorsHandler(this, current));
      }
    }

    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // start with default settings
            string sharedCfgFileName = Path.Combine(Application.StartupPath, "SharedConfig.xml");
            //string localCfgFileName = Path.Combine(Application.StartupPath, "LocalConfig.xml");
            string machineId = System.Environment.MachineName;
            int masterPort = 5032;
            string masterIP = "127.0.0.1";

            // overwrite with application config
            if( Properties.Settings.Default.MachineId != "" ) machineId = Properties.Settings.Default.MachineId;
            if( Properties.Settings.Default.MasterIP != "" ) masterIP = Properties.Settings.Default.MasterIP;
            if( Properties.Settings.Default.MasterPort != 0 ) masterPort = Properties.Settings.Default.MasterPort;
            if (Properties.Settings.Default.SharedConfigFile != "") sharedCfgFileName = Properties.Settings.Default.SharedConfigFile;

            // overwrite with command line options
            var options = new Options();
            if (CommandLine.Parser.Default.ParseArguments(System.Environment.GetCommandLineArgs(), options))
            {
                if( options.MachineId != "" ) machineId = options.MachineId;
                if( options.MasterIP != "" ) masterIP = options.MasterIP;
                if( options.MasterPort != 0 ) masterPort = options.MasterPort;
                if (options.SharedConfigFile != "") sharedCfgFileName = options.SharedConfigFile;
            }


            try
            {
                SharedConfig scfg = new SharedXmlConfigReader().Load(File.OpenText(sharedCfgFileName));
                //LocalConfig lcfg = new LocalXmlConfigReader().Load(File.OpenText(localCfgFileName));

                var agent = new Dirigent.Agent.Core.Agent( machineId, masterIP, masterPort );

                Application.Run(new frmMain(agent.getControl(), agent.tick, scfg, machineId));
            }
            catch( Exception ex )
            {
                ExceptionDialog.showException(ex, "Dirigent Exception", "");
            }

        }
    }
}
