using System;
using System.Linq;

namespace Dirigent.Net
{
	/// <summary>
	/// What the master and its clients have to agree on before they can talk. Raised whenever the
	/// encoding or the meaning of a message changes in a way an older program cannot follow.
	/// </summary>
	public static class Protocol
	{
		/// <summary>
		/// The version of the master protocol. 3120 = introduced with Dirigent 3.1.20 (integer keys, LZ4).
		/// A client that says another number is refused by the master.
		/// </summary>
		public const int Version = 3120;

		/// <summary>The version of this program as the person updating it knows it (3.1.20.0).</summary>
		public static string ProductVersion
		{
			get
			{
				var asm = typeof( Protocol ).Assembly;
				var info = asm.GetCustomAttributes( typeof( System.Reflection.AssemblyInformationalVersionAttribute ), false )
					.OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion;
				// "3.1.20.0+commit" -> "3.1.20.0"
				var text = info ?? asm.GetName().Version?.ToString() ?? "unknown";
				var plus = text.IndexOf( '+' );
				return plus > 0 ? text.Substring( 0, plus ) : text;
			}
		}

		/// <summary>How long a client that was refused waits before it asks again.</summary>
		public static readonly TimeSpan RefusedRetryInterval = TimeSpan.FromSeconds( 30 );

		/// <summary>The master says it once per address and this long, about a client it cannot read at all.</summary>
		public static readonly TimeSpan UnreadableClientLogInterval = TimeSpan.FromMinutes( 10 );
	}
}
