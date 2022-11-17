using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;

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

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool AllocConsole();


		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static int Main()
		{
			EAppExitCode exitCode = EAppExitCode.NoError;

			// debugging feature - shows console and waits for pressing Return before continuing with whatever else
			if( Environment.CommandLine.Contains("--attachdebugger") )
			{
				AllocConsole();
				Console.WriteLine("Attach the debugger and press Return to continue...");
				Console.ReadLine();
			}

			IApp app = null;
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
						// just gui, no agent
						case "remoteControlGui":  // for compatibility with Dirigent 1.x
						case "gui":  
						{
							runAgent = false;
							runGui = true;
							break;
						}

						// agent + gui
						default:
						case "agent":
						case "traygui":  // for compatibility with Dirigent 1.x
						case "trayapp":  // for compatibility with Dirigent 1.x
						{
							runAgent = true;
							runGui = true;
							break;
						}

						// just agent (no gui)
						case "daemon":
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

					app = new GuiTrayApp( ac, runAgent, runGui, isMaster );
					exitCode = app.run();
					app.Dispose();
					log.Debug( $"Exiting gracefully with exitCode {(int)exitCode} ({exitCode})." );
				}
			}
			catch( Exception ex )
			{
				log.Error( ex );
				ExceptionDialog.showExceptionWithStackTrace(ex, "Exception", "");
				app?.Dispose();
				exitCode = EAppExitCode.ExceptionError;
			}

			return ( int )exitCode;
		}
	}
}
