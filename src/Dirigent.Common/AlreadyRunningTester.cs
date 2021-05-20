using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Dirigent.Common
{
	public class AlreadyRunningTester
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		private Mutex? _singleInstanceMutexMaster;
		private Mutex? _singleInstanceMutexAgent;

		private string _masterIP;
		private int _masterPort;
		private string _machineId;

		public AlreadyRunningTester( string masterIP, int masterPort, string machineId )
		{
			_masterIP = masterIP;
			_masterPort = masterPort;
			_machineId = machineId;
		}

		
		public bool IsAgentAlreadyRunning()
		{
			bool createdNew;

			_singleInstanceMutexAgent = new Mutex( true, String.Format( "DirigentAgent_{0}_{1}_{2}", _masterIP, _masterPort, _machineId ), out createdNew );

			if( !createdNew )
			{
				// myApp is already running...
				log.Error( "Another instance of Dirigent Agent is already running!" );
				return true;
			}
			return false;
		}

		public bool IsMasterAlreadyRunning()
		{
			bool createdNew;

			_singleInstanceMutexMaster = new Mutex( true, String.Format( "DirigentMaster_{0}_{1}_{2}", _masterIP, _masterPort, _machineId ), out createdNew );

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
