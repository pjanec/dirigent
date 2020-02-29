using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Dirigent.CLI
{
	/// <summary>
	/// Allows sending text commands to Dirigent and to receive text-based responses.
	/// </summary>
	public class CommandLineClient : IDisposable
	{
		int lastReqId = 0;
		string NewReqId() { return string.Format("{0:0000}", ++lastReqId); }

		TcpClient client = null;
		StreamReader reader = null;

		// throws on connection timeout!
		public CommandLineClient( string ip, int port )
		{
			client = new TcpClient( ip, port );
			reader = new StreamReader(client.GetStream(), Encoding.UTF8, false, 1024);
		}

		public void Dispose()
		{
			StopAsynResponseReading();

			if( reader !=null )
			{
				reader.Dispose(); // closes the stream
				reader = null;
			}

			if(client!=null)
			{
				client.Close();
				client = null;
			}
		}


		/// <summary>
		/// Sends request. Auto-allocated request id.
		/// </summary>
		/// <param name="req">text of the request</param>
		/// <returns>requestId used</returns>
		public string SendReq( string req )
		{
			var reqId = NewReqId();
			lock(client)
			{
				var stream = client.GetStream();
				var message = String.Format( "[{0}] {1}\n", reqId, req );
				//Console.Write("REQ> {0}", message);
				var messageBytes = System.Text.Encoding.UTF8.GetBytes( message );
				stream.Write( messageBytes, 0, messageBytes.Length );
				stream.Flush();
				return reqId;
			}
		}

		/// <summary>
		/// Reads one single line from the TCP stream. 
		/// </summary>
		/// <param name="timeOutMs">time to wait for the line to be read before exiting prematurely</param>
		/// <returns>Line read. End-of-line character not included. null on timeout</returns>
		public string ReadResp( int timeOutMs )
		{
			lock(client)
			{
				NetworkStream stream;	
				try
				{
					stream = client.GetStream();
					stream.ReadTimeout = timeOutMs;
				}
				catch( System.InvalidOperationException )
				{
					return null; // socket not connected?
				}

				try
				{
					string line = reader.ReadLine();
					//Console.WriteLine(">>>{0}", line);
					return line;
				}
				catch( System.IO.IOException )
				{
					//Console.WriteLine("Timeout waiting for response...");
					return null;
				}
			}
		}

		/// <summary>
		/// Checks if the connection to dirigent is valid
		/// </summary>
		public bool Connected
		{
			get
			{
				return client != null && client.Connected;
			}
		}

		// expects response like for ex:
		//   [002] APP:m1.a:SRIP:0:0:0:0:0
		// appStateCode = a group of letters
		//	  `S` = started (was launched at least once within a single "StartPlan" command)
		//	  `F` = start failed (error when launching, like if the exe is missing or invalid)
		//	  `R` = running (currently running)
		//	  `K` = killed (killed by dirigent)
		//	  `I` = initialized
		//	  `P` = plan applied (app was processed as part of the plan)
		//    'D' = dying
		//    'X' = restarting
		public bool ParseAppState( string resp, out string appIdTupleString, out string appStateCode )
		{
			appIdTupleString = "";
			appStateCode = "";
			if( string.IsNullOrEmpty(resp) ) return false;

			Match match = Regex.Match(resp, @"(?:\[[^\]]*\]?\s?)?APP:([a-z,A-Z,0-9,_,\.]*):([a-z,A-Z,0-9,_,\.]*):", RegexOptions.IgnoreCase);
			if( !match.Success ) return false;

			appIdTupleString = match.Groups[1].Value;
			appStateCode = match.Groups[2].Value;;

			return true;
		}

		public bool ParsePlanState( string resp, out string planName, out string planStateCode )
		{
			planName = "";
			planStateCode = "";
			if( string.IsNullOrEmpty(resp) ) return false;

			Match match = Regex.Match(resp, @"(?:\[[^\]]*\]?\s?)?PLAN:([a-z,A-Z,0-9,_,\.]*):([a-z,A-Z,0-9,_,\.]*):?", RegexOptions.IgnoreCase);
			if( !match.Success ) return false;

			planName = match.Groups[1].Value;
			planStateCode = match.Groups[2].Value;

			return true;
		}

		public bool ParseReqIdAndTheRest( string resp, out string reqId, out string rest )
		{
			reqId = "";
			rest = "";
			if( string.IsNullOrEmpty(resp) ) return false;

			Match match = Regex.Match(resp, @"\[([^\]]*)\]\s*(.*)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
			if( !match.Success ) return false;

			reqId = match.Groups[1].Value;
			rest =  match.Groups[2].Value;

			return true;
		}

		public delegate void RespReceivedDelegate( string respText );
		public delegate void DisconnectedDelegate();

		bool terminateRespThread = false;
		Thread respReadingThread;

		void respReadingThreadFunc( object data )
		{
			var p = (RespThreadParams) data;

			while(!terminateRespThread )
			{
				if( !Connected )
				{
					break;
				}

				string resp = ReadResp(10);

				if( resp != null )
				{
					p.onRespReceived( resp );
				}
			}

			// just wait until the thread is stopped
			if( !Connected )
			{
				if(p.onDisconnected!=null)
				{
					p.onDisconnected();
				}

				while(!terminateRespThread )
				{
					Thread.Sleep(100);
				}
			}
		}

		private struct RespThreadParams
		{
			public RespReceivedDelegate	onRespReceived;
			public DisconnectedDelegate onDisconnected;
		}

		
		public void StartAsynResponseReading( RespReceivedDelegate onRespReceived, DisconnectedDelegate onDisconnected=null )
		{
			if( respReadingThread != null ) return;
			terminateRespThread = false;
			respReadingThread = new Thread( respReadingThreadFunc );
			var p = new RespThreadParams { onRespReceived=onRespReceived, onDisconnected=onDisconnected };
			respReadingThread.Start(p);
		}

		public void StopAsynResponseReading()
		{
			if( respReadingThread != null )
			{
				terminateRespThread = true;
				respReadingThread.Join();
				respReadingThread = null;
			}
		}

	}
}
