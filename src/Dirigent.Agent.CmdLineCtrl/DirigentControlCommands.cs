using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

using Dirigent.Common;

namespace Dirigent.Agent.CmdLineCtrl.Commands
{
    public class DirigentControlCommand : ICommand
    {
        protected IDirigentControl ctrl;
        string name;

        public DirigentControlCommand(IDirigentControl ctrl)
        {
            this.name = this.GetType().Name;
            this.ctrl = ctrl;
        }

        public string Name { get { return name; } }

        public virtual void Execute(IList<string> args)
        {
            throw new NotImplementedException();
        }

    }


    public class StartPlan : DirigentControlCommand
    {
        public StartPlan(IDirigentControl ctrl)
            : base(ctrl)
        {
        }

        public override void Execute(IList<string> args)
        {
            ctrl.StartPlan();
        }
    }


    public class StopPlan : DirigentControlCommand
    {
        public StopPlan(IDirigentControl ctrl)
            : base(ctrl)
        {
        }

        public override void Execute(IList<string> args)
        {
            ctrl.StopPlan();
        }
    }

    public class RestartPlan : DirigentControlCommand
    {
        public RestartPlan(IDirigentControl ctrl)
            : base(ctrl)
        {
        }

        public override void Execute(IList<string> args)
        {
            ctrl.RestartPlan();
        }
    }


    public class StartApp : DirigentControlCommand
    {
        public StartApp(IDirigentControl ctrl)
            : base(ctrl)
        {
        }

        public override void Execute(IList<string> args)
        {
            if( args.Count == 0 ) throw new MissingArgumentException("appIdTuple", "AppIdTuple expected.");
            var t = new AppIdTuple(args[0]);
            if (t.AppId == "") throw new ArgumentSyntaxErrorException("appIdTuple", args[0], "\"machineId.appId\" expected");
            ctrl.StartApp(t);
        }
    }

    public class StopApp : DirigentControlCommand
    {
        public StopApp(IDirigentControl ctrl)
            : base(ctrl)
        {
        }

        public override void Execute(IList<string> args)
        {
            if (args.Count == 0) throw new MissingArgumentException("appIdTuple", "AppIdTuple expected.");
            var t = new AppIdTuple(args[0]);
            if (t.AppId == "") throw new ArgumentSyntaxErrorException("appIdTuple", args[0], "\"machineId.appId\" expected");
            ctrl.StopApp(t);
        }
    }

    public class RestartApp : DirigentControlCommand
    {
        public RestartApp(IDirigentControl ctrl)
            : base(ctrl)
        {
        }

        public override void Execute(IList<string> args)
        {
            if (args.Count == 0) throw new MissingArgumentException("appIdTuple", "AppIdTuple expected.");
            var t = new AppIdTuple(args[0]);
            if (t.AppId == "") throw new ArgumentSyntaxErrorException("appIdTuple", args[0], "\"machineId.appId\" expected");
            ctrl.RestartApp(t);
        }
    }

    public class LoadPlan : DirigentControlCommand
    {
        public LoadPlan(IDirigentControl ctrl)
            : base(ctrl)
        {
        }

        public override void Execute(IList<string> args)
        {
            if (args.Count == 0) throw new MissingArgumentException("planName", "plan name expected.");

            var planName = args[0];

            // find plan in the repository
            ILaunchPlan plan;
            try
            {
                IEnumerable<ILaunchPlan> planRepo = ctrl.GetPlanRepo();
                plan = planRepo.First((i) => i.Name == planName);
            }
            catch
            {
                throw new UnknownPlanName(planName);
            }

            ctrl.LoadPlan(plan);
        }
    }

}
