using System;
using System.Threading.Tasks;

namespace Dirigent
{
	/// <summary>
	/// A machine that never answers - its agent is not there under the name it was asked by, or hangs - must not hold a batched
	/// query for ever: the others' answers are worth having.
	/// </summary>
	public static class AnswerTimeout
	{
		/// <summary>How long the batched per-machine queries (locations, versions, crashes) wait for one machine.</summary>
		public static readonly TimeSpan Default = TimeSpan.FromSeconds( 60 );

		/// <summary>The text of a machine that did not answer in time.</summary>
		public static string Message( TimeSpan timeout ) => string.Format( System.Globalization.CultureInfo.InvariantCulture, "no answer within {0:0.#} s", timeout.TotalSeconds );

		/// <summary>The task's result, or a <see cref="TimeoutException"/> saying "no answer within N s".</summary>
		public static async Task<T> Within<T>( Task<T> task, TimeSpan timeout )
		{
			using var cts = new System.Threading.CancellationTokenSource();
			var finished = await Task.WhenAny( task, Task.Delay( timeout, cts.Token ) );
			if( finished != task ) throw new TimeoutException( Message( timeout ) );

			cts.Cancel();
			return await task;
		}
	}
}
