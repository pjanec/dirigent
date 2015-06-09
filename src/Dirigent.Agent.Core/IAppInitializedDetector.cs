using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Dirigent.Common
{
    public interface IAppInitializedDetector : IAppWatcher
    {
        bool IsInitialized { get; }
    }

    public interface IAppInitializedDetectorFactory
    {
        IAppInitializedDetector create(AppDef appDef, AppState appState, int processId, XElement xml);
    }
}
