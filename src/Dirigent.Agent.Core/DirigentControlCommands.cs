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

		/// <summary>
		/// A command is done when Execute returns, unless it says otherwise - see <see cref="ICommand.Finished"/>.
		/// </summary>
		public virtual bool Finished => true;

		public virtual void Tick() {}

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


	[CliResponse( Terminator = ETerminator.End )]
	public class GetAllPlansState : DirigentControlCommand
	{
		public GetAllPlansState( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			foreach( (var planName, var planState) in ctrl.GetAllPlansState() )
			{
				var stateStr = Tools.GetPlanStateString( planName, planState );
				WriteResponse( stateStr );
			}
			WriteResponse( "END" );
		}
	}


	[CliResponse( Terminator = ETerminator.End )]
	public class GetAllAppsState : DirigentControlCommand
	{
		public GetAllAppsState( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			foreach( var pair in ctrl.GetAllAppsState() )
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

	// StartScript <guid> <path> [args]
	//   guid - existing script id or a new one
	//   path - path to the script to start, relative to script root folder; can be empty if the script is already defined
	//   args - arguments to the script (optional)
	public class StartScript : DirigentControlCommand
	{
		public StartScript( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			if( args.Count == 0 ) throw new MissingArgumentException( "id", "script id expected." );

			Guid id;
			try
			{
				id = Guid.Parse(args[0]);
			}
			catch
			{
				throw new ArgumentException( "id", "script id must be a guid" );
			}

			string path = "";
			if( args.Count > 1 )
			{
				path = args[1];
			}
			
			string? scriptArgs = null;
			if( args.Count > 2 )
			{
				scriptArgs = args[2];
			}
			
			ctrl.StartSingletonScript( _requestorId, id, path, scriptArgs );
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

			Guid id;
			try
			{
				id = Guid.Parse(args[0]);
			}
			catch
			{
				throw new ArgumentException( "id", "script id must be a guid" );
			}

			ctrl.KillScript( _requestorId, id );
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

			Guid id;
			try
			{
				id = Guid.Parse(args[0]);
			}
			catch
			{
				throw new ArgumentException( "id", "script id must be a guid" );
			}

			var state = ctrl.GetScriptState( _requestorId, id );

			if( state is null )
			{
				WriteResponse( "" );
			}
			else
			{
				var stateJsonStr = Tools.Serialize( state );
				WriteResponse( $"SCRIPT:{id}:{stateJsonStr}");
			}
		}
	}

	/// <summary>
	/// Waits for a script to end and says how it went. `WaitForScript &lt;guid&gt; [timeout=&lt;seconds&gt;]`
	/// </summary>
	/// <remarks>
	/// The point of it is a caller that must not carry on yet - a plan step marking the log files
	/// before the applications start writing to them. `StartScript` answers as soon as the script has
	/// been started, which is not the same thing.
	///
	/// It answers `ACK` when it has accepted the work - the documented meaning of ACK, "delivered and
	/// processed" - and `END` when the script has finished, so a sender knows the difference between
	/// "begun" and "over". Waiting costs the master nothing: the command reports itself unfinished and
	/// is ticked again, while every other request carries on around it.
	/// </remarks>
	[CliResponse( Terminator = ETerminator.End )]
	public class WaitForScript : DirigentControlCommand
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		/// <summary>Seconds to wait before giving up and stopping the script. 0 = as long as it takes.</summary>
		double _timeoutSec;

		Guid _id;
		DateTime _deadline;
		bool _finished;

		public WaitForScript( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override bool Finished => _finished;

		public override void Execute()
		{
			if( args.Count == 0 ) throw new MissingArgumentException( "id", "script id expected." );

			try
			{
				_id = Guid.Parse( args[0] );
			}
			catch
			{
				throw new ArgumentException( "id", "script id must be a guid" );
			}

			var options = Tools.ParseKeyValList( args );
			if( Tools.TryGetValueIgnoreKeyCase( options, "timeout", out var timeoutStr ) )
			{
				if( !double.TryParse( timeoutStr, System.Globalization.NumberStyles.Float,
									System.Globalization.CultureInfo.InvariantCulture, out _timeoutSec )
					|| _timeoutSec < 0 )
				{
					throw new ArgumentSyntaxErrorException( "timeout", timeoutStr, "seconds expected" );
				}
			}

			// Nothing to wait for is an error, and it is worth failing before the ACK so that the
			// answer is a single ERROR line. A script that has never run and one whose id nobody
			// knows look the same from here, and for a wait they are the same case.
			var state = ctrl.GetScriptState( _requestorId, _id );
			if( state is null )
				throw new Exception( $"Script {_id} is not running and has no result to wait for." );

			WriteResponse( "ACK" );

			_deadline = _timeoutSec > 0 ? DateTime.UtcNow.AddSeconds( _timeoutSec ) : DateTime.MaxValue;

			// it may be over already - a caller waiting for a script that has just ended
			if( !state.IsAlive ) Conclude( state );
		}

		public override void Tick()
		{
			var state = ctrl.GetScriptState( _requestorId, _id );

			if( state is null )
			{
				// forgotten while we waited - a generic script's record does not live long
				Fail( $"Script {_id} disappeared before it finished." );
				return;
			}

			if( !state.IsAlive )
			{
				Conclude( state );
				return;
			}

			if( DateTime.UtcNow >= _deadline )
			{
				// Stopping it is the point: whoever asked is about to carry on regardless, and a
				// script that lands its work afterwards is worse than one that did not run - a mark
				// arriving after the applications have started cuts the beginning off the run.
				log.Warn( $"WaitForScript: {_id} did not finish within {_timeoutSec} s; killing it." );
				try { ctrl.KillScript( _requestorId, _id ); }
				catch( Exception e ) { log.Warn( $"WaitForScript: could not kill {_id}: {e.Message}" ); }

				Fail( $"Script {_id} did not finish within {_timeoutSec} seconds and was stopped." );
			}
		}

		void Conclude( ScriptState state )
		{
			_finished = true;

			switch( state.Status )
			{
				case EScriptStatus.Finished:
					WriteResponse( "END" );
					break;

				case EScriptStatus.Failed:
					WriteResponse( $"ERROR: script {_id} failed: {ErrorTextOf( state )}" );
					break;

				case EScriptStatus.Cancelled:
					WriteResponse( $"ERROR: script {_id} was cancelled." );
					break;

				default:
					WriteResponse( $"ERROR: script {_id} ended as {state.Status}." );
					break;
			}
		}

		void Fail( string message )
		{
			_finished = true;
			WriteResponse( "ERROR: " + message );
		}

		/// <summary>The message of the exception a failed script left behind, if it can be read.</summary>
		static string ErrorTextOf( ScriptState state )
		{
			if( string.IsNullOrEmpty( state.Data ) )
				return string.IsNullOrEmpty( state.Text ) ? "no details" : state.Text!;

			try
			{
				var exception = Tools.Deserialize<SerializedException>( state.Data );
				if( exception is not null && !string.IsNullOrEmpty( exception.Message ) )
					return Tools.JustFirstLine( exception.Message );
			}
			catch( Exception e )
			{
				log.Debug( $"WaitForScript: could not read the script error: {e.Message}" );
			}

			return Tools.JustFirstLine( state.Data! );
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

	public class GetClientState : DirigentControlCommand
	{
		public GetClientState( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			if( args.Count == 0 ) throw new MissingArgumentException( "args[0]", "machine id (or client id) expected." );
			var clientId = args[0];
			var state = ctrl.GetClientState( clientId );
			var stateStr = Tools.GetClientStateString( clientId, state );
			WriteResponse( stateStr );
		}
	}

	[CliResponse( Terminator = ETerminator.End )]
	public class GetAllClientsState : DirigentControlCommand
	{
		public GetAllClientsState( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			foreach( (var id, var state) in ctrl.GetAllClientsState() )
			{
				if( state.Ident is null ) continue;
				if( !state.Ident.IsAgent ) continue; // report just true agents (i.e. machines), not all the GUIs or CLI clients
				var stateStr = Tools.GetClientStateString( id, state );
				WriteResponse( stateStr );
			}
			WriteResponse( "END" );
		}
	}


}
