using System;

namespace Dirigent
{
	/// <summary>
	/// How the answer to a text command ends.
	/// </summary>
	/// <remarks>
	/// Two of the three response shapes docs/CLI.md describes; the third - a subscription that keeps
	/// sending - terminates nothing and is not a command a sender waits for.
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
