using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Dirigent.Net
{
	/// <summary>
	/// What went over the wire: per message type, how many messages and how many framed bytes (header + body),
	/// separately for what was sent and what was received. Thread-safe and cheap - a lookup and two interlocked adds
	/// per message - so it stays on in production; <c>GET /api/status</c> reports the master's.
	/// </summary>
	/// <remarks>
	/// A <see cref="Server"/> or a <see cref="Client"/> counts into the instance it is given, or into
	/// <see cref="Default"/>, the process-wide one. A test bed runs the master, agents and GUIs in one process, so
	/// the master's own numbers are had by giving its server an instance of its own.
	/// </remarks>
	public class TrafficStats
	{
		/// <summary>The counters of a process that does not ask for better.</summary>
		public static readonly TrafficStats Default = new();

		class Counter
		{
			public long Messages;
			public long Bytes;
		}

		readonly ConcurrentDictionary<Type, Counter> _sent = new();
		readonly ConcurrentDictionary<Type, Counter> _received = new();
		long _startedTicks = Stopwatch.GetTimestamp();

		/// <summary>One message type's numbers.</summary>
		public class TypeTraffic
		{
			public string type { get; set; } = string.Empty;
			public long messages { get; set; }
			public long bytes { get; set; }
			public double bytesPerSecond { get; set; }
		}

		/// <summary>What one direction came to.</summary>
		public class DirectionTraffic
		{
			public long messages { get; set; }
			public long bytes { get; set; }
			public double bytesPerSecond { get; set; }
		}

		/// <summary>The counters at a moment, the biggest type first - the answer of GET /api/status.</summary>
		public class Report
		{
			/// <summary>Since the start of the process or the last <see cref="Reset"/>.</summary>
			public double seconds { get; set; }
			public List<TypeTraffic> sent { get; set; } = new();
			public List<TypeTraffic> received { get; set; } = new();
			public DirectionTraffic sentTotal { get; set; } = new();
			public DirectionTraffic receivedTotal { get; set; } = new();
		}

		static void Add( ConcurrentDictionary<Type, Counter> to, Type type, long bytes, long messages )
		{
			var c = to.GetOrAdd( type, _ => new Counter() );
			Interlocked.Add( ref c.Messages, messages );
			Interlocked.Add( ref c.Bytes, bytes );
		}

		/// <summary>Counts messages of the type put on the wire; <paramref name="bytes"/> is the framed size of one.</summary>
		public void Sent( Type messageType, long bytes, long messages = 1 ) => Add( _sent, messageType, bytes * messages, messages );

		/// <summary>Counts a message of the type taken off the wire; <paramref name="bytes"/> is its framed size.</summary>
		public void Received( Type messageType, long bytes ) => Add( _received, messageType, bytes, 1 );

		/// <summary>Starts counting from nothing.</summary>
		public void Reset()
		{
			_sent.Clear();
			_received.Clear();
			Interlocked.Exchange( ref _startedTicks, Stopwatch.GetTimestamp() );
		}

		public Report Snapshot()
		{
			var seconds = Math.Max( 0.001, ( Stopwatch.GetTimestamp() - Interlocked.Read( ref _startedTicks ) ) / (double) Stopwatch.Frequency );

			List<TypeTraffic> Of( ConcurrentDictionary<Type, Counter> d )
				=> d.Select( kv => new TypeTraffic()
					{
						type = kv.Key.Name,
						messages = Interlocked.Read( ref kv.Value.Messages ),
						bytes = Interlocked.Read( ref kv.Value.Bytes ),
					} )
					.OrderByDescending( t => t.bytes ).ThenBy( t => t.type, StringComparer.Ordinal )
					.Select( t => { t.bytesPerSecond = Math.Round( t.bytes / seconds, 1 ); return t; } )
					.ToList();

			DirectionTraffic Total( List<TypeTraffic> l )
			{
				var bytes = l.Sum( t => t.bytes );
				return new DirectionTraffic() { messages = l.Sum( t => t.messages ), bytes = bytes, bytesPerSecond = Math.Round( bytes / seconds, 1 ) };
			}

			var sent = Of( _sent );
			var received = Of( _received );

			return new Report()
			{
				seconds = Math.Round( seconds, 1 ),
				sent = sent,
				received = received,
				sentTotal = Total( sent ),
				receivedTotal = Total( received ),
			};
		}
	}
}
