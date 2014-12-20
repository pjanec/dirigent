using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Dirigent.Common
{
    /// <summary>
    /// AppWatcher keeps watching the application and can do something to it, something like:
    ///    - reposition its windows to new location
    ///    - detecting whether the app is already initialized and setting appropriate flag in AppState
    /// Its instantiated with the application launch.
    /// If finished with its tasks it signalizes that it shall be removed; the system will destroy it then.
    /// </summary>
    public interface IAppWatcher
    {
        void Tick();
        
        /// <summary>
        /// Shall the watcher be removed by the system?
        /// </summary>
        /// <returns></returns>
        bool ShallBeRemoved { get; }
    }

    //public interface IAppWatcherFactory
    //{
    //    IAppWatcher create(AppDef appDef, AppState appState, int processId, string parameters);
    //}
}
