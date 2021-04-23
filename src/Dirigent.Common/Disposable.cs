using System;

namespace Dirigent.Common
{
    public class Disposable : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public Disposable()
        {
        }

        ~Disposable()
        {
            Dispose( false );
        }

        public void Dispose()
        {
            if( !IsDisposed )
            {
                Dispose( true );
                IsDisposed = true;
            }
        }

        protected virtual void Dispose( bool disposing )
        {
        }
    }
}
