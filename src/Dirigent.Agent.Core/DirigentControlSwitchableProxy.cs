using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dirigent.Common;

namespace Dirigent.Agent.Core
{
    class DirigentControlSwitchableProxy : IDirigentControl
    {
        IDirigentControl impl;

        public DirigentControlSwitchableProxy(IDirigentControl initialImpl)
        {
            impl = initialImpl;
        }

        public void SwitchImpl(IDirigentControl newImpl)
        {
            impl = newImpl;
        }
        
        public AppState GetAppState(AppIdTuple appIdTuple)
        {
            return impl.GetAppState(appIdTuple);
        }

        public void SetRemoteAppState(AppIdTuple appIdTuple, AppState state)
        {
            impl.SetRemoteAppState(appIdTuple, state);
        }

        public void LoadPlan(ILaunchPlan plan)
        {
            impl.LoadPlan(plan);
        }

        public ILaunchPlan GetCurrentPlan()
        {
            return impl.GetCurrentPlan();
        }

        public IEnumerable<ILaunchPlan> GetPlanRepo()
        {
            return impl.GetPlanRepo();
        }

        public void SetPlanRepo(IEnumerable<ILaunchPlan> planRepo)
        {
            impl.SetPlanRepo(planRepo);
        }

        public void StartPlan()
        {
            impl.StartPlan();
        }

        public void StopPlan()
        {
            impl.StopPlan();
        }

        public void RestartPlan()
        {
            impl.RestartPlan();
        }

        public void RunApp(AppIdTuple appIdTuple)
        {
            impl.RunApp(appIdTuple);
        }

        public void RestartApp(AppIdTuple appIdTuple)
        {
            impl.RestartApp(appIdTuple);
        }

        public void KillApp(AppIdTuple appIdTuple)
        {
            impl.KillApp(appIdTuple);
        }
    }
}
