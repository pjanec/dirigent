using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dirigent.Common
{
    public interface IAppInitializedDetector : IAppWatcher
    {
        bool IsInitialized { get; }
    }

    public interface IAppInitializedDetectorFactory
    {
        IAppInitializedDetector create(AppDef appDef, AppState appState, int processId, string parameters);
    }
}
