using System;

namespace Dirigent
{
	/// <summary>
	/// How the answer to a text command ends.
	/// </summary>
	/// <remarks>
	/// The response shapes docs/CLI.md describes, except the subscription that keeps sending - that
	/// terminates nothing and is not a command a sender waits for.
	/// </remarks>
	public enum ETerminator
	{
		/// <summary>
		/// One line, `ACK`, and the command is done. What almost every command does.
		/// </summary>
		Ack = 0,

		/// <summary>
		/// The answer runs to an `END`: the listings, which write their lines and then that, and a
		/// command that acknowledges first and finishes later.
		/// </summary>
		End,

		/// <summary>
		/// One line carrying the answer itself, and nothing after it - no ACK, no END.
		/// What the single getters do: `PLAN:...`, `APP:...`, `SCRIPT:...`, `CLIENT:...`.
		/// </summary>
		/// <remarks>
		/// Declared so that a client can stop when the answer has arrived. Without it a sender waits
		/// for a terminator that is never coming, and `Dirigent.CLI.exe` did exactly that: it
		/// printed the right answer, sat out its five-second read timeout and then reported a
		/// failure. Nothing about the master's answer changes for this - it never sent a terminator
		/// for these and still does not, so a telnet client sees what it always saw.
		/// </remarks>
		SingleLine,
	}

	/// <summary>
	/// Declares how a command's answer ends, for senders that have to know before they send.
	/// </summary>
	/// <remarks>
	/// It sits on the command class because that is what writes the answer: a table kept anywhere
	/// else could disagree with the code, and a marker word inside the response would extend a
	/// protocol that other people's clients already parse.
	///
	/// `ERROR` ends any answer whatever this says.
	/// </remarks>
	[AttributeUsage( AttributeTargets.Class, Inherited = false )]
	public class CliResponseAttribute : Attribute
	{
		public ETerminator Terminator { get; set; } = ETerminator.Ack;
	}
}
