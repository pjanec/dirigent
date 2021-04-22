using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Dirigent.Common;
using Dirigent.Agent;

namespace Dirigent.Agent
{
	public class CliApp : App
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		private AppConfig _ac;

		//private Master _master;
		//private Agent _agent;
		private bool _interactive = false;

		public CliApp( AppConfig ac, bool interactive )
		{
			this._ac = ac;
			_interactive = interactive;
		}

		public EAppExitCode run()
		{
			if( _interactive )
			{
				log.Debug( "Running in interactive CLI mode" );
			}
			else
			{
				log.Debug( "Running in non-interactive CLI mode" );

				if( _ac.NonOptionArgs.Count > 0 ) // non-interactive cmd line; retruns error code 0 if command reply is not error
				{
					//var input = string.Join( " ", ac.nonOptionArgs );
					//errorCode = NonInteractive( client, input );
				}
				else // non-interactive but no params
				{
					log.Error( "No commands passed on the command line!" );
					return EAppExitCode.CmdLineError;
				}
			}

			return EAppExitCode.NoError;
		}
	}


}
