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
	public class AgentMasterApp : Disposable, App
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		private AppConfig _ac;

		private Master? _master;
		private Agent? _agent;

		private bool _isMaster;
		private bool _isAgent;


		public AgentMasterApp( AppConfig ac, bool isAgent, bool isMaster )
		{
			_isMaster = isMaster;
			_isAgent = isAgent;
			_ac = ac;
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if( !disposing ) return;

			_master?.Dispose();
			_agent?.Dispose();
		}

		public EAppExitCode run()
		{
			if( _isMaster )
			{
				if( !IsMasterAlreadyRunning() )
				{
					if( _ac.SharedConfig is null )
						throw new ConfigurationErrorException("SharedConfig not defined");

					_master = new Master( _ac.LocalIP, _ac.MasterPort, _ac.CliPort, _ac.SharedConfig );
				}
			}

			if( _isAgent )
			{
				if( !IsAgentAlreadyRunning() )
				{
					_agent = new Agent( _ac.MachineId, _ac.MasterIP, _ac.MasterPort, _ac.RootForRelativePaths );
				}
			}


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

			_agent?.Dispose();
			_master?.Dispose();

			return EAppExitCode.OK;
		}

		private Mutex? _singleInstanceMutexMaster;
		private Mutex? _singleInstanceMutexAgent;

		bool IsAgentAlreadyRunning()
		{
			bool createdNew;

			_singleInstanceMutexAgent = new Mutex( true, String.Format( "DirigentAgent_{0}_{1}_{2}", _ac.MasterIP, _ac.MasterPort, _ac.MachineId ), out createdNew );

			if( !createdNew )
			{
				// myApp is already running...
				log.Error( "Another instance of Dirigent Agent is already running!" );
				return true;
			}
			return false;
		}

		bool IsMasterAlreadyRunning()
		{
			bool createdNew;

			_singleInstanceMutexMaster = new Mutex( true, String.Format( "DirigentMaster_{0}_{1}_{2}", _ac.MasterIP, _ac.MasterPort, _ac.MachineId ), out createdNew );

			if( !createdNew )
			{
				// myApp is already running...
				log.Error( "Another instance of Dirigent Master is already running!" );
				return true;
			}
			return false;
		}
	}


}
