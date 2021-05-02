using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Common;

namespace Dirigent.Agent
{
    /// <summary>
    /// Handles the time separation interval contraints between apps. Waits
    /// until the separation time elapses, then provides next app from the list.
    /// Remembers the last launch time (the last time when some app was provided for launch)
    /// </summary>
    public class LaunchSequencer
    {
        List<AppDef> _appQueue; // queue of apps to be launched sequentially with given time separation

        double _timeOfLastLaunch; // time stamp (in seconds)
        double _lastAppSeparationInterval; // in seconds

        public LaunchSequencer()
        {
            _timeOfLastLaunch = 0;
            _lastAppSeparationInterval = 0.0;
            _appQueue = new List<AppDef>();
        }

        public void AddApps( IEnumerable<AppDef> appDefs )
        {
            // add new apps at the end of queue
            foreach( var ad in appDefs )
            {
                _appQueue.Add( ad );
            }
        }
        
        public bool IsEmpty()
        {
            return ( _appQueue.Count == 0 );
        }
        
        /// <summary>
        /// If the time interval from last query exceeds the separation interval of last returned app,
        /// returns a next app from the queue.
        /// </summary>
        /// <param name="currentTimeStamp"></param>
        /// <returns>null if no app to be lanched, otherwise the AppDef</returns>
        public AppDef? GetNext( double currentTime )
        {
            AppDef? res = null;
            if( !IsEmpty() )
            {
                double deltaSeconds = currentTime - _timeOfLastLaunch;

                if( deltaSeconds >= _lastAppSeparationInterval )
                {
                    res = _appQueue[0];
                    
                    // remember constraints for the next one
                    _timeOfLastLaunch = currentTime;
                    _lastAppSeparationInterval = res.SeparationInterval;

                    // remove the app returned
                    _appQueue.RemoveAt(0);
                }
            
            }
            return res;
        }
    }


}
