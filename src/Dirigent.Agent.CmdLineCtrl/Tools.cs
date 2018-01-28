using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dirigent.Common;

namespace Dirigent.Agent.CmdLineCtrl
{
	class Tools
	{
		public static ILaunchPlan FindPlanByName(IEnumerable<ILaunchPlan> planRepo, string planName)
		{
			// find plan in the repository
			ILaunchPlan plan;
			try
			{
				plan = planRepo.First((i) => i.Name == planName);
				return plan;
			}
			catch
			{
				throw new UnknownPlanName(planName);
			}

		}
	}
}
