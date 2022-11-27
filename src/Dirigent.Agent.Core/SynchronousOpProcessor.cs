using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace Dirigent
{
	// Queues operations from whatever async context and executes them synchronously in Tick() method.
	public class SynchronousOpProcessor
	{
		private ConcurrentQueue<SynchronousOp> _synchronousOps; // operations waiting to be processed within master's tick

		public SynchronousOpProcessor()
		{
			_synchronousOps = new ConcurrentQueue<SynchronousOp>();
		}

		// Adds operation to be processed by the master during its next tick.
		// Returns the operation object.
		// The caller can asynchronously await the completion of the operation (using "await operation.WaitAsync();")
		// Thread safe, can be called from async context.
		public SynchronousOp AddSynchronousOp( Action act )
		{
			var op = new SynchronousOp( act );
			_synchronousOps.Enqueue( op );	
			return op;
		}

		public SynchronousOp AddSynchronousOp( Func<object?> func )
		{
			var op = new SynchronousOp( func );
			_synchronousOps.Enqueue( op );	
			return op;
		}

		void ProcessSynchronousOps()
		{
			var numToTake = _synchronousOps.Count;
			while( numToTake-- > 0 )
			{
				if( _synchronousOps.TryDequeue( out var op ) )
				{
					// Execute the operation and release its semaphore
					// Potential exception is stored to the operation object
					op.Execute();
				}
			}
		}

		public void Tick()
		{
			ProcessSynchronousOps();
		}
	}
}
