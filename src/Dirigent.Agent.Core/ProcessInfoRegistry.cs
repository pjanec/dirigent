using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
#if Windows
using System.Management;
#endif
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dirigent
{
    public class ProcessInfo
    {
        public int PID;
		public UInt64 PercentProcessorTime;
        public UInt64 WorkingSetPrivate;
		public float CPU; // percent of CPU usage
	}


	/// <summary>
	/// Gather some info about running processes.
	/// Updated asynchronously in separate thread to avoid blocking the querying thread.
	/// Warning: each update is taking long time as it scans all processes!
	/// </summary>
	public class ProcessInfoRegistry : Disposable
	{
		// logger
		private static readonly log4net.ILog Log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		const double UPDATE_PERIOD = 3.0; // do not update too often, takes a lot of CPU
		CancellationTokenSource _cts; 
		Thread _thread;

		Dictionary<int, ProcessInfo> _processInfos = new();
		float _totalCpuUsage = 0; // percents

		public ProcessInfoRegistry()
		{
			_cts = new CancellationTokenSource();
			_thread = new Thread( new ThreadStart( UpdateLoop ) );
			_thread.IsBackground = true; // do not block app exit
			_thread.Priority = ThreadPriority.BelowNormal;
			_thread.Start();
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if (!disposing) return;

			_cts.Cancel();
			//_thread?.Join(); // we do not need to block, the thread will exit on its own
		}

		public ProcessInfo? GetProcessInfo( int pid )
		{
			ProcessInfo? pi;
			lock(this)
			{
				if (_processInfos.TryGetValue( pid, out pi ))
				{
					return pi;
				}
				else
				{
					return null;
				}
			}
		}

		public float GetTotalCpuUsage()
		{
			lock(this)
			{
				return _totalCpuUsage;
			}
		}

		void UpdateLoop()
		{
			while( !_cts.IsCancellationRequested )
			{
				try
				{
					Update();
				}
				catch( Exception ex )
				{
					Log.Error( "ProcessInfoRegistry.Update() failed.", ex );
					
					// stops further attempts
					break; 
				}

				const double sleepTimeSec = 0.1;
				for (int i=0; i < UPDATE_PERIOD/sleepTimeSec; i++ )
				{
					Thread.Sleep( (int)(sleepTimeSec*1000) );
					if (_cts.IsCancellationRequested) break;
				}
			}
		}

		void Update()
		{
		#if Windows
			// using know how from https://stackoverflow.com/a/11565773
			// (part of https://stackoverflow.com/questions/11523150/how-do-you-monitor-the-cpu-utilization-of-a-process-using-powershell)
			
			var processInfos = new Dictionary<int, ProcessInfo>();

			float totalCpu = 0;
			
			using (var items = new ManagementObjectSearcher( String.Format( "Select PercentProcessorTime, WorkingSetPrivate, IDProcess From Win32_PerfFormattedData_PerfProc_Process" ) ).Get())
			{
				foreach (var item in items)
				{
					var pi = new ProcessInfo();
					pi.PID = (int)((UInt32)item["IDProcess"]);
					pi.PercentProcessorTime = (UInt64)item["PercentProcessorTime"];
					pi.CPU = (float)pi.PercentProcessorTime/Environment.ProcessorCount;
					pi.WorkingSetPrivate = (UInt64)item["WorkingSetPrivate"];
					processInfos[pi.PID] = pi;

					if( pi.PID >= 8 )  // processes under 8 are various system/idle processes
					{
						totalCpu += pi.CPU;
					}
				}
			}

			// replace the shared info with new
			lock (this)
			{
				_processInfos = processInfos;
				_totalCpuUsage = totalCpu;
			}
		#else
			// LINUX: TODO!
		#endif
		}

		public void Tick()
		{
			// no need, we are updating asynchronously in separate thread
		}

	}
}
