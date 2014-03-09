using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Common;

namespace Dirigent.Agent.Core
{
    /// <summary>
    /// Handles the time separation interval contraints between apps. Waits
    /// until the separation time elapses, then provides next app from the list.
    /// Remembers the last launch time (the last time when some app was provided for launch)
    /// </summary>
    public class LaunchSequencer
    {
        List<AppDef> appQueue; // queue of apps to be launched sequentially with given time separation

        double timeOfLastLaunch; // time stamp (in seconds)
        double lastAppSeparationInterval; // in seconds

        public LaunchSequencer()
        {
            timeOfLastLaunch = 0;
            lastAppSeparationInterval = 0.0;
            appQueue = new List<AppDef>();
        }

        public void AddApps( IEnumerable<AppDef> appDefs )
        {
            // add new apps at the end of queue
            foreach( var ad in appDefs )
            {
                appQueue.Add( ad );
            }
        }
        
        public bool IsEmpty()
        {
            return ( appQueue.Count == 0 );
        }
        
        /// <summary>
        /// If the time interval from last query exceeds the separation interval of last returned app,
        /// returns a next app from the queue.
        /// </summary>
        /// <param name="currentTimeStamp"></param>
        /// <returns>null if no app to be lanched, otherwise the AppDef</returns>
        public AppDef GetNext( double currentTime )
        {
            AppDef res = null;
            if( !IsEmpty() )
            {
                double deltaSeconds = currentTime - timeOfLastLaunch;

                if( deltaSeconds >= lastAppSeparationInterval )
                {
                    res = appQueue[0];
                    
                    // remember constraints for the next one
                    timeOfLastLaunch = currentTime;
                    lastAppSeparationInterval = res.SeparationInterval;

                    // remove the app returned
                    appQueue.RemoveAt(0);
                }
            
            }
            return res;
        }
    }


}
