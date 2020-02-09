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

		public void SelectPlan(string planName)
		{
			impl.SelectPlan(planName);
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

		public PlanState GetPlanState(string planName)
		{
			return impl.GetPlanState(planName);
		}

		public void SetPlanState(string planName, PlanState state)
		{
			impl.SetPlanState(planName, state);
		}

        public void StartPlan( string planName )
        {
            impl.StartPlan( planName );
        }

        public void StopPlan( string planName )
        {
            impl.StopPlan( planName );
        }

        public void KillPlan( string planName )
        {
            impl.KillPlan( planName );
        }

        public void RestartPlan( string planName )
        {
            impl.RestartPlan( planName );
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

        public void SetAppEnabled(string planName, AppIdTuple appIdTuple, bool enabled)
        {
            impl.SetAppEnabled(planName, appIdTuple, enabled);
        }

		public void SetVars( string vars )
		{
            impl.SetVars( vars );
		}
    }
}
