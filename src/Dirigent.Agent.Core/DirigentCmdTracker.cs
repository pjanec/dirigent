using System;
using System.Collections.Generic;

namespace Dirigent
{
	/// <summary>
	/// Follows the master's answer to a Dirigent command an application sent
	/// (`ExeFullPath="[dirigent.command]"`), and says how it went.
	/// </summary>
	/// <remarks>
	/// It belongs to the LocalApp rather than to the Launcher that sent the request, because such an
	/// app has no process: it counts as exited the moment it is asked, and the launcher of an app that
	/// is not running is disposed on the next tick - long before the answer arrives.
	///
	/// The answer is matched by request id, which the CLI protocol has always carried and the master
	/// echoes on every line, so one app's answer is never taken for another's.
	/// </remarks>
	public class DirigentCmdTracker
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public enum EOutcome
		{
			/// <summary>No answer yet.</summary>
			Pending,

			/// <summary>Every command of the line answered, none of them with an error.</summary>
			Ok,

			/// <summary>One of them answered ERROR - or the line was refused as a whole.</summary>
			Error,
		}

		/// <summary>The id this request was sent under.</summary>
		public string ReqId { get; }

		public EOutcome Outcome { get; private set; } = EOutcome.Pending;

		/// <summary>The error line, when the outcome is <see cref="EOutcome.Error"/>.</summary>
		public string ErrorText { get; private set; } = string.Empty;

		/// <summary>What is still owed: one terminal line per command sent, in order.</summary>
		readonly List<ETerminator> _expected;

		int _seen;

		public DirigentCmdTracker( string reqId, List<ETerminator> expected )
		{
			ReqId = reqId;
			_expected = expected;
		}

		/// <summary>
		/// A tracker for the given command line, with a fresh request id.
		/// </summary>
		public static DirigentCmdTracker ForCommandLine( string cmdLine )
			=> new DirigentCmdTracker( Guid.NewGuid().ToString( "N" ), ExpectedTerminators( cmdLine ) );

		/// <summary>The line to send, carrying the request id the answer will be recognised by.</summary>
		public string RequestLine( string cmdLine ) => $"[{ReqId}] {cmdLine}";

		/// <summary>
		/// How each command of the line will end its answer, in the order they were sent.
		/// </summary>
		/// <remarks>
		/// Split on the semicolon exactly as the master splits it, so that both sides count the same
		/// commands - including a semicolon inside an argument, which neither side treats as anything
		/// but a separator.
		/// </remarks>
		static List<ETerminator> ExpectedTerminators( string cmdLine )
		{
			var terminators = new List<ETerminator>();

			foreach( var part in cmdLine.Split( ';' ) )
			{
				var trimmed = part.Trim();
				if( trimmed.Length == 0 ) continue;

				var name = trimmed.Split( new char[] { ' ', '\t' }, 2 )[0];
				terminators.Add( DirigentCommandRegistrator.TerminatorOf( name ) );
			}

			return terminators;
		}

		/// <summary>
		/// Offers a response from the master. True if it was the answer to our request.
		/// </summary>
		/// <remarks>
		/// One terminal line is expected per command sent, of the kind that command declares, and the
		/// first ERROR settles the whole thing - which also covers a line the master refused to parse,
		/// where it answers once for the line rather than once per command.
		/// </remarks>
		public bool OnResponse( string text )
		{
			bool wasOurs = false;

			foreach( var line in text.Split( new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries ) )
			{
				if( !TryStripReqId( line, ReqId, out var body ) ) continue;

				wasOurs = true;

				if( Outcome != EOutcome.Pending ) continue; // already settled

				if( body.StartsWith( "ERROR", StringComparison.OrdinalIgnoreCase ) )
				{
					Outcome = EOutcome.Error;
					ErrorText = body;
					log.Warn( $"A dirigent command failed: {body}" );
					continue;
				}

				if( _seen >= _expected.Count ) continue; // nothing more is owed

				var isTerminator = _expected[_seen] == ETerminator.End
									? body.StartsWith( "END", StringComparison.OrdinalIgnoreCase )
									: body.StartsWith( "ACK", StringComparison.OrdinalIgnoreCase );

				// anything else is on the way to it: the ACK of a command that ends with END, or a
				// line of a listing
				if( !isTerminator ) continue;

				_seen++;

				if( _seen >= _expected.Count )
				{
					Outcome = EOutcome.Ok;
					log.Debug( $"A dirigent command is done ({_seen} of {_expected.Count} answers)." );
				}
			}

			return wasOurs;
		}

		/// <summary>Takes the "[id] " off a response line, if it carries the given one.</summary>
		static bool TryStripReqId( string line, string reqId, out string body )
		{
			body = string.Empty;

			var prefix = $"[{reqId}]";
			var trimmed = line.TrimStart();
			if( !trimmed.StartsWith( prefix, StringComparison.Ordinal ) ) return false;

			body = trimmed.Substring( prefix.Length ).TrimStart();
			return true;
		}
	}
}
