using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace Dirigent.Agent
{
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
	public partial class TelnetServer : Common.Disposable
	{
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

		private string localIPstr;
		private int port;
		private Master ctrl;
		TcpListener? server = null;
		CLIProcessor _cliProc;

		// describes a client that connected
		private class TClient : Common.Disposable, ICLIClient
		{
	        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

			public TcpClient client; // what client sent this request, what client to respond to
			NetworkStream ns;
			StringBuilder buf; // for reading line from partial messages
			public delegate void LineReadDeleg( string line ); // called when a line is read
			public event LineReadDeleg? LineRead;

			public TClient( TcpClient client )
			{
				this.client = client;
				ns = client.GetStream();
				buf = new StringBuilder();
			}

			public string Name
			{
				get { return client.Client.RemoteEndPoint.ToString(); }
			}

			// reads input data if avalable, cadd ProcesLine if a completely line found
			public void Tick()
			{
				// read data
				if( client.ReceiveBufferSize > 0 && ns.DataAvailable )
				{
					var bytes = new byte[client.ReceiveBufferSize];
					int numRead = ns.Read( bytes, 0, client.ReceiveBufferSize );
					string msg = Encoding.UTF8.GetString( bytes, 0, numRead ); //the message incoming

					buf.Append( msg );

					// parse to individual lines
					while( true )
					{
						var s = buf.ToString();
						int pos = s.IndexOf( '\n' );
						if( pos < 0 ) break;
						var fullLine = s.Substring( 0, pos ); // do not include the \n
						if( LineRead != null ) LineRead( fullLine );
						buf.Remove( 0, pos + 1 ); // remove the already processe line, skip also the \n
					}
				}
			}

			public bool Connected
			{
				get { return client.Connected; }
			}

			protected override void Dispose(bool disposing)
			{
				base.Dispose(disposing);
				if(!disposing) return;
				client.Close();
				LineRead = null;
			}

			// write back to the client	the string as-in (no adding LF at the end)
			public void WriteResponse( string buf )
			{
				var bytes = Encoding.UTF8.GetBytes( buf );
				try
				{
					ns.Write( bytes, 0, bytes.Length );
				}
				catch( Exception )
				{
					// client is probably already disconnected if we can't write...
				}
			}

		}

		List<TClient> clients = new();

		public TelnetServer( string localIPstr, int port, CLIProcessor cliProc )
		{
			this.localIPstr = localIPstr;
			this.port = port;
			_cliProc = cliProc;
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if( !disposing ) return;
			Stop();
		}

		/// <summary>
		/// Call this to accept pending connections and process requests
		/// </summary>
		public void Tick()
		{
			// check for new connection and create new client
			if( server.Pending() )
			{
				AcceptNewConnection();
			}

			// read requests from clients
			// add new client requests to pendingRequests
			// check for disconnection and remove old clients
			TickClients();
		}

		void AcceptNewConnection()
		{
			// add client to list
			TcpClient client = server.AcceptTcpClient();
			var c = new TClient( client );
			c.LineRead += cmdLine => { AddRequest( c, cmdLine ); };
			clients.Add( c );
		}

		void AddRequest( TClient c, string cmdLine )
		{
			_cliProc.AddRequest( c, cmdLine );
		}

		void TickClients()
		{
			var toRemove = new List<TClient>();

			foreach( var c in clients )
			{
				if( !c.Connected ) toRemove.Add( c );
				c.Tick();
			}

			// remove disconnected ones
			foreach( var c in toRemove )
			{
				clients.Remove( c );
				c.Dispose(); // remove all delegates etc.
			}

		}
		/// <summary>
		/// start the server
		/// </summary>
		public void Start()
		{
			IPAddress localAddr = IPAddress.Parse( localIPstr );

			server = new TcpListener( localAddr, port );

			// Start listening for client requests.
			server.Start();

		}

		/// <summary>
		/// stop the server
		/// </summary>
		public void Stop()
		{
			// Stop listening for new clients.
			server.Stop();

			// kill all clients
			foreach( var c in clients )
			{
				c.Dispose();
			}
			clients.Clear();
		}
	}
}
