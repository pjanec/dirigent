using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace Dirigent.Commands
{
	public class DirigentControlCommand : Disposable, ICommand
	{
		protected string _requestorId = string.Empty; // ident of the one sending the request (error will be delivered back to him)
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




		public DirigentControlCommand( Master ctrl, string requestorId )
		{
			this.name = this.GetType().Name;
			this.ctrl = ctrl;
			this._requestorId = requestorId;
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
		public StartPlan( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			if( args.Count == 0 )  throw new MissingArgumentException( "planId", "planId expected." );

			Dictionary<string, string>? vars = null;
			if( args.Count > 1 )
			{
				try { vars = Tools.ParseEnvVarList(args[1]); }
				catch { throw new ArgumentSyntaxErrorException( "extraVars", args[1], "expected VAR1=VAL1::VAR2==VAL2" ); }
			}


			ctrl.StartPlan( _requestorId, args[0], vars );
			WriteResponse( "ACK" );
		}
	}

	public class StopPlan : DirigentControlCommand
	{
		public StopPlan( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			if( args.Count == 0 )  throw new MissingArgumentException( "planId", "planId expected." );

			ctrl.StopPlan( _requestorId, args[0] );
			WriteResponse( "ACK" );
		}
	}

	public class KillPlan : DirigentControlCommand
	{
		public KillPlan( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			if( args.Count == 0 )  throw new MissingArgumentException( "planId", "planId expected." );

			ctrl.KillPlan( _requestorId, args[0] );
			WriteResponse( "ACK" );
		}
	}

	public class RestartPlan : DirigentControlCommand
	{
		public RestartPlan( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			if( args.Count == 0 )  throw new MissingArgumentException( "planId", "planId expected." );

			Dictionary<string, string>? vars = null;
			if( args.Count > 1 )
			{
				try { vars = Tools.ParseEnvVarList(args[1]); }
				catch { throw new ArgumentSyntaxErrorException( "extraVars", args[1], "expected VAR1=VAL1::VAR2==VAL2" ); }
			}


			ctrl.RestartPlan( _requestorId, args[0], vars );
			WriteResponse( "ACK" );
		}
	}

	public class StartApp : DirigentControlCommand
	{
		public StartApp( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			if( args.Count == 0 )  throw new MissingArgumentException( "appIdTuple", "AppIdTuple expected." );
			var (id, planName) = Tools.ParseAppIdWithPlan( args[0] );
			if( id.AppId == "" ) ThrowAppIdTupleSyntax(args[0]);
			
			Dictionary<string, string>? vars = null;
			if( args.Count > 1 )
			{
				try { vars = Tools.ParseEnvVarList(args[1]); }
				catch { throw new ArgumentSyntaxErrorException( "extraVars", args[1], "expected VAR1=VAL1::VAR2==VAL2" ); }
			}

			ctrl.StartApp( _requestorId, id, planName, flags:0, vars:vars );
			WriteResponse( "ACK" );
		}
	}

	public class KillApp : DirigentControlCommand
	{
		public KillApp( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			if( args.Count == 0 ) throw new MissingArgumentException( "appIdTuple", "AppIdTuple expected." );
			var (id, planName) = Tools.ParseAppIdWithPlan( args[0] );
			if( id.AppId == "" ) ThrowAppIdTupleSyntax(args[0]);
			ctrl.KillApp( _requestorId, id );
			WriteResponse( "ACK" );
		}
	}

	public class RestartApp : DirigentControlCommand
	{
		public RestartApp( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			if( args.Count == 0 ) throw new MissingArgumentException( "appIdTuple", "AppIdTuple expected." );
			var (id, planName) = Tools.ParseAppIdWithPlan( args[0] );
			if( id.AppId == "" ) ThrowAppIdTupleSyntax(args[0]);

			Dictionary<string, string>? vars = null;
			if( args.Count > 1 )
			{
				try { vars = Tools.ParseEnvVarList(args[1]); }
				catch { throw new ArgumentSyntaxErrorException( "extraVars", args[1], "expected VAR1=VAL1::VAR2==VAL2" ); }
			}

			ctrl.RestartApp( _requestorId, id, vars );
			WriteResponse( "ACK" );
		}
	}

	public class SelectPlan : DirigentControlCommand
	{
		public SelectPlan( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			if( args.Count == 0 )  throw new MissingArgumentException( "planId", "planId expected." );

			ctrl.SelectPlan( _requestorId, args[0] );
			WriteResponse( "ACK" );
		}
	}

	public class GetPlanState : DirigentControlCommand
	{
		public GetPlanState( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			if( args.Count == 0 ) throw new MissingArgumentException( "args[0]", "Plan name expected." );
			var planName = args[0];
			var planState = ctrl.GetPlanState( planName );
			var stateStr = Tools.GetPlanStateString( planName, planState );
			WriteResponse( stateStr );
		}
	}

	public class GetAppState : DirigentControlCommand
	{
		public GetAppState( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			if( args.Count == 0 ) throw new MissingArgumentException( "appIdTuple", "AppIdTuple expected." );
			var t = new AppIdTuple( args[0] );
			if( t.AppId == "" ) throw new ArgumentSyntaxErrorException( "appIdTuple", args[0], "\"machineId.appId\" expected" );

			var appState = ctrl.GetAppState( t );
			var stateStr = Tools.GetAppStateString( t, appState );

			WriteResponse( stateStr );
		}
	}


	public class GetAllPlansState : DirigentControlCommand
	{
		public GetAllPlansState( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			foreach( (var planName, var planState) in ctrl.GetAllPlanStates() )
			{
				var stateStr = Tools.GetPlanStateString( planName, planState );
				WriteResponse( stateStr );
			}
			WriteResponse( "END" );
		}
	}


	public class GetAllAppsState : DirigentControlCommand
	{
		public GetAllAppsState( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			foreach( var pair in ctrl.GetAllAppStates() )
			{
				var stateStr = Tools.GetAppStateString( pair.Key, pair.Value );
				WriteResponse( stateStr );
			}
			WriteResponse( "END" );
		}
	}

	public class SetVars : DirigentControlCommand
	{
		public SetVars( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
            if (args.Count == 0) throw new MissingArgumentException("vars", "variable=value expected.");
            ctrl.SetVars( _requestorId, args[0] );
			WriteResponse( "ACK" );
		}
	}

	public class KillAll : DirigentControlCommand
	{
		public KillAll( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			var argsStruct = new KillAllArgs() { };
			if ( args.Count > 0 )
			{
				argsStruct.MachineId = args[0];
			}
			ctrl.KillAll( _requestorId, argsStruct );
			WriteResponse( "ACK" );
		}
	}


	public class Shutdown : DirigentControlCommand
	{
		public Shutdown( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			var argsStruct = new ShutdownArgs() { };

			var argsDict = Tools.ParseKeyValList( args );

			string modeStr;
			if ( Tools.TryGetValueIgnoreKeyCase( argsDict, "mode", out modeStr ) )
			{
				if ( !Tools.GetEnumValueByNameIgnoreCase<EShutdownMode>( modeStr, out argsStruct.Mode ) )
				{
					throw new ArgumentException( String.Format( "invalid mode '{0}'", modeStr ), "mode" );
				}
			}

			string valStr;
			string machineId = "";
			if ( Tools.TryGetValueIgnoreKeyCase( argsDict, "machineId", out valStr ) )
			{
				machineId = valStr;
			}

			ctrl.Shutdown( _requestorId, argsStruct, machineId );
			WriteResponse( "ACK" );
		}
	}

	public class  Terminate : DirigentControlCommand
	{
		public Terminate( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			var argsStruct = new TerminateArgs() { KillApps = true };

			var argsDict = Tools.ParseKeyValList( args );
			string valStr;
			if ( Tools.TryGetValueIgnoreKeyCase( argsDict, "killApps", out valStr ) )
			{
				if ( valStr == "1" ) argsStruct.KillApps = true;
			}

			string machineId = "";
			if ( Tools.TryGetValueIgnoreKeyCase( argsDict, "machineId", out valStr ) )
			{
				machineId = valStr;
			}

			ctrl.Terminate( _requestorId, argsStruct, machineId );
			WriteResponse( "ACK" );
		}
	}

	public class Reinstall : DirigentControlCommand
	{
		public Reinstall( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			var argsStruct = new ReinstallArgs() { };

			var argsDict = Tools.ParseKeyValList( args );

			string modeStr;
			if ( Tools.TryGetValueIgnoreKeyCase( argsDict, "downloadMode", out modeStr ) )
			{
				if ( !Tools.GetEnumValueByNameIgnoreCase<EDownloadMode>( modeStr, out argsStruct.DownloadMode ) )
				{
					throw new ArgumentException( String.Format( "invalid download mode '{0}'", modeStr ), "downloadMode" );
				}
			}

			string urlStr;
			if ( Tools.TryGetValueIgnoreKeyCase( argsDict, "url", out urlStr ) )
			{
				argsStruct.Url = urlStr;
			}

			ctrl.Reinstall( _requestorId, argsStruct );
			WriteResponse( "ACK" );
		}
	}

	public class ReloadSharedConfig : DirigentControlCommand
	{
		public ReloadSharedConfig( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			var argsStruct = new ReloadSharedConfigArgs() { KillApps=true };

			var argsDict = Tools.ParseKeyValList( args );
			string valStr;
			if ( Tools.TryGetValueIgnoreKeyCase( argsDict, "killApps", out valStr ) )
			{
				argsStruct.KillApps = valStr == "1";
			}

			if ( Tools.TryGetValueIgnoreKeyCase( argsDict, "file", out valStr ) )
			{
				argsStruct.FileName = valStr;
			}

			ctrl.ReloadSharedConfig( _requestorId, argsStruct );
			WriteResponse( "ACK" );
		}
	}

	public class StartScript : DirigentControlCommand
	{
		public StartScript( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			if( args.Count == 0 ) throw new MissingArgumentException( "id", "script id expected." );
			(var id, var par) = Tools.ParseScriptIdArgs( args[0] );
			ctrl.StartSingletonScript( _requestorId, Guid.Parse(id), par );
			WriteResponse( "ACK" );
		}
	}

	public class KillScript : DirigentControlCommand
	{
		public KillScript( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			if( args.Count == 0 ) throw new MissingArgumentException( "id", "script id expected." );
			ctrl.KillScript( _requestorId, Guid.Parse(args[0]) );
			WriteResponse( "ACK" );
		}
	}

	public class GetScriptState : DirigentControlCommand
	{
		public GetScriptState( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			if( args.Count == 0 ) throw new MissingArgumentException( "id", "script id expected." );

			var state = ctrl.GetScriptState( _requestorId, Guid.Parse(args[0]) );

			if( state is null )
			{
				WriteResponse( "" );
			}
			else
			{
				WriteResponse( $"{state.Status}:{state.Text??""}");
			}

		}
	}

	public class ApplyPlan : DirigentControlCommand
	{
		public ApplyPlan( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			if( args.Count == 0 )  throw new MissingArgumentException( "planId", "planId expected." );
			var planName = args[0];

			AppIdTuple appIdTuple = new AppIdTuple();
			if( args.Count > 0 )
			{
				appIdTuple = new AppIdTuple( args[1] );
			}

			ctrl.ApplyPlan( _requestorId, planName, appIdTuple );
			
			WriteResponse( "ACK" );
		}
	}

}
