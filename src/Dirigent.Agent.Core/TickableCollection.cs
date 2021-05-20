using System;
using System.Collections.Generic;

namespace Dirigent.Agent
{
    // something to be ticked each frame
    public interface ITickable
    {
        string Id { get; }

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
	public class TickableCollection
    {
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

        private List<ITickable> _ticker = new List<ITickable>();

        private List<ITickable> _toRemove = new(20);

        private List<ITickable> _toReinstall = new List<ITickable>();

        /// <summary>
        /// Replaces Tickable of the same type id existing, or adds a new one
        /// </summary>
        /// <param name="w">new tickable to install</param>
        public void Reinstall( ITickable w )
        {
            // postpone to a safe place outside of watcher's tick
            _toReinstall.Add( w );
        }

        void RemoveAll( Func<ITickable, bool> condition )
        {
            //_watchers.RemoveAll( condition );

            // with debug print
            _toRemove.Clear();
            foreach( var w in _ticker )
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
            foreach( var w in _ticker )
            {
                w.Tick();
            }

            RemoveAll( (x) => x.ShallBeRemoved );
            
            // remove watchers
            foreach( var w in _toRemove )
            {
                log.Debug($"Removing ticker {w.Id} {w.GetType().Name}");
                _ticker.Remove(w);
            }

            // install watchers
            foreach( var w in _toReinstall )
            {
                DoReinstall( w );    
            }
            _toReinstall.Clear();
        }

        void DoReinstall( ITickable w )
        {
            RemoveAll( (x) => x.Id == w.Id );

            log.Debug($"Installing ticker {w.Id} {w.GetType()}" );
            _ticker.Add( w );
        }

    }
}
