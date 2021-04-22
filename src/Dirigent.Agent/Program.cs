﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;

using Dirigent.Common;

using log4net;

[assembly: log4net.Config.XmlConfigurator( Watch = true )]

namespace Dirigent.Agent
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
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );



		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		static int Main()
		{

            #if Windows
				Console.WriteLine("Windows!");
            #endif

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
					App? app = null;

					switch( ac.Mode.ToLower() )
					{
						// agent with optionl master?
						case "traygui":  // for compatibility with Dirigent 1.x
						case "trayapp":  // for compatibility with Dirigent 1.x
						case "daemon":
						case "agent":
						{
							app = new AgentMasterApp( ac, isAgent: true, isMaster: Tools.BoolFromString( ac.IsMaster ) );
							break;
						}

						// master only?
						case "master":
						{
							app = new AgentMasterApp( ac, isAgent: false, isMaster: true );
							break;
						}


						case "":
						case "cli":
						{
							app = new CliApp( ac, interactive: false );
							break;
						}

						case "telnet":
						{
							app = new CliApp( ac, interactive: true );
							break;
						}

						default:
						{
							log.Error( $"Invalid app mode '{ac.Mode}'" );
							break;
						}

					}

					if( app != null )
					{
						exitCode = app.run();
					}

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