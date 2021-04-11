using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

using Dirigent.Common;
using Dirigent.Agent.Core;

namespace Dirigent.Agent.TrayApp
{
    public class DaemonApp : App
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        AppConfig ac;


        public DaemonApp(AppConfig ac)
        {
            this.ac = ac;
        }

        public void run()
        {
            log.InfoFormat("Running with machineId={0}, masterIp={1}, masterPort={2}", ac.machineId, ac.masterIP, ac.masterPort);

            using (var client = new Dirigent.Net.Client(ac.machineId, ac.masterIP, ac.masterPort, ac.mcastIP, ac.masterPort, ac.localIP, autoConn:true ))
            {

                string rootForRelativePaths = System.IO.Path.GetDirectoryName( System.IO.Path.GetFullPath(ac.sharedCfgFileName) );
                var agent = new Dirigent.Agent.Core.Agent(ac.machineId, client, true, rootForRelativePaths, false, AppConfig.BoolFromString(ac.mcastAppStates));


                IEnumerable<ILaunchPlan> planRepo = (ac.scfg != null) ? ac.scfg.Plans : null;

                // if there is some local plan repo defined, use it for local operations
                if (planRepo != null)
                {
                    agent.LocalOps.SetPlanRepo(planRepo);
                }

                // start given plan if provided
                if (planRepo != null)
                {
                    agent.LocalOps.SelectPlan(ac.startupPlanName);
                }

                // tick forever
                while (true)
                {
                    agent.tick();
                    Thread.Sleep(500);
                }
            }
        }
    }


}
