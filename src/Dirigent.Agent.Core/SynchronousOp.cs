using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace Dirigent
{
		// Operation that needs to be executed in synchronous context but is invoked from an async one which then needs to wait for the op to finish.
		// Works with SynchronousOpProcessor queing the operations and executing them in its Tick()
		// Exception (if thrown during op execution) is stored to tne Exception member.
		// Result of the operation (if any) is stored to the Result memner.
		// Usage:
		// From async context we create the operation and add it to the queue, then we wait for it to finish:
		//	 op = new SynchronouseOp( () => {...} )
		//	 syncQueue.Add( op ) // the queue calls op.Execute at some point in time from diferent context
		//   await op.WaitAsync()	// this blocks until the op gets processed by the queue
		public class SynchronousOp
		{
			private SemaphoreSlim _mutex;
			private Func<object?> _function;
			private Exception? _except;
			
			public Exception Exception => _except; // exception caught when executing the action

			public object? Result;
			
			// operation not returning anything
			public SynchronousOp( Action act )
			{
				this._mutex = new SemaphoreSlim(0);
				this._function = () => { act(); return null; };
				this._except = null;
			}

			// operation returning something (the result is saved to Result member)
			public SynchronousOp( Func<object> func )
			{
				this._mutex = new SemaphoreSlim(0);
				this._function = func;
				this._except = null;
			}

			public System.Threading.Tasks.Task WaitAsync()
			{
				return _mutex.WaitAsync();
			}
			
			// Gets called from tick
			// Potential exception can't be propagated to the async code waiting for the op to execute as
			// the action is processed from different context (tick) - so we need to save the exception
			// and the caller needs to check it
			public void Execute()
			{
				try
				{
					Result = _function();
				}
				catch( Exception ex )
				{
					_except = ex;
				}

				// we expect max one thread to wait for this (one async method)
				_mutex.Release();
			}
		}

}
