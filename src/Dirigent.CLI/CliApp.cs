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

        /// <summary>
        /// How long to wait for the next line of an answer, in milliseconds. Silence for longer than
        /// this is taken as a failure - of the connection, or of the master.
        /// </summary>
        const int _readTimeoutMs = 5000;

        EAppExitCode NonInteractiveSubCmd( string subcmd )
        {
            // What ends this command's answer, as the command itself declares it: ACK for almost
            // everything, END for the listings and for a command that acknowledges first and finishes
            // later. Without asking, a waiting command would be over at its ACK - which says only
            // that the master accepted it - and this would report success before the work had begun.
            var terminator = TerminatorOf( subcmd );

            // Waiting for such a command is unbounded on purpose: it takes as long as the work takes,
            // and the bound belongs where the work is - WaitForScript has a timeout= of its own. A
            // broken connection still ends the wait, because the read fails rather than going quiet.
            var readTimeoutMs = terminator == ETerminator.End
                                    ? System.Threading.Timeout.Infinite
                                    : _readTimeoutMs;

            var reqId = _client.NewReqId();
            log.Debug( $"Sent: [{reqId}] {subcmd}" );
            _client.SendReq( subcmd, reqId );

            // wait for response
            while( true )
            {
                var resp = _client.ReadResp( readTimeoutMs );
                if( string.IsNullOrEmpty( resp ) )
                    return EAppExitCode.ErrorResp; // error

                log.Debug( $"Recv: {resp}" );

                string respId;
                string rest;
                if( !_client.ParseReqIdAndTheRest( resp, out respId, out rest ) )
                    return EAppExitCode.ErrorResp; // error

                if( string.IsNullOrEmpty( rest ) )
                    return EAppExitCode.ErrorResp; // error

                Console.WriteLine( rest );

                if( rest.StartsWith( "ERROR" ) )
                    return EAppExitCode.ErrorResp; // error

                if( rest.StartsWith( "ACK" ) && terminator == ETerminator.Ack )
                    return EAppExitCode.OK;

                if( rest.StartsWith( "END" ) )
                    return EAppExitCode.OK;

                // anything else is on the way to the terminator: a line of a listing, or the ACK of
                // a command that answers again when it is done
            }
        }

        /// <summary>
        /// The line that ends the answer to a command, as its class declares it.
        /// </summary>
        static ETerminator TerminatorOf( string subcmd )
        {
            var trimmed = subcmd.Trim();
            if( trimmed.Length == 0 ) return ETerminator.Ack;

            var name = trimmed.Split( new char[] { ' ', '	' }, 2 )[0];
            return DirigentCommandRegistrator.TerminatorOf( name );
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
