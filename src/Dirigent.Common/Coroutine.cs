using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent
{
	/// <summary>
	/// Nestable coroutine
	/// </summary>
	/// <example>
	/// 	using System.Collections;
	/// 	
	///		IEnumerable MyCoroutine( int x )
	///		{
	///		    // ...
	///		}
	///		
	///		static IEnumerable Test1()
	///		{
	///			// invoke coroutine method
	///         yield return new Coroutine( MyCoroutine( 5 ) );
	///     }    
	///
	///
	///		static IEnumerable Test2()
	///		{
	///			Console.WriteLine("Test2: Started");
	///			Console.WriteLine("Test2: Going to wait");
	///			// invoke coroutine class
	///			yield return new WaitForSeconds(1.0);
	///			Console.WriteLine("Test2: Waiting finished");
	///			Console.WriteLine("Test2: Finished");
	///			
	///		}
	///
	///		static void Main(string[] args)
	///		{
	///			var x = new Coroutine( Test2() );
	///			while(!x.IsFinished )
	///			{
	///				x.Tick();
	///			}
	///			x.Dispose();
	///			Console.WriteLine("Done");
	///		}
	/// </example>
	public class Coroutine : IDisposable
	{
		private IEnumerator? _routine; 

		public Action? Completed;

		public bool IsFinished;

		public Coroutine? _nested;
		private bool disposedValue;
		private bool wasStarted = false;

		public Coroutine()
		{
		}

		public Coroutine( IEnumerable routine )
		{
			Start( routine );
		}
		
		public void Start(IEnumerable routine)
        {
			if( wasStarted ) throw new Exception("Coroutine can't be started more than once!");
			wasStarted = true;

            _routine = routine.GetEnumerator();
        }

		public void Tick()
		{
			if( IsFinished )
				return;

			// if nested coroutine exists, tick it instead of our own
			if( _nested is not null )
			{
				_nested.Tick();
				if( _nested.IsFinished )
				{
					_nested.Dispose();
					_nested = null;
				}
				// don't tick our corutine until the nested one finishes
				return;
			}

			if (_routine is not null)
            {
                if (!_routine.MoveNext())
                {
                    if (Completed is not null)
                        Completed();

                    IsFinished = true;

					return;
                }

				// install a nested coroutine if a coroutine object is returned
				_nested = _routine.Current as Coroutine;
     		}
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					// TODO: dispose managed state (managed objects)

					if( _nested is not null  )
					{
						_nested.Dispose();
						_nested = null;
					}

					if (_routine is not null)
					{
						IDisposable? disp = _routine as IDisposable;
						if (disp is not null)
						{
							disp.Dispose();
						}
						_routine = null;
					}
				}

				// TODO: free unmanaged resources (unmanaged objects) and override finalizer
				// TODO: set large fields to null
				disposedValue = true;
			}
		}

		// // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
		// ~Coroutine()
		// {
		//     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
		//     Dispose(disposing: false);
		// }

		public void Dispose()
		{
			// Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
			Dispose(disposing: true);
			GC.SuppressFinalize(this);
		}
	}

	public class WaitForSeconds	: Coroutine
	{
		DateTime _endTime;

		public WaitForSeconds(double secondsToWait)
		{
			_endTime = DateTime.Now + new TimeSpan( 0, 0, 0, 0, (int) (secondsToWait*1000) );
			Start( Wait() );
		}

		IEnumerable Wait()
		{
			while( DateTime.Now < _endTime )
			{
				yield return null;
			}
		}
	}


}
