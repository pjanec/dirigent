using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;

using Dirigent.Common;

using log4net;

[assembly: log4net.Config.XmlConfigurator( Watch = true )]

namespace Dirigent.Gui.WinForms
{

	public interface App
	{
		/// <summary>
		///  returns exit code
		/// </summary>
		EAppExitCode run();
	}

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
				( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );



		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static int Main()
		{
			EAppExitCode exitCode = EAppExitCode.NoError;

			try
			{
				log.Info( $"Started with cmdLine: {Environment.CommandLine}" );
				var ac = new AppConfig();
				if( ac.HadErrors )
				{
					log.Error( "Error parsing command line arguments.\n" + ac.GetUsageHelpText() );
					exitCode = EAppExitCode.CmdLineError;
				}
				else
				{
					bool runAgent = false;
					bool runGui = true;

					if( string.Equals( ac.Mode, "TrayAgent", StringComparison.OrdinalIgnoreCase )
						||
						string.Equals( ac.Mode, "TrayApp", StringComparison.OrdinalIgnoreCase )
					)
					{
						runAgent = true;
						runGui = false;
					}
					else
					if( string.Equals( ac.Mode, "TrayAgentGui", StringComparison.OrdinalIgnoreCase ) )
					{
						runAgent = true;
						runGui = true;
					}

					App app = new GuiTrayApp( ac, runAgent, runGui );
					exitCode = app.run();
					log.Info( $"Exiting gracefully with exitCode {(int)exitCode} ({exitCode})." );
				}
			}
			catch( Exception ex )
			{
				log.Error( ex );
				exitCode = EAppExitCode.ExceptionError;
			}

			return ( int )exitCode;
		}
	}
}
