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

        public Dictionary<AppIdTuple, AppState> GetAllAppsState()
        {
            return impl.GetAllAppsState();
        }
        
        public void SetRemoteAppState(AppIdTuple appIdTuple, AppState state)
        {
            impl.SetRemoteAppState(appIdTuple, state);
        }

		public void SelectPlan(ILaunchPlan plan)
		{
			impl.SelectPlan(plan);
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

        public void StartPlan( ILaunchPlan plan )
        {
            impl.StartPlan( plan );
        }

        public void StopPlan( ILaunchPlan plan )
        {
            impl.StopPlan( plan );
        }

        public void KillPlan( ILaunchPlan plan )
        {
            impl.KillPlan( plan );
        }

        public void RestartPlan( ILaunchPlan plan )
        {
            impl.RestartPlan( plan );
        }

        public void LaunchApp(AppIdTuple appIdTuple)
        {
            impl.LaunchApp(appIdTuple);
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
