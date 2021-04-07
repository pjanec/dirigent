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

    enum ErrorCode
    {
        OK = 0,
        ConnectFailed = 1,
        ErrorResp = 2 
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


        static ErrorCode NonInteractiveSubCmd( Dirigent.CLI.CommandLineClient client, string subcmd )
        {
            var reqId = client.NewReqId();
			client.SendReq( subcmd, reqId );
            // wait for response
            while(true)
            {
                var resp = client.ReadResp(5000);
                if( string.IsNullOrEmpty(resp) )
                    return ErrorCode.ErrorResp; // error
                            
                string respId;
                string rest;
                if( client.ParseReqIdAndTheRest( resp, out respId, out rest ) )
                {
                    if( string.IsNullOrEmpty(rest))
                        return ErrorCode.ErrorResp; // error

    				Console.WriteLine( rest );

                            
                    if( rest.StartsWith("ERROR") )
                        return ErrorCode.ErrorResp; // error

                    if( rest.StartsWith("ACK") )
                        return ErrorCode.OK;

                    if( rest.StartsWith("END") )
                        return ErrorCode.OK;
                }
                else
                    return ErrorCode.ErrorResp; // error
            }
        }

        // returns error code of the last failed command (or OK if all ok)
        static ErrorCode NonInteractive( Dirigent.CLI.CommandLineClient client, string input )
        {
            var split = input.Split( ';' );
            ErrorCode err = ErrorCode.OK;
            foreach( var subcmd in split )
            {
                if( string.IsNullOrEmpty(subcmd) )
                    continue;
                var subErr = NonInteractiveSubCmd( client, subcmd );
                if( subErr != ErrorCode.OK )
                {
                    err = subErr;
                }
            }
            return err;
        }

        static ErrorCode consoleAppMain( string[] args )
        {
		    ErrorCode errorCode = ErrorCode.OK;

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


                if( ac.nonOptionArgs.Count > 0 ) // non-interactive cmd line; retruns error code 0 if command reply is not error
                {
                    var input = string.Join( " ", ac.nonOptionArgs );
                    errorCode = NonInteractive( client, input );
                }
                else // interactive
                {
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
                    errorCode = ErrorCode.OK;
                }
                client.Dispose();

				return errorCode;

            }
            catch (Exception ex)
            {
                log.Error(ex);
                //Console.WriteLine(string.Format("Error: {0} [{1}]", ex.Message, ex.GetType().ToString()));
                Console.WriteLine(string.Format("Error: {0}", ex.Message));
                //ExceptionDialog.showException(ex, "Dirigent Exception", "");
                return ErrorCode.ConnectFailed;
            }
        }

        static int Main(string[] args)
        {
            return (int) consoleAppMain( args );
            //Console.WriteLine("Press a key to exit the server.");
            //Console.ReadLine();
        }

    }
}
