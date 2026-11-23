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
		SecurityConfigError = 5, // security is on and the site secret is missing or invalid; never falls back to open
	}

	static class Program
	{

		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );



		/// <summary>
		/// With security on or audit the site secret has to be there and be valid, or the program does not start - there is no falling back to open.
		/// </summary>
		static bool SecurityLoads( AppConfig ac )
		{
			try
			{
				ac.LoadSecurity();
				return true;
			}
			catch( SecurityConfigException ex )
			{
				log.Error( ex.Message );
				Console.Error.WriteLine( ex.Message );
				return false;
			}
		}

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
				else if( !SecurityLoads( ac ) )
				{
					exitCode = EAppExitCode.SecurityConfigError;
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
