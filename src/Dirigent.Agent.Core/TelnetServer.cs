using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Globalization;

namespace Dirigent
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
	public partial class TelnetServer : Disposable
	{
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

		private string localIPstr;
		private int port;
		TcpListener server;
		CLIProcessor _cliProc;

		// describes a client that connected
		private class TClient : Disposable, ICLIClient
		{
	        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

			public TcpClient client; // what client sent this request, what client to respond to
			NetworkStream ns;
			StringBuilder buf; // for reading line from partial messages
			public delegate void LineReadDeleg( string line ); // called when a line is read
			public event LineReadDeleg? LineRead;

			public bool IsSubscribedToEvents { get; set; } = false;
			public string? EventsRequestId { get; set; } = null;

			// Caches for last sent state objects
			public Dictionary<AppIdTuple, AppState> LastSentAppStates { get; } = new();
			public Dictionary<string, PlanState> LastSentPlanStates { get; } = new();
			public Dictionary<Guid, ScriptState> LastSentScriptStates { get; } = new();

			// Custom update interval per client
			public const double DefaultUpdateInterval = 0.25;
			public double UpdateIntervalSec { get; set; } = DefaultUpdateInterval;
			public DateTime NextUpdateTime { get; set; } = DateTime.UtcNow;

			public TClient( TcpClient client )
			{
				this.client = client;
				ns = client.GetStream();
				buf = new StringBuilder();
			}

			public string Name
			{
				get { return client.Client?.RemoteEndPoint?.ToString() ?? string.Empty; }
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

			IPAddress localAddr = IPAddress.Parse( localIPstr );

			server = new TcpListener( localAddr, port );

			// Start listening for client requests.
			server.Start();
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

			// Poll for status changes for subscribed clients
			CheckForStatusChanges();
		}

		/// <summary>
		/// Handles connection-specific commands like SendEvents.
		/// Sends an immediate ACK to confirm support before performing longer operations.
		/// </summary>
		/// <returns>True if a command was handled, false otherwise.</returns>
		private bool HandleLocalCommands(TClient c, string cmdLine)
		{
			string trimmedCmd = cmdLine.Trim();
			string? reqId = null;
			string cmdBody = trimmedCmd;

			var match = Regex.Match(trimmedCmd, @"^\s*\[(.*?)\]\s*(.*)");
			if (match.Success && match.Groups.Count > 1)
			{
				reqId = match.Groups[1].Value;
				cmdBody = match.Groups[2].Value.Trim();
			}

			if (cmdBody.StartsWith("SendEvents", StringComparison.OrdinalIgnoreCase))
			{
				var parts = cmdBody.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

				// First, send acknowledgement that the command is understood
				string response = "ACK\n";
				if (!string.IsNullOrEmpty(reqId))
				{
					response = $"[{reqId}] {response}";
				}
				c.WriteResponse(response);

				// Now, process the command
				if (parts.Length >= 2 && (parts[1] == "0" || parts[1] == "1"))
				{
					bool subscribe = parts[1] == "1";
					c.IsSubscribedToEvents = subscribe;

					if (subscribe)
					{
						c.EventsRequestId = reqId;

						if (parts.Length > 2 && double.TryParse(parts[2], NumberStyles.Any, CultureInfo.InvariantCulture, out double interval))
						{
							c.UpdateIntervalSec = Math.Max(0.1, interval); // Enforce minimum
						}
						else
						{
							c.UpdateIntervalSec = TClient.DefaultUpdateInterval;
						}
						c.NextUpdateTime = DateTime.UtcNow;
						InitialStatusDump(c);   
					}
					else
					{
						c.EventsRequestId = null;
						c.LastSentAppStates.Clear();
						c.LastSentPlanStates.Clear();
						c.LastSentScriptStates.Clear();
					}
					log.DebugFormat("{0}: CLI Event Subscription: {1}", c.Name, subscribe ? "ON" : "OFF");
				}
				else
				{
					string error = "ERROR: Syntax is SendEvents <0|1> [<interval>]\n";
					if (!string.IsNullOrEmpty(reqId))
					{
						error = $"[{reqId}] {error}";
					}
					c.WriteResponse(error);
				}
				return true; // Command was handled
			}
			return false; // Not a local command
		}

		private void InitialStatusDump(TClient c)
		{
			if (string.IsNullOrEmpty(c.EventsRequestId)) return;

			// Dump all apps
			foreach (var pair in _cliProc.ctrl.GetAllAppsState())
			{
				var statusLine = Tools.GetAppStateString(pair.Key, pair.Value);
				c.WriteResponse($"[{c.EventsRequestId}] {statusLine}\n");
				c.LastSentAppStates[pair.Key] = pair.Value.Clone();
			}

			// Dump all plans
			foreach (var pair in _cliProc.ctrl.GetAllPlansState())
			{
				var statusLine = Tools.GetPlanStateString(pair.Key, pair.Value);
				c.WriteResponse($"[{c.EventsRequestId}] {statusLine}\n");
				c.LastSentPlanStates[pair.Key] = pair.Value.Clone();
			}
        
			// Dump all DEFINED scripts and their current state (if any)
			foreach (var scriptDef in _cliProc.ctrl.GetAllScriptsDef()) // if we use GetAllScriptState, we will miss the never-started scripts
			{
				var scriptId = scriptDef.Guid;
				// Get the current state if it exists, otherwise create a default "Unknown" state.
				var scriptState = _cliProc.ctrl.GetScriptState(scriptId) ?? new ScriptState();

				var statusLine = Tools.GetScriptStateString(scriptId, scriptState);
				c.WriteResponse($"[{c.EventsRequestId}] {statusLine}\n");
				c.LastSentScriptStates[scriptId] = scriptState.Clone();
			}
		}

		private void CheckForStatusChanges()
		{
			var now = DateTime.UtcNow;
			foreach (var c in clients)
			{
				if (!c.IsSubscribedToEvents || string.IsNullOrEmpty(c.EventsRequestId) || now < c.NextUpdateTime)
				{
					continue;
				}

				c.NextUpdateTime = now.AddSeconds(c.UpdateIntervalSec);

				// --- Check App States ---
				var currentAppStates = _cliProc.ctrl.GetAllAppsState().ToDictionary(p => p.Key, p => p.Value);
				foreach (var pair in currentAppStates)
				{
					if (!c.LastSentAppStates.TryGetValue(pair.Key, out var lastState) || !lastState.Equals(pair.Value))
					{
						var statusLine = Tools.GetAppStateString(pair.Key, pair.Value);
						c.WriteResponse($"[{c.EventsRequestId}] {statusLine}\n");
						c.LastSentAppStates[pair.Key] = pair.Value.Clone();
					}
				}
				var removedAppKeys = c.LastSentAppStates.Keys.Except(currentAppStates.Keys).ToList();
				foreach(var key in removedAppKeys) c.LastSentAppStates.Remove(key);

				// --- Check Plan States ---
				var currentPlanStates = _cliProc.ctrl.GetAllPlansState().ToDictionary(p => p.Key, p => p.Value);
				foreach (var pair in currentPlanStates)
				{
					if (!c.LastSentPlanStates.TryGetValue(pair.Key, out var lastState) || !lastState.Equals(pair.Value))
					{
						var statusLine = Tools.GetPlanStateString(pair.Key, pair.Value);
						c.WriteResponse($"[{c.EventsRequestId}] {statusLine}\n");
						c.LastSentPlanStates[pair.Key] = pair.Value.Clone();
					}
				}
				var removedPlanKeys = c.LastSentPlanStates.Keys.Except(currentPlanStates.Keys).ToList();
				foreach(var key in removedPlanKeys) c.LastSentPlanStates.Remove(key);

				// --- Check Script States ---
				var currentScriptStates = _cliProc.ctrl.GetAllScriptsState().ToDictionary(p => p.Key, p => p.Value);
				foreach (var pair in currentScriptStates)
				{
					if (!c.LastSentScriptStates.TryGetValue(pair.Key, out var lastState) || !lastState.Equals(pair.Value))
					{
						var statusLine = Tools.GetScriptStateString(pair.Key, pair.Value);
						c.WriteResponse($"[{c.EventsRequestId}] {statusLine}\n");
						c.LastSentScriptStates[pair.Key] = pair.Value.Clone();
					}
				}
				var removedScriptKeys = c.LastSentScriptStates.Keys.Except(currentScriptStates.Keys).ToList();
				foreach(var key in removedScriptKeys)
				{
					c.LastSentScriptStates.Remove(key);
				}
			}
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
			if (HandleLocalCommands(c, cmdLine))
			{
				return; // The command was handled locally, do not forward to CLIProcessor
			}
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
		/// stop the server
		/// </summary>
		void Stop()
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
