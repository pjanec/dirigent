using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Dirigent.Common;
using Dirigent.Agent;

namespace Dirigent.Agent
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

					_master = new Master( _ac.LocalIP, _ac.MasterPort, _ac.CliPort, _ac.SharedCfgFileName );
				}
			}

			if( _isAgent )
			{
				if( !_alreadyRunningTester.IsAgentAlreadyRunning() )
				{
					_agent = new Agent( _ac.MachineId, _ac.MasterIP, _ac.MasterPort, _ac.RootForRelativePaths );
				}
			}

			//if( _runGui 
			//   && _ac.ParentPid == -1 )	// just if we are NOT launched from GUI
			//{
			//	_guiRunner = new Common.ProcRunner(
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


			//using (var client = new Dirigent.Net.Client(ac.machineId, ac.masterIP, ac.masterPort, ac.mcastIP, ac.masterPort, ac.localIP, autoConn:true ))
			//{

			//    string rootForRelativePaths = System.IO.Path.GetDirectoryName( System.IO.Path.GetFullPath(ac.sharedCfgFileName) );
			//    var agent = new Dirigent.Agent.Core.Agent(ac.machineId, client, true, rootForRelativePaths, false, AppConfig.BoolFromString(ac.mcastAppStates));


			//    IEnumerable<ILaunchPlan> planRepo = (ac.scfg != null) ? ac.scfg.Plans : null;

			//    // if there is some local plan repo defined, use it for local operations
			//    if (planRepo != null)
			//    {
			//        agent.LocalOps.SetPlanRepo(planRepo);
			//    }

			//    // start given plan if provided
			//    if (planRepo != null)
			//    {
			//        agent.LocalOps.SelectPlan(ac.startupPlanName);
			//    }

			//}

			if( stayRunning )
			{
				try
				{
					while( true )
					{
						if( _master is not null )
						{
							_master.Tick();
							if( _master.WantsQuit ) break;
						}

						if( _agent is not null )
						{
							_agent.Tick();
							if( _agent.WantsQuit ) break;
						}


						Thread.Sleep( _ac.TickPeriod );
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
