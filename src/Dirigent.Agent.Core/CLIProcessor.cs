using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Dirigent
{
	public interface ICLIClient
	{
		string Name { get; }
		void WriteResponse( string text );
	}

	/// <summary>
	/// Command line TCP server accepting multiple simultaneous clients.
	/// Accepts single text line based requests from clients;
	/// For each requests sends back one or more status replies depending on the command type.
	/// Each request is optinally marked with request id which is the used to mark appropriate response.
	/// Requests are buffered and processed sequenially, response may come later.
	/// Clients do not need to wait for a response before sending another request.
	/// </summary>
	/// <remarks>
	/// Request line format:
	///   [optional-req-id] request text till the end of line \a
	///   
	/// Response line format:
	///   [req-id] response text till the end of line \a
	/// 
	/// Request commands
	///   StartPlan planName .... starts given plan, i.e. start launching apps
	///   StopPlan planName ..... stops starting next applications from the plan
	///   KillPlan planName ..... kills given plans (kills all its apps)
	///   RestartPlan planName .. stops all apps and starts the plan again
	///    
	///   LaunchApp appId ....... starts given app
	///   KillApp appId ......... kills given app
	///   RestartApp appId ...... restarts given app
	///   
	///   GetPlanState planName  returns the status of given plan
	///   GetAppState planName   returns the status of given app
	///   
	///   GetAllPlansState	..... returns one line per plan; last line will be "END\n"
	///   GetAllAppsState ...... returns one line per application; last line will be "END\n"
	/// 
	/// 
	/// Response text for GetPlanState
	///   PLAN:planName:None
	///   PLAN:planName:InProgress
	///   PLAN:planName:Failure
	///   PLAN:planName:Success
	///   PLAN:planName:Killing
	///    
	/// Response text for GetAppState
	///   APP:AppName:Flags:ExitCode:StatusAge:CPU:GPU:Memory
	///   
	///   Flags
	///     Each letter represents one status flag. If letter is missing, flag is cleared.
	///	      S = started
	///	      F = start failed
	///	      R = running
	///	      K = killed
	///	      I = initialized
	///	      P = plan applied
	///   
	///   ExitCode = integer number	if exit code (valid only if aff has exited, i.e. Started but not Running)
	///   StatusAge = Number of seconds since last update of the app state
	///   CPU = integer percentage of CPU usage
	///   GPU = integer percentage of GPU usage
	///   Memory = integer number of MBytes used
	/// 
	/// Response text for other commands
	///   ACK ... command reception was acknowledged, command was issued
	///   ERROR: error text here
	///   END ..... ends the list in case the command is expected multiple line response
	///   
	/// </remarks>
	/// <example>
	///   Request:   "[001] StartPlan plan1"
	///	  Response:	 "[001] OK"
	///
	///   Leaving out the request id
	///   Request:   "KillPlan plan2"
	///	  Response:	 "ACK"
	///	
	///   Leaving out the request id
	///   Request:   "KillPlan invalidPlan1"
	///	  Response:	 "ERROR: Plan 'invalidPlan1' does not exist"
	///	
	///   Starting an application
	///   Request:   "[002] StartApp m1.a1"
	///	  Response:	 "[002] ACK"
	///
	///   Getting plan status
	///   Request:   "[003] GetPlanStatus plan1"
	///	  Response:	 "[003] PLAN:plan1:InProgress
	///   
	///   Request:   "GetAppStatus m1.a1"
	///	  Response:	 "APP:m1.a1:SIP:255:2018-06-27_13-02-20.345"
	///	                  
	///   Setting variable or variables
	///   Request:   "[002] SetVars VAR1=VALUE1::VAR2=VALUE2"
	///	  Response:	 "[002] ACK"
	///
	/// </example>
	public class CLIProcessor : Disposable
	{
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

		private Master ctrl;

		List<CLIRequest> pendingRequests = new List<CLIRequest>();

		public CLIProcessor( Master ctrl )
		{
			this.ctrl = ctrl;
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if( !disposing ) return;

			// kill all pending requests
			foreach( var r in pendingRequests )
			{
				r.Dispose();
			}
			pendingRequests.Clear();
		}

		/// <summary>
		/// Call this to accept pending connections and process requests
		/// </summary>
		public void Tick()
		{
			// tick all pending requests
			// remove finished requests
			TickRequests();
		}

		public void AddRequest( ICLIClient c, string cmdLine )
		{
			log.DebugFormat("{0}: CLI Request: {1}", c.Name,  cmdLine );
			var r = new CLIRequest( c, ctrl, cmdLine );
			if( !r.Finished ) // parsed succesfully?
			{
				pendingRequests.Add( r );
			}
		}

		void TickRequests()
		{
			var toRemove = new List<CLIRequest>();
			foreach( var r in pendingRequests )
			{
				r.Tick();
				if( r.Finished ) toRemove.Add( r );
			}

			foreach( var r in toRemove )
			{
				r.Dispose();
				pendingRequests.Remove( r );
			}
		}


	}
}
