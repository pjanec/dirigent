using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Common;

namespace Dirigent.Agent.Core
{
    public interface ILauncher
    {
        void Launch();
        void Kill();
        bool IsRunning();
    }

    public interface ILauncherFactory
    {
        ILauncher createLauncher( AppDef appDef );
    }
}
