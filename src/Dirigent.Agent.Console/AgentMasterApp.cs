﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Dirigent
{

	///<summary>Console app with Agent and/or Master</summary>
	public class AgentMasterApp : Disposable, IApp
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		private AppConfig _ac;

		private Master? _master;
		private Agent? _agent;

		private bool _isMaster;
		private bool _isAgent;
		//private bool _runGui;

		private AlreadyRunningTester _alreadyRunningTester;

        //private ProcRunner? _guiRunner;



		public AgentMasterApp( AppConfig ac, bool isAgent, bool isMaster )
		{
			_isMaster = isMaster;
			_isAgent = isAgent;
			//_runGui = runGui;
			_ac = ac;
			_alreadyRunningTester = new AlreadyRunningTester( ac.MasterIP, ac.MasterPort, ac.MachineId );

			Tools.SetDefaultEnvVars( System.IO.Path.GetDirectoryName( _ac.SharedCfgFileName )! );

		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if( !disposing ) return;

			_master?.Dispose();
			_agent?.Dispose();

			//_guiRunner?.Dispose();
			//_guiRunner = null;

		}

		public EAppExitCode run()
		{
			bool stayRunning = _isMaster || _isAgent;

			if( _isMaster )
			{
				if( !_alreadyRunningTester.IsMasterAlreadyRunning() )
				{
					if( string.IsNullOrEmpty( _ac.SharedCfgFileName ) )
						throw new ConfigurationErrorException("SharedConfig not defined");

					_master = new Master(
						_ac,
						PathUtils.GetRootForRelativePaths( _ac.SharedCfgFileName, _ac.RootForRelativePaths )
					);
					if( !string.IsNullOrEmpty(_ac.StartupScript) ) _master.StartSingletonScript( _ac.MachineId, _ac.StartupScript );
				}
				else
				{
					log.Error( "Another instance of Dirigent Master is already running!" );
				}
			}

			if( _isAgent )
			{
				if( !_alreadyRunningTester.IsAgentAlreadyRunning() )
				{
					_agent = new Agent( _ac );
				}
				else
				{
					log.Error( "Another instance of Dirigent Agent is already running!" );
				}
			}

			//if( _runGui 
			//   && _ac.ParentPid == -1 )	// just if we are NOT launched from GUI
			//{
			//	_guiRunner = new ProcRunner(
			//		"Dirigent.Gui.exe",
			//		"trayGui",
			//		killOnDispose:stayRunning  // if we are just the GUI launcher, do not kill the gui when we exit
			//		);
			//	try
			//	{
			//		{
			//			if( _ac.ParentPid == -1 )
			//			{
			//				_guiRunner.Launch();
			//			}
			//			else
			//			{
			//				_guiRunner.Adopt( _ac.ParentPid );
			//			}

			//			if( stayRunning ) // re-launch crashed gui just if we are staying
			//			{
			//				_guiRunner.StartKeepAlive();
			//			}
			//		}
			//	}
			//	catch (Exception ex)
			//	{
			//		log.Error(ex);
			//		_guiRunner.Dispose();
			//		_guiRunner = null;
			//	}
			//}


			if( stayRunning )
			{
				int numMasterTicksPerMainTick = _ac.TickPeriod / _ac.MasterTickPeriod;
				if( numMasterTicksPerMainTick <= 0 ) numMasterTicksPerMainTick = 1;
				int masterSleep = _ac.TickPeriod / numMasterTicksPerMainTick;
				if( masterSleep <= 0 ) masterSleep = 1;
				int commonSleep = _ac.TickPeriod;
				if( _master is not null ) commonSleep -= numMasterTicksPerMainTick * masterSleep;

				try
				{
					while( true )
					{
						if( _agent is not null )
						{
							_agent.Tick();
							if( _agent.WantsQuit ) break;
						}

						if( _master is not null )
						{
							for( int i=0; i < numMasterTicksPerMainTick; i++)
							{
								_master.Tick();
								Thread.Sleep( masterSleep );
							}

							if( _master.WantsQuit ) break;
						}

						Thread.Sleep( commonSleep );
					}
				}
				catch( Exception ex )
				{
					log.Error("Exception", ex);
				}
			}

			return EAppExitCode.OK;
		}
	}


}
