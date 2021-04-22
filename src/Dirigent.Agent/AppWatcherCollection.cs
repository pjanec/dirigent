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

        private List<IAppWatcher> _watchersToRemove = new(20);

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

        // remove watcher of given type
        public void RemoveWatchersOfType<T>()
        {
            _watchers.RemoveAll( (x) => x.GetType() == typeof(T) );
        }

        void Reinstall( IAppWatcher w )
        {
            _watchers.RemoveAll( (x) => x.GetType() == w.GetType() );
            log.DebugFormat("Installing watcher {0}, app {1}", this.GetType().Name, w.App.Id );
            _watchers.Add( w );
        }

        /// <summary>
        /// Removes all watcher matching given flags
        /// </summary>
        /// <param name="flags"></param>
        public void RemoveMatching( IAppWatcher.EFlags flags )
        {
            _watchers.RemoveAll( (x) => (x.Flags & flags) != 0 );
        }

        /// <summary>
        /// Ticks all watchers. Removes those who wants to be removed.
        /// </summary>
        public void Tick()
        {
            _watchersToRemove.Clear();

            foreach( var w in _watchers )
            {
                w.Tick();
                if( w.ShallBeRemoved ) _watchersToRemove.Add(w);
            }
            
            foreach( var w in _watchersToRemove )
            {
                log.DebugFormat("Removing watcher {0}, app {1}", w.ToString(), w.App.Id );

                _watchers.Remove(w);
            }

            foreach( var w in _toReinstall )
            {
                Reinstall( w );    
            }
            _toReinstall.Clear();
        }

    }
}
