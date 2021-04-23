using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using Dirigent.Common;

namespace Dirigent.Agent.Commands
{
	public class DirigentControlCommand : Disposable, ICommand
	{
		private static List<string>	_emptyArgs = new();
		public List<string> args = _emptyArgs;

		public IList<string> Args
		{
			get { return args; }
			set { args = new List<string>( value ); }
		}

		public event WriteResponseDeleg? Response; // to be set externally by command class instance creator and to be called through WriteRespose from command handler

		protected Master ctrl;
		protected string name;




		public DirigentControlCommand( Master ctrl )
		{
			this.name = this.GetType().Name;
			this.ctrl = ctrl;
		}

		public string Name { get { return name; } }

		public virtual void Execute()
		{
			throw new System.NotImplementedException();
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			Response = null;
		}

		// txt = just the response body, i.e. no request id prefix and no LF at the end
		public void WriteResponse( string txt )
		{
			if( Response != null )
			{
				Response( txt );
			}
		}

		public static void ThrowAppIdTupleSyntax( string appIdTupleString )
		{
			throw new ArgumentSyntaxErrorException( "appIdTuple", appIdTupleString, "\"<machine>.<app>[@<plan>]\" expected" );
		}

	}


	public class StartPlan : DirigentControlCommand
	{
		public StartPlan( Master ctrl )
			: base( ctrl )
		{
		}

		public override void Execute()
		{
			ctrl.StartPlan( args[0] );
			WriteResponse( "ACK" );
		}
	}

	public class StopPlan : DirigentControlCommand
	{
		public StopPlan( Master ctrl )
			: base( ctrl )
		{
		}

		public override void Execute()
		{
			ctrl.StopPlan( args[0] );
			WriteResponse( "ACK" );
		}
	}

	public class KillPlan : DirigentControlCommand
	{
		public KillPlan( Master ctrl )
			: base( ctrl )
		{
		}

		public override void Execute()
		{
			ctrl.KillPlan( args[0] );
			WriteResponse( "ACK" );
		}
	}

	public class RestartPlan : DirigentControlCommand
	{
		public RestartPlan( Master ctrl )
			: base( ctrl )
		{
		}

		public override void Execute()
		{
			ctrl.RestartPlan( args[0] );
			WriteResponse( "ACK" );
		}
	}

	public class LaunchApp : DirigentControlCommand
	{
		public LaunchApp( Master ctrl )
			: base( ctrl )
		{
		}

		public override void Execute()
		{
			if( args.Count == 0 )  throw new MissingArgumentException( "appIdTuple", "AppIdTuple expected." );
			var (id, planName) = Common.Tools.ParseAppIdWithPlan( args[0] );
			if( id.AppId == "" ) DirigentControlCommand.ThrowAppIdTupleSyntax(args[0]);
			ctrl.LaunchApp( id, planName );
			WriteResponse( "ACK" );
		}
	}

	public class KillApp : DirigentControlCommand
	{
		public KillApp( Master ctrl )
			: base( ctrl )
		{
		}

		public override void Execute()
		{
			if( args.Count == 0 ) throw new MissingArgumentException( "appIdTuple", "AppIdTuple expected." );
			var (id, planName) = Common.Tools.ParseAppIdWithPlan( args[0] );
			if( id.AppId == "" ) DirigentControlCommand.ThrowAppIdTupleSyntax(args[0]);
			ctrl.KillApp( id );
			WriteResponse( "ACK" );
		}
	}

	public class RestartApp : DirigentControlCommand
	{
		public RestartApp( Master ctrl )
			: base( ctrl )
		{
		}

		public override void Execute()
		{
			if( args.Count == 0 ) throw new MissingArgumentException( "appIdTuple", "AppIdTuple expected." );
			var (id, planName) = Common.Tools.ParseAppIdWithPlan( args[0] );
			if( id.AppId == "" ) DirigentControlCommand.ThrowAppIdTupleSyntax(args[0]);
			ctrl.RestartApp( id );
			WriteResponse( "ACK" );
		}
	}

	//public class SelectPlan : DirigentControlCommand
	//{
	//    public SelectPlan(Master ctrl)
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

	public class GetPlanState : DirigentControlCommand
	{
		public GetPlanState( Master ctrl )
			: base( ctrl )
		{
		}

		public override void Execute()
		{
			throw new CommandNotImplementedException( Name );
			//if( args.Count == 0 ) throw new MissingArgumentException( "args[0]", "Plan name expected." );
			//var planName = args[0];
			//var planState = ctrl.GetPlanState( planName );
			//var stateStr = Tools.GetPlanStateString( planName, planState );
			//WriteResponse( stateStr );
		}
	}

	public class GetAppState : DirigentControlCommand
	{
		public GetAppState( Master ctrl )
			: base( ctrl )
		{
		}

		public override void Execute()
		{
			throw new CommandNotImplementedException( Name );
			//if( args.Count == 0 ) throw new MissingArgumentException( "appIdTuple", "AppIdTuple expected." );
			//var t = new AppIdTuple( args[0] );
			//if( t.AppId == "" ) throw new ArgumentSyntaxErrorException( "appIdTuple", args[0], "\"machineId.appId\" expected" );

			//var appState = ctrl.GetAppState( t );
			//var stateStr = Tools.GetAppStateString( t, appState );

			//WriteResponse( stateStr );
		}
	}


