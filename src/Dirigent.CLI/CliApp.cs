using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Dirigent;

namespace Dirigent
{
	public class CliApp : Disposable, IApp
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		private AppConfig _ac;

		//private Master _master;
		//private Agent _agent;
		private bool _interactive = false;

        Dirigent.CLI.CommandLineClient _client;

		public CliApp( AppConfig ac, bool interactive )
		{
			this._ac = ac;
			_interactive = interactive;

			Tools.SetDefaultEnvVars( System.IO.Path.GetDirectoryName( _ac.SharedCfgFileName ) );

            _client = new Dirigent.CLI.CommandLineClient( _ac.MasterIP, _ac.CliPort );
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if( !disposing ) return;

			_client.Dispose();
		}

		public EAppExitCode run()
		{
			EAppExitCode errorCode = EAppExitCode.OK;
			if( _interactive )
			{
				log.Debug( "Running in interactive CLI mode" );
				errorCode = Interactive();
			}
			else
			{
				log.Debug( "Running in non-interactive CLI mode" );

				if( _ac.NonOptionArgs.Count > 0 ) // non-interactive cmd line; retruns error code 0 if command reply is not error
				{
					var input = string.Join( " ", _ac.NonOptionArgs );
					errorCode = NonInteractive( input );
				}
				else // non-interactive but no params
				{
					log.Error( "No commands passed on the command line!" );
					errorCode = EAppExitCode.CmdLineError;
				}
			}

			return errorCode;
		}


        EAppExitCode Interactive()
        {
			bool wantExit = false;
			_client.StartAsynResponseReading(
					
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
				_client.SendReq( input );
			}
            return EAppExitCode.OK;
        }

        EAppExitCode NonInteractiveSubCmd( string subcmd )
        {
            var reqId = _client.NewReqId();
			_client.SendReq( subcmd, reqId );
            // wait for response
            while(true)
            {
                var resp = _client.ReadResp(5000);
                if( string.IsNullOrEmpty(resp) )
                    return EAppExitCode.ErrorResp; // error
                            
                string respId;
                string rest;
                if( _client.ParseReqIdAndTheRest( resp, out respId, out rest ) )
                {
                    if( string.IsNullOrEmpty(rest))
                        return EAppExitCode.ErrorResp; // error

    				Console.WriteLine( rest );

                            
                    if( rest.StartsWith("ERROR") )
                        return EAppExitCode.ErrorResp; // error

                    if( rest.StartsWith("ACK") )
                        return EAppExitCode.OK;

                    if( rest.StartsWith("END") )
                        return EAppExitCode.OK;
                }
                else
                    return EAppExitCode.ErrorResp; // error
            }
        }

        // returns error code of the last failed command (or OK if all ok)
        EAppExitCode NonInteractive( string input )
        {
            var split = input.Split( ';' );
            EAppExitCode err = EAppExitCode.OK;
            foreach( var subcmd in split )
            {
                if( string.IsNullOrEmpty(subcmd) )
                    continue;
                var subErr = NonInteractiveSubCmd( subcmd );
                if( subErr != EAppExitCode.OK )
                {
                    err = subErr;
                }
            }
            return err;
        }

	}


}
