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
            ctrl.StartPlan( Tools.FindPlanByName( ctrl.GetPlanRepo(), args[0]).Name );
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
            ctrl.StopPlan( Tools.FindPlanByName( ctrl.GetPlanRepo(), args[0]).Name );
        }
    }

    public class KillPlan : DirigentControlCommand
    {
        public KillPlan(IDirigentControl ctrl)
            : base(ctrl)
        {
        }

        public override void Execute(IList<string> args)
        {
            ctrl.KillPlan( Tools.FindPlanByName( ctrl.GetPlanRepo(), args[0]).Name );
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
            ctrl.RestartPlan( Tools.FindPlanByName( ctrl.GetPlanRepo(), args[0]).Name );
        }
    }


    public class LaunchApp : DirigentControlCommand
    {
        public LaunchApp(IDirigentControl ctrl)
            : base(ctrl)
        {
        }

        public override void Execute(IList<string> args)
        {
            if( args.Count == 0 ) throw new MissingArgumentException("appIdTuple", "AppIdTuple expected.");
            var t = new AppIdTuple(args[0]);
            if (t.AppId == "") throw new ArgumentSyntaxErrorException("appIdTuple", args[0], "\"machineId.appId\" expected");
            ctrl.LaunchApp(t);
        }
    }

    public class KillApp : DirigentControlCommand
    {
        public KillApp(IDirigentControl ctrl)
            : base(ctrl)
        {
        }

        public override void Execute(IList<string> args)
        {
            if (args.Count == 0) throw new MissingArgumentException("appIdTuple", "AppIdTuple expected.");
            var t = new AppIdTuple(args[0]);
            if (t.AppId == "") throw new ArgumentSyntaxErrorException("appIdTuple", args[0], "\"machineId.appId\" expected");
            ctrl.KillApp(t);
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

    //public class SelectPlan : DirigentControlCommand
    //{
    //    public SelectPlan(IDirigentControl ctrl)
    //        : base(ctrl)
    //    {
    //    }

    //    public override void Execute(IList<string> args)
    //    {
    //        if (args.Count == 0) throw new MissingArgumentException("planName", "plan name expected.");

    //        // find plan in the repository
    //        ILaunchPlan plan = Tools.FindPlanByName( ctrl.GetPlanRepo(), args[0]) ;

    //        ctrl.SelectPlan(plan);
    //    }
    //}

}