	public class GetAllPlansState : DirigentControlCommand
	{
		public GetAllPlansState( Master ctrl )
			: base( ctrl )
		{
		}

		public override void Execute()
		{
			throw new CommandNotImplementedException( Name );
			//foreach( var p in ctrl.GetPlanRepo() )
			//{
			//	var planState = ctrl.GetPlanState( p.Name );
			//	var stateStr = Tools.GetPlanStateString( p.Name, planState );
			//	WriteResponse( stateStr );
			//}
			//WriteResponse( "END" );
		}
	}


	public class GetAllAppsState : DirigentControlCommand
	{
		public GetAllAppsState( Master ctrl )
			: base( ctrl )
		{
		}

		public override void Execute()
		{
			throw new CommandNotImplementedException( Name );

			//foreach( var pair in ctrl.GetAllAppsState() )
			//{
			//	var stateStr = Tools.GetAppStateString( pair.Key, pair.Value );
			//	WriteResponse( stateStr );
			//}
			//WriteResponse( "END" );
		}
	}

	public class SetVars : DirigentControlCommand
	{
		public SetVars( Master ctrl )
			: base( ctrl )
		{
		}

		public override void Execute()
		{
			throw new CommandNotImplementedException( Name );
   //         if (args.Count == 0) throw new MissingArgumentException("vars", "variable=value expected.");
   //         ctrl.SetVars( args[0] );
			//WriteResponse( "ACK" );
		}
	}

	public class KillAll : DirigentControlCommand
	{
		public KillAll( Master ctrl )
			: base( ctrl )
		{
		}

		public override void Execute()
		{
			throw new CommandNotImplementedException( Name );
			//var argsStruct = new KillAllArgs() {}; 
   //         if( args.Count > 0 )
   //         {
   //             argsStruct.MachineId = args[0];
   //         }
   //         ctrl.KillAll( argsStruct );
			//WriteResponse( "ACK" );
		}
	}


	public class Shutdown : DirigentControlCommand
	{
		public Shutdown( Master ctrl )
			: base( ctrl )
		{
		}

		public override void Execute()
		{
			throw new CommandNotImplementedException( Name );
			//var argsStruct = new ShutdownArgs() {}; 

			//var argsDict = Tools.ParseKeyValList( args );

			//string modeStr;
			//if( Tools.TryGetValueIgnoreKeyCase( argsDict, "mode", out modeStr ) )
			//{
			//	if( !Tools.GetEnumValueByNameIgnoreCase<EShutdownMode>( modeStr, out argsStruct.Mode ) )
			//	{
			//		throw new ArgumentException( String.Format("invalid mode '{0}'", modeStr), "mode" );
			//	}
			//}

   //         ctrl.Shutdown( argsStruct );
			//WriteResponse( "ACK" );
		}
	}

	public class  Terminate : DirigentControlCommand
	{
		public Terminate( Master ctrl )
			: base( ctrl )
		{
		}

		public override void Execute()
		{
			throw new CommandNotImplementedException( Name );
			//var argsStruct = new TerminateArgs() { KillApps=true }; 

			//var argsDict = Tools.ParseKeyValList( args );
			//string valStr;
			//if( Tools.TryGetValueIgnoreKeyCase( argsDict, "killApps", out valStr ) )
			//{
			//	if( valStr=="1" ) argsStruct.KillApps = true;
			//}

			//if( Common.Tools.TryGetValueIgnoreKeyCase( argsDict, "machineId", out valStr ) )
			//{
			//	argsStruct.MachineId = valStr;
			//}

   //         ctrl.Terminate( argsStruct );
			//WriteResponse( "ACK" );
		}
	}

	public class Reinstall : DirigentControlCommand
	{
		public Reinstall( Master ctrl )
			: base( ctrl )
		{
		}

		public override void Execute()
		{
			throw new CommandNotImplementedException( Name );
			//var argsStruct = new ReinstallArgs() {}; 

			//var argsDict = Tools.ParseKeyValList( args );

			//string modeStr;
			//if( Tools.TryGetValueIgnoreKeyCase( argsDict, "downloadMode", out modeStr ) )
			//{
			//	if( !Tools.GetEnumValueByNameIgnoreCase<EDownloadMode>( modeStr, out argsStruct.DownloadMode ) )
			//	{
			//		throw new ArgumentException( String.Format("invalid download mode '{0}'", modeStr), "downloadMode" );
			//	}
			//}

			//string urlStr;
			//if( Tools.TryGetValueIgnoreKeyCase( argsDict, "url", out urlStr ) )
			//{
			//	argsStruct.Url = urlStr;
			//}

   //         ctrl.Reinstall( argsStruct );
			//WriteResponse( "ACK" );
		}
	}

	public class ReloadSharedConfig : DirigentControlCommand
	{
		public ReloadSharedConfig( Master ctrl )
			: base( ctrl )
		{
		}

		public override void Execute()
		{
			throw new CommandNotImplementedException( Name );
			//var argsStruct = new ReloadSharedConfigArgs() {}; 

			//var argsDict = Tools.ParseKeyValList( args );
			//string valStr;
			//if( Tools.TryGetValueIgnoreKeyCase( argsDict, "killApps", out valStr ) )
			//{
			//	if( valStr=="1" ) argsStruct.KillApps = true;
			//}

   //         ctrl.ReloadSharedConfig( argsStruct );
			//WriteResponse( "ACK" );
		}
	}

}
