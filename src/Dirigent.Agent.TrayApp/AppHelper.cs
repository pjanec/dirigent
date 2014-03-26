using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Common;

namespace Dirigent.Agent.TrayApp
{
    public static class AppHelper
    {
        // Throws exception if plan not found; returns null if empty input arguments are provided.
        public static ILaunchPlan GetPlanByName(IEnumerable<ILaunchPlan> planRepo, string planName)
        {
            // start the initial launch plan if specified
            if (planRepo != null && planName != null && planName != "")
            {
                try
                {
                    ILaunchPlan plan = planRepo.First((i) => i.Name == planName);
                    return plan;
                }
                catch
                {
                    throw new UnknownPlanName(planName);
                }
            }
            return null;
        }
    }
}
