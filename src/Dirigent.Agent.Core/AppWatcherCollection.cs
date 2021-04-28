using System;
using System.Collections.Generic;

namespace Dirigent.Agent
{
    /// <summary>
    /// A set IAppWatchers currently installed for a local app
    /// </summary>
	public class AppWatcherCollection
    {
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

        private List<IAppWatcher> _watchers = new List<IAppWatcher>();

        private List<IAppWatcher> _toRemove = new(20);

        private List<IAppWatcher> _toReinstall = new List<IAppWatcher>();

        /// <summary>
        /// Replaces watcher of the same type if existing, or adds a new one
        /// </summary>
        /// <param name="w">new watcher to install</param>
        public void ReinstallWatcher( IAppWatcher w )
        {
            // postpone to a safe place outside of watcher's tick
            _toReinstall.Add( w );
        }

        void RemoveAll( Func<IAppWatcher, bool> condition )
        {
            //_watchers.RemoveAll( condition );

            // with debug print
            _toRemove.Clear();
            foreach( var w in _watchers )
            {
                if( condition(w) )
                    _toRemove.Add(w);
            }
        }

        // remove watcher of given type
        public void RemoveWatchersOfType<T>()
        {
            RemoveAll((x) => x.GetType() == typeof(T));
        }

        void Reinstall( IAppWatcher w )
        {
            RemoveAll( (x) => x.GetType() == w.GetType() );

            log.DebugFormat("Installing watcher {0}, app {1}", w.GetType().Name, w.App.Id );
            _watchers.Add( w );
        }

        /// <summary>
        /// Removes all watcher matching given flags
        /// </summary>
        /// <param name="flags"></param>
        public void RemoveHavingFlags( IAppWatcher.EFlags flags )
        {
            RemoveAll( (x) => (x.Flags & flags) != 0 );
        }

        /// <summary>
        /// Ticks all watchers. Removes those who wants to be removed.
        /// </summary>
        public void Tick()
        {
            // tick watchers
            foreach( var w in _watchers )
            {
                w.Tick();
            }

            RemoveAll( (x) => x.ShallBeRemoved );
            
            // remove watchers
            foreach( var w in _toRemove )
            {
                log.Debug($"Removing watcher {w.GetType().Name} from app {w.App.Id}");
                _watchers.Remove(w);
            }

            // install watchers
            foreach( var w in _toReinstall )
            {
                Reinstall( w );    
            }
            _toReinstall.Clear();
        }

    }
}
