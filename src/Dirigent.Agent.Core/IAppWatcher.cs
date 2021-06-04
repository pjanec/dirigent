using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace Dirigent
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
        bool ShallBeRemoved { get; set; }

        [Flags]
        enum EFlags
        {
            ClearOnLaunch = 0x01 // will be removed (if installed) when the app is being launched
        };

        /// <summary>
        /// Flags describing how the system should handle this watcher
        /// </summary>
        EFlags Flags { get; }

        /// <summary>
        /// What app is this watcher installed on
        /// </summary>
        LocalApp App { get; } 
    }

    //public interface IAppWatcherFactory
    //{
    //    IAppWatcher create(AppDef appDef, AppState appState, int processId, string parameters);
    //}
}
