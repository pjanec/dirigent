using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;

using log4net;
using ImGuiNET;

[assembly: log4net.Config.XmlConfigurator( Watch = true )]

namespace Dirigent.Gui
{

	public enum EAppExitCode
	{
		NoError = 0,
		AlreadyRunning = 1,
		CmdLineError = 2,
		ExceptionError = 3,
	}

	static class Program
	{

		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );



		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		static int Main()
		{
			EAppExitCode exitCode = EAppExitCode.NoError;

			//try
			{
				log.Info( $"Started with cmdLine: {Environment.CommandLine}" );
				var ac = new AppConfig();
				// --help and --version: the parser has written the text already, so there is
				// nothing to add and nothing wrong - answer them and do not start
				if( ac.HelpOrVersionRequested )
				{
					ac.WriteHelpOrVersion();
					exitCode = EAppExitCode.NoError;
				}
				else if( ac.HadErrors )
				{
					log.Error( "Error parsing command line arguments.\n" + ac.GetUsageHelpText() );
					exitCode = EAppExitCode.CmdLineError;
				}
				else
				{
					switch( ac.Mode )
					{
						default:	
						case "Gui":
						{
							var app = new GuiApp( ac );
							exitCode = app.run();
							app.Dispose();
							break;
						}

						case "AllInOneDebug":
						{
							var app = new AllInOneDebugApp( ac );
							exitCode = app.run();
							app.Dispose();
							break;
						}
					}
					log.Debug( $"Exiting gracefully with exitCode {(int)exitCode} ({exitCode})." );
				}
			}
			//catch( Exception ex )
			//{
			//	log.Error( ex );
			//	exitCode = EAppExitCode.ExceptionError;
			//}

			return ( int )exitCode;
		}
	}
}
