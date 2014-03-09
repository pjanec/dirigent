using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dirigent.Common
{
    public interface IAppStateMonitor
    {
        AppState getAppState( string appId );
        void setAppState(string appId, AppState appState);
    }
}
