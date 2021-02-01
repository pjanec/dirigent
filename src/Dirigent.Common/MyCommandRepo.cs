using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Dirigent.Common;

namespace Dirigent.Common
{
    public static class DirigentCommandRegistrator
    {
        public static void Register( CommandRepository repo )
        {
            repo.Register( "StartPlan",             (ctrl) => new Commands.StartPlan(ctrl));
            repo.Register( "StopPlan",              (ctrl) => new Commands.StopPlan(ctrl));
            repo.Register( "KillPlan",              (ctrl) => new Commands.KillPlan(ctrl));
            repo.Register( "RestartPlan",           (ctrl) => new Commands.RestartPlan(ctrl));
            repo.Register( "LaunchApp",             (ctrl) => new Commands.LaunchApp(ctrl));
            repo.Register( "KillApp",               (ctrl) => new Commands.KillApp(ctrl));
            repo.Register( "RestartApp",            (ctrl) => new Commands.RestartApp(ctrl));
            //Register(new Commands.SelectPlan(ctrl));
            repo.Register( "GetPlanState",          (ctrl) => new Commands.GetPlanState(ctrl));
            repo.Register( "GetAppState",           (ctrl) => new Commands.GetAppState(ctrl));
            repo.Register( "GetAllPlansState",      (ctrl) => new Commands.GetAllPlansState(ctrl));
            repo.Register( "GetAllAppsState",       (ctrl) => new Commands.GetAllAppsState(ctrl));
            repo.Register( "SetVars",               (ctrl) => new Commands.SetVars(ctrl));
            repo.Register( "KillAll",               (ctrl) => new Commands.KillAll(ctrl));
            repo.Register( "Shutdown",              (ctrl) => new Commands.Shutdown(ctrl));
            repo.Register( "Terminate",             (ctrl) => new Commands.Terminate(ctrl));
            repo.Register( "Reinstall",             (ctrl) => new Commands.Reinstall(ctrl));
            repo.Register( "ReloadSharedConfig",    (ctrl) => new Commands.ReloadSharedConfig(ctrl));
        }

    }
}
