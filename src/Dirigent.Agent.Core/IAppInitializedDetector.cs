using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Dirigent
{
    public interface IAppInitializedDetector : IAppWatcher
    {
        bool IsInitialized { get; }
    }

    public interface IAppInitializedDetectorFactory
    {
        IAppInitializedDetector create( LocalApp app, XElement xml);
    }
}
