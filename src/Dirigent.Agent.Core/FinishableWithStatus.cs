using System;
using System.Collections.Generic;

namespace Dirigent
{
    // something to be ticked each frame
    public abstract class FinishableWithStatus : Disposable, ITickable
    {
        public string Id { get { return _name; } set { _name = value; } }
        public string Status => _status; 
        public bool ShallBeRemoved => _shallBeRemoved;

        public virtual void Tick()
        {
        }

        [Flags]
        public enum EFlags 
        {
            Finished,
        };

        public UInt32 Flags => (UInt32) _flags;



        private EFlags _flags = 0;
        private string _name = String.Empty;
        private string _status = String.Empty;
        private bool _shallBeRemoved = false;
    }

}
