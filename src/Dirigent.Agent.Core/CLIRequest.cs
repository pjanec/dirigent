using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Dirigent
{
	// request being processes
	public class CLIRequest	: Disposable
	{
		public ICLIClient Client;
		public string? Uid; // unique request id (if provided by client, will become part of response)
		public bool Finished; // is processing of this request finished? If so, will be discarded.

		Queue<ICommand> Commands; // commands to be performed as part of the request
		CommandRepository cmdRepo;
		Master ctrl;
		private SemaphoreSlim _mutex; // blocks async waiting for request finishing
		private Exception? _except;
		public Exception? Exception => _except; // exception caught when executing the request

        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

		public CLIRequest( ICLIClient client, Master ctrl, string cmdLine )
		{
			this.ctrl = ctrl;
			Client = client;
			Commands = new Queue<ICommand>();
			cmdRepo = new CommandRepository( ctrl );
			DirigentCommandRegistrator.Register( cmdRepo );

			// parse commands and fill cmd queue
			string? restAfterUid;
			SplitToUuidAndRest( cmdLine, out Uid, out restAfterUid );
			if( string.IsNullOrEmpty( restAfterUid ) )
			{
				Finished = true;
				_mutex = new SemaphoreSlim(1);
				return;
			}

			try
			{
				var cmdList = cmdRepo.ParseCmdLine( client.Name, restAfterUid, WriteResponseLine );
				Commands = new Queue<ICommand>( cmdList );
				_mutex = new SemaphoreSlim(0);
			}
			catch( Exception e )
			{
				// take just first line of exception description
				string excMsg = e.ToString();
				var crPos = excMsg.IndexOf( '\r' );
				var lfPos = excMsg.IndexOf( '\n' );
				if( crPos >= 0 || lfPos >= 0 )
				{
					excMsg = excMsg.Substring( 0, Math.Min( crPos, lfPos ) );
				}

				WriteResponseLine( "ERROR: " + Tools.JustFirstLine( e.Message ) );

				Finished = true;
				_mutex = new SemaphoreSlim(1);
				_except = e;
			}
		}

		// adds reapsonse id prefix and adds LF at the end
		void WriteResponseLine( string respLine )
		{
			var sb = new StringBuilder();

			// uid if provided
			if( !string.IsNullOrEmpty( Uid ) )
			{
				sb.AppendFormat( "[{0}] ", Uid );
			}

			sb.Append( respLine );

			log.DebugFormat("{0}: Response: {1}", Client.Name, respLine);

			sb.Append( "\n" );

			Client.WriteResponse( sb.ToString() );
		}

		void SplitToUuidAndRest( string s, out string? uuid, out string? rest )
		{
			uuid = null;
			rest = null;
			MatchCollection matches = Regex.Matches( s, @"\s*(?:\[(.*)\])?\s*(.*)" );
			if( matches.Count > 0 )
			{
				Match m = matches[0];
				uuid = m.Groups[1].Value;
				rest = m.Groups[2].Value;
			}
		}

		public virtual void Tick()
		{
			// execute commands, all at once
			while( Commands.Count > 0 )
			{
				var cmd = Commands.Dequeue();
				try
				{
					cmd.Execute();
				}
				catch( Exception e )
				{
					WriteResponseLine( "ERROR: " + Tools.JustFirstLine( e.Message ) );
				}
				cmd.Dispose();
			}
			Finished = true;
			_mutex.Release();
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			Finished = true;
		}

		// waits until the request completes
		public Task WaitAsync()
		{
			return _mutex.WaitAsync();
		}
	}
}
