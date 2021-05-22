using System;
using System.Collections.Generic;

namespace Dirigent
{
    // something to be ticked each frame
    public interface ITickable : IDisposable
    {
        string Id { get; set; }

        void Tick();

        /// <summary>
        /// Shall the watcher be removed by the system?
        /// </summary>
        /// <returns></returns>
        bool ShallBeRemoved { get; }

        UInt32 Flags { get; }
    }

    /// <summary>
    /// A set ITickables that can be added/removed/reinstalled
    /// </summary>
	public class TickableCollection : Disposable
    {
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

        private List<ITickable> _tickables = new List<ITickable>();

        private List<ITickable> _toRemove = new(20);

        private List<ITickable> _toInstall = new List<ITickable>();

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
            if( !disposing ) return;
            _tickables.ForEach( x => x.Dispose() );
		}

		/// <summary>
		/// Replaces Tickable of the same type id existing, or adds a new one
		/// </summary>
		/// <param name="w">new tickable to install</param>
		public void Install( ITickable w )
        {
            // postpone to a safe place outside of watcher's tick
            _toInstall.Add( w );
        }

        void RemoveAll( Func<ITickable, bool> condition )
        {
            //_watchers.RemoveAll( condition );

            // with debug print
            foreach( var w in _tickables )
            {
                if( condition(w) )
                    _toRemove.Add(w);
            }
        }

        // remove ticker of given type
        public void RemoveByType<T>()
        {
            RemoveAll((x) => x.GetType() == typeof(T));
        }

        // remove ticker of given Id
        public void RemoveById( string id )
        {
            RemoveAll((x) => x.Id == id);
        }

        /// <summary>
        /// Removes all watcher matching given flags
        /// </summary>
        /// <param name="flags"></param>
        public void RemoveHavingFlags( UInt32 flags )
        {
            RemoveAll( (x) => (x.Flags & flags) != 0 );
        }

        /// <summary>
        /// Ticks all watchers. Removes those who wants to be removed.
        /// </summary>
        public void Tick()
        {
            // tick all
            foreach( var w in _tickables )
            {
                try
                {
                    w.Tick();
                }
                catch( Exception ex )
                {
                    log.Error( $"Tickable [{w.Id}] exception: {ex}" );
                    _toRemove.Add( w );
                }
            }

            RemoveAll( (x) => x.ShallBeRemoved );
            
            // remove watchers
            foreach( var w in _toRemove )
            {
                log.Debug($"Removing ticker {w.Id} {w.GetType().Name}");
                _tickables.Remove(w);
                w.Dispose();
            }
            _toRemove.Clear();


            // install watchers
            foreach( var w in _toInstall )
            {
                DoInstall( w );    
            }
            _toInstall.Clear();
        }

        void DoInstall( ITickable w )
        {
            RemoveAll( (x) => x.Id == w.Id );

            log.Debug($"Installing ticker {w.Id} {w.GetType()}" );
            _tickables.Add( w );
        }

    }
}
