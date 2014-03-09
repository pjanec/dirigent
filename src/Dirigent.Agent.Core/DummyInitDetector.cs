using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dirigent.Common;

namespace Dirigent.Agent.Core
{
    class DummyInitDetector : IAppInitializedDetector
    {
        public bool IsInitialized()
        {
            return true;
        }
    }
}
