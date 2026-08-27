using System;
using System.Collections.Generic;
using System.Linq;

using Dirigent.CLI;

namespace Dirigent.TestBed
{
	/// <summary>
	/// A text-command session with the master, over the very socket the dirigent CLI and a
	/// PowerShell driver use. Requests are strictly sequential here: send one, read its answer.
	/// </summary>
	/// <remarks>
	/// This runs on the test thread, not the pump thread - the master picks the requests up in its
	/// own tick, so a blocking read here is safe and does not need the OffPump treatment.
	/// </remarks>
	public sealed class CliSession : Disposable
	{
		static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds( 10 );

		readonly CommandLineClient _client;

		public CliSession( int port, string ip = "127.0.0.1" )
		{
			_client = new CommandLineClient( ip, port );
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if( !disposing ) return;
			_client.Dispose();
		}

		/// <summary>One request, one response line, with the request-id prefix stripped.</summary>
		public string Request( string cmdLine, TimeSpan? timeout = null )
		{
			var reqId = _client.SendReq( cmdLine );
			return Read( cmdLine, reqId, timeout );
		}

		/// <summary>
		/// A request whose answer is a list of lines terminated by "END" - GetAllAppsState and
		/// friends. The terminator is not included.
		/// </summary>
		public List<string> RequestList( string cmdLine, TimeSpan? timeout = null )
		{
			var reqId = _client.SendReq( cmdLine );

			var lines = new List<string>();
			while( true )
			{
				var line = Read( cmdLine, reqId, timeout );
				if( line == "END" ) return lines;
				lines.Add( line );
			}
		}

		string Read( string cmdLine, string reqId, TimeSpan? timeout )
		{
			var raw = _client.ReadResp( (int) ( timeout ?? DefaultTimeout ).TotalMilliseconds );
			if( raw is null )
				throw new TimeoutException( $"no answer to '{cmdLine}' within {( timeout ?? DefaultTimeout ).TotalSeconds:0.#} s" );

			var line = raw.Trim();

			// responses carry the id of the request they answer, when one was given
			var prefix = $"[{reqId}] ";
			if( line.StartsWith( prefix, StringComparison.Ordinal ) )
				line = line.Substring( prefix.Length ).Trim();

			if( line.StartsWith( "ERROR:", StringComparison.OrdinalIgnoreCase ) )
				throw new Exception( $"'{cmdLine}' was refused: {line}" );

			return line;
		}
	}
}
