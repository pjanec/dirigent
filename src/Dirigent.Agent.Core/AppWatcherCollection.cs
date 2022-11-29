using System;
using System.Collections.Generic;

namespace Dirigent
{
    /// <summary>
    /// A set IAppWatchers currently installed for a local app
    /// </summary>
	public class AppWatcherCollection : Disposable
    {
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

        private List<IAppWatcher> _watchers = new List<IAppWatcher>();

        private List<IAppWatcher> _toRemove = new(20);

        private List<List<IAppWatcher>> _toReinstall = new List<List<IAppWatcher>>();

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if (!disposing) return;
			//foreach (var w in _watchers) w.Dispose(); // TODO: make watchers disposable
			_watchers.Clear();
		}

		/// <summary>
		/// Replaces watcher of the same type if existing, or adds a new one
		/// </summary>
		/// <param name="w">new watcher to install</param>
		public void ReinstallWatcher( IAppWatcher w )
        {
            // postpone to a safe place outside of watcher's tick
            _toReinstall.Add( new List<IAppWatcher>() { w } );
        }

        /// <summary>
        /// Removes watchers of the same type and adds new ones from the list
        /// </summary>
        public void ReinstallWatchers( List<IAppWatcher> watchers )
        {
            if( watchers.Count == 0 ) return;
            // postpone to a safe place outside of watcher's tick
            _toReinstall.Add( watchers );
        }

        void RemoveAll( Func<IAppWatcher, bool> condition )
        {
            //_watchers.RemoveAll( condition );

            foreach( var w in _watchers )
            {
                if( condition(w) )
                {
                    w.ShallBeRemoved = true;
                }
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
                if( !w.ShallBeRemoved )  // ignore those marked for removal
                {
                    w.Tick();
                }
            }

            RemoveAll( (x) => x.ShallBeRemoved );
            
            // remove watchers
            foreach( var w in _toRemove )
            {
                log.Debug($"Removing watcher {w.GetType().Name} from app {w.App.Id}");
                _watchers.Remove(w);
            }
            _toRemove.Clear();

            // install watchers
            foreach( var grp in _toReinstall )
            {
                // first remove all of same type
                foreach( var w in grp )
                {
                    RemoveAll( (x) => x.GetType() == w.GetType() );
                }
                // then add them
                foreach( var w in grp )
                {
                    _watchers.Add( w );
                }
            }
            _toReinstall.Clear();
        }

    }
}
