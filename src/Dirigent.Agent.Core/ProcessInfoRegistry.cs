using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Dirigent
{
	#if Windows
    public class ProcessInfo
    {
        public int PID;
		public UInt64 PercentProcessorTime;
        public UInt64 WorkingSetPrivate;
		public UInt64 TimeStamp_Sys100NS;
		public float CPU; // percent of CPU usage
	}


	/// <summary>
	/// Gather some info about running processes.
	/// Updated asynchronously to avoid blocking the querying thread.
	/// Warning: the update is taking a lot of CPU as it scans all processes!
	/// </summary>
	public class ProcessInfoRegistry : Disposable
	{
		const double UPDATE_PERIOD = 3.0; // do not update frequently, takes a lot of CPU

		DateTime _timeStamp_Prev;
		Dictionary<int, ProcessInfo> _processInfosPrev = new();
		Dictionary<int, ProcessInfo> _processInfos = new();
		CancellationTokenSource _cts; 
		Task _updateTask;
		//Clock _clock;
		Thread _thread;


		//PerformanceCounter _perfTotalCPU = new PerformanceCounter("Process", "% Processor Time", "_Total");


		public ProcessInfoRegistry()
		{
			_cts = new CancellationTokenSource();
			//_updateTask = Task.Run( UpdateLoop, _cts.Token );
			_thread = new Thread( new ThreadStart( UpdateLoop ) );
			_thread.Start();
			//_clock = new Clock();
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if (!disposing) return;

			_cts.Cancel();
			//_updateTask?.Wait();
			_thread?.Join();
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

		async Task UpdateTask()
		{
			try
			{
				while( !_cts.IsCancellationRequested )
				{
					Update();
					await Task.Delay( (int)(UPDATE_PERIOD * 1000), _cts.Token );
				}
			}
			catch (TaskCanceledException)
			{
				// ok
			}
		}
		
		void UpdateLoop()
		{
			while( !_cts.IsCancellationRequested )
			{
				Update();
				for(int i=0; i < UPDATE_PERIOD/1000; i++ )
				{
					Thread.Sleep( 1000 );
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
			var now = DateTime.Now;

			double timeDelta100ns = (now - _timeStamp_Prev).TotalSeconds * 10e7;

			//var totalCpuVal = _perfTotalCPU.NextValue();

			using (var items = new ManagementObjectSearcher( String.Format( "Select PercentProcessorTime, TimeStamp_Sys100NS, WorkingSetPrivate, IDProcess From Win32_PerfFormattedData_PerfProc_Process" ) ).Get())
			{
				foreach (var item in items)
				{
					var pi = new ProcessInfo();
					pi.PID = (int)((UInt32)item["IDProcess"]);
					pi.PercentProcessorTime = (UInt64)item["PercentProcessorTime"];
					//pi.TimeStamp_Sys100NS = (UInt64)item["TimeStamp_Sys100NS"];
					pi.CPU = (float)((double)pi.PercentProcessorTime/Environment.ProcessorCount)*100;
					pi.WorkingSetPrivate = (UInt64)item["WorkingSetPrivate"];
					processInfos[pi.PID] = pi;

					//// find delta from last call
					//if (_processInfos.TryGetValue( pi.PID, out var piPrev ))
					//{
					//	//var procDelta = piPrev.PercentProcessorTime - pi.PercentProcessorTime;
					//	var timeDelta = pi.TimeStamp_Sys100NS - piPrev.TimeStamp_Sys100NS;
					//	pi.CPU = (float)((double)procDelta/timeDelta/Environment.ProcessorCount)*100;
					//}
					//else
					//{
					//	pi.CPU = -1;
					//}
					

				}
			}

			// replace the shared info with new
			lock (this)
			{
				_timeStamp_Prev = now;
				_processInfosPrev = _processInfos;
				_processInfos = processInfos;
			}
		#endif
		}

		public void Tick()
		{
			// no need, we are updating asynchronously via recurring task
		}

	}
	#endif
}
