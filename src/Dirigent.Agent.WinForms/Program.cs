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

	public interface IApp : IDisposable
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
					bool isMaster = Tools.BoolFromString( ac.IsMaster );

					switch( ac.Mode.ToLower() )
					{
						case "gui":  // just gui, no agent
						{
							runAgent = false;
							runGui = true;
							break;
						}

						// agent + gui
						default:
						case "traygui":  // for compatibility with Dirigent 1.x
						case "trayapp":  // for compatibility with Dirigent 1.x
						case "trayagentgui":
						{
							runAgent = true;
							runGui = true;
							break;
						}

						// just agent (no gui)
						case "daemon":
						case "agent":
						{
							runAgent = true;
							runGui = false;
							break;
						}

						// master only
						case "master":
						{
							runAgent = false;  // master is part of agent executable; if run with "--mode master", just master will run
							runGui = false;
							isMaster = true;
							break;
						}
					}

					IApp app = new GuiTrayApp( ac, runAgent, runGui, isMaster );
					exitCode = app.run();
					app.Dispose();
					log.Debug( $"Exiting gracefully with exitCode {(int)exitCode} ({exitCode})." );
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
