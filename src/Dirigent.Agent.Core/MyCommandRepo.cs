using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace Dirigent
{
    public static class DirigentCommandRegistrator
    {
        public static void Register( CommandRepository repo )
        {
            repo.Register( "StartPlan",             (ctrl, requestorId) => new Commands.StartPlan(ctrl, requestorId));
            repo.Register( "StopPlan",              (ctrl, requestorId) => new Commands.StopPlan(ctrl, requestorId));
            repo.Register( "KillPlan",              (ctrl, requestorId) => new Commands.KillPlan(ctrl, requestorId));
            repo.Register( "RestartPlan",           (ctrl, requestorId) => new Commands.RestartPlan(ctrl, requestorId));
            repo.Register( "LaunchApp",             (ctrl, requestorId) => new Commands.StartApp(ctrl, requestorId));
            repo.Register( "StartApp",              (ctrl, requestorId) => new Commands.StartApp(ctrl, requestorId));
            repo.Register( "KillApp",               (ctrl, requestorId) => new Commands.KillApp(ctrl, requestorId));
            repo.Register( "RestartApp",            (ctrl, requestorId) => new Commands.RestartApp(ctrl, requestorId));
            repo.Register( "SelectPlan",            (ctrl, requestorId) => new Commands.SelectPlan(ctrl, requestorId));
            repo.Register( "GetPlanState",          (ctrl, requestorId) => new Commands.GetPlanState(ctrl, requestorId));
            repo.Register( "GetAppState",           (ctrl, requestorId) => new Commands.GetAppState(ctrl, requestorId));
            repo.Register( "GetAllPlansState",      (ctrl, requestorId) => new Commands.GetAllPlansState(ctrl, requestorId));
            repo.Register( "GetAllAppsState",       (ctrl, requestorId) => new Commands.GetAllAppsState(ctrl, requestorId));
            repo.Register( "SetVars",               (ctrl, requestorId) => new Commands.SetVars(ctrl, requestorId));
            repo.Register( "KillAll",               (ctrl, requestorId) => new Commands.KillAll(ctrl, requestorId));
            repo.Register( "Shutdown",              (ctrl, requestorId) => new Commands.Shutdown(ctrl, requestorId));
            repo.Register( "Terminate",             (ctrl, requestorId) => new Commands.Terminate(ctrl, requestorId));
            repo.Register( "Reinstall",             (ctrl, requestorId) => new Commands.Reinstall(ctrl, requestorId));
            repo.Register( "ReloadSharedConfig",    (ctrl, requestorId) => new Commands.ReloadSharedConfig(ctrl, requestorId));
            repo.Register( "StartScript",           (ctrl, requestorId) => new Commands.StartScript(ctrl, requestorId));
            repo.Register( "KillScript",            (ctrl, requestorId) => new Commands.KillScript(ctrl, requestorId));
            repo.Register( "GetScriptState",        (ctrl, requestorId) => new Commands.GetScriptState(ctrl, requestorId));
            repo.Register( "ApplyPlan",             (ctrl, requestorId) => new Commands.ApplyPlan(ctrl, requestorId));
        }

    }
}
