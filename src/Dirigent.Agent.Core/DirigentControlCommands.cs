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

	[CliResponse( Terminator = ETerminator.SingleLine )]
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
			var stateStr = Tools.GetPlanStateString( planName, planState ) + Tools.GetPlanMarkingSuffix( planState );
			WriteResponse( stateStr );
		}
	}

	[CliResponse( Terminator = ETerminator.SingleLine )]
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

			// every defined app has a state from the start, so none is an app the master does not know - an ERROR, as for
			// any other unknown id, rather than an empty line
			var appState = ctrl.GetAppState( t ) ?? throw new UnknownAppIdException( t );
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
				var stateStr = Tools.GetPlanStateString( planName, planState ) + Tools.GetPlanMarkingSuffix( planState );
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

	public class WakeUp : DirigentControlCommand
	{
		public WakeUp( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			if( args.Count == 0 )  throw new MissingArgumentException( "machineId", "machineId expected." );

			ctrl.WakeUp( _requestorId, args[0] );
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
			var (argsStruct, machineId) = ParseArgs( args );
			ctrl.Terminate( _requestorId, argsStruct, machineId );
			WriteResponse( "ACK" );
		}

		/// <summary>
		/// killApps=0|1 (default 1, as Dirigent has always done) and machineId=. "killApps=0" used to be ignored - only "1" was looked
		/// at, on top of a default of true - so the apps were killed whatever was asked; the saved state of each agent then said "killed"
		/// while slow-closing apps lived on, and a restarted agent had nothing to adopt.
		/// </summary>
		public static (TerminateArgs Args, string MachineId) ParseArgs( IList<string> args )
		{
			var argsStruct = new TerminateArgs() { KillApps = true };

			var argsDict = Tools.ParseKeyValList( args );
			string valStr;
			if ( Tools.TryGetValueIgnoreKeyCase( argsDict, "killApps", out valStr ) )
			{
				argsStruct.KillApps = Tools.BoolFromString( valStr );
			}

			string machineId = "";
			if ( Tools.TryGetValueIgnoreKeyCase( argsDict, "machineId", out valStr ) )
			{
				machineId = valStr;
			}

			return (argsStruct, machineId);
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

	[CliResponse( Terminator = ETerminator.SingleLine )]
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
	/// The actual files of one app instance, with local and UNC paths:
	/// `GetAppFiles &lt;appId&gt;[@plan] [content=0]`. The same answer as GET /api/appdefs/{id}/files.
	/// </summary>
	/// <remarks>
	/// The answer is `APPFILES:&lt;appIdAsGiven&gt;:&lt;json&gt;` followed by `END`. It arrives only when the
	/// owning agents have answered, so - like <see cref="WaitForScript"/> - the command reports itself
	/// unfinished and is ticked until the resolving task is done, and its terminator is END: the
	/// client then waits without a read timeout instead of failing on a slow agent. An unknown app or
	/// plan fails in Execute, so the answer is the usual single ERROR line.
	/// </remarks>
	[CliResponse( Terminator = ETerminator.End )]
	public class GetAppFiles : DirigentControlCommand
	{
		string _id = string.Empty;
		System.Threading.Tasks.Task<Web.AppFilesResult>? _task;
		bool _finished;

		public GetAppFiles( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override bool Finished => _finished;

		public override void Execute()
		{
			if( args.Count == 0 ) throw new MissingArgumentException( "appIdTuple", "AppIdTuple expected." );
			_id = args[0];

			var includeContent = true;
			var options = Tools.ParseKeyValList( args.Skip( 1 ).ToList() );
			if( Tools.TryGetValueIgnoreKeyCase( options, "content", out var content ) )
				includeContent = !( content == "0" || string.Equals( content, "false", StringComparison.OrdinalIgnoreCase ) );

			Web.AppFilesQuery query;
			try
			{
				query = Web.AppFilesQuery.Prepare( ctrl, _id ); // on the master thread, which we are
			}
			catch( Web.AppFilesNotFoundException e )
			{
				throw new Exception( e.Message );
			}

			// started from here, but awaiting the agents on other threads - never blocking the tick
			_task = System.Threading.Tasks.Task.Run( () => query.ResolveAsync( includeContent ) );
		}

		public override void Tick()
		{
			if( _task is null || !_task.IsCompleted ) return;

			_finished = true;

			if( _task.IsFaulted )
				throw new Exception( Tools.JustFirstLine( _task.Exception!.GetBaseException().Message ) );

			WriteResponse( $"APPFILES:{_id}:{_task.Result.ToJson()}" );
			WriteResponse( "END" );
		}
	}

	/// <summary>
	/// The files of any top-level file definition, addressed as scripts address them:
	/// `GetFiles &lt;id&gt; [machine=m1] [app=a] [content=0]`. Same answer as GET /api/filedefs/{id}/files:
	/// `FILES:&lt;id&gt;:&lt;json&gt;` and then `END`, completed asynchronously like <see cref="GetAppFiles"/>.
	/// </summary>
	[CliResponse( Terminator = ETerminator.End )]
	public class GetFiles : DirigentControlCommand
	{
		string _id = string.Empty;
		System.Threading.Tasks.Task<Web.FileDefFilesResult>? _task;
		bool _finished;

		public GetFiles( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override bool Finished => _finished;

		public override void Execute()
		{
			if( args.Count == 0 ) throw new MissingArgumentException( "id", "VFS node id expected." );
			_id = args[0];

			var options = Tools.ParseKeyValList( args.Skip( 1 ).ToList() );

			var includeContent = true;
			if( Tools.TryGetValueIgnoreKeyCase( options, "content", out var content ) )
				includeContent = !( content == "0" || string.Equals( content, "false", StringComparison.OrdinalIgnoreCase ) );

			Tools.TryGetValueIgnoreKeyCase( options, "machine", out var machine );
			Tools.TryGetValueIgnoreKeyCase( options, "app", out var app );

			var query = Web.FileDefFilesQuery.Prepare( ctrl, _id, machine, app ); // master thread: finds the definitions
			_task = System.Threading.Tasks.Task.Run( () => query.ResolveAsync( includeContent ) );
		}

		public override void Tick()
		{
			if( _task is null || !_task.IsCompleted ) return;

			_finished = true;

			if( _task.IsFaulted )
				throw new Exception( Tools.JustFirstLine( _task.Exception!.GetBaseException().Message ) );

			WriteResponse( $"FILES:{_id}:{_task.Result.ToJson()}" );
			WriteResponse( "END" );
		}
	}

	/// <summary>
	/// The top-level file definitions as declared: `GetFileDefs [type=] [id=] [machine=] [app=] [group=]`. Same as
	/// GET /api/filedefs. Answers `FILEDEFS:&lt;json&gt;`.
	/// </summary>
	[CliResponse( Terminator = ETerminator.SingleLine )]
	public class GetFileDefs : DirigentControlCommand
	{
		public GetFileDefs( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override void Execute()
		{
			var options = Tools.ParseKeyValList( args );
			Tools.TryGetValueIgnoreKeyCase( options, "type", out var type );
			Tools.TryGetValueIgnoreKeyCase( options, "id", out var id );
			Tools.TryGetValueIgnoreKeyCase( options, "machine", out var machine );
			Tools.TryGetValueIgnoreKeyCase( options, "app", out var app );
			Tools.TryGetValueIgnoreKeyCase( options, "group", out var group );

			WriteResponse( "FILEDEFS:" + Web.FileDefListing.ToJson( Web.FileDefListing.List( ctrl, type, id, machine, app, group ) ) );
		}
	}

	/// <summary>
	/// Starts a download job and answers `DOWNLOAD:&lt;jobId&gt;:&lt;json&gt;`, then `END`. With `wait=1` the answer
	/// comes when the job has ended. The common part of DownloadPackage and DownloadApp.
	/// </summary>
	/// <summary>
	/// The arguments of a command that takes several ids: `a b c machine=m1 comment=...`. What looks like one of the known
	/// options is an option, the rest are ids - so one id is the command as it has always been.
	/// </summary>
	public static class CliIds
	{
		static readonly string[] _options = { "machine", "app", "comment", "wait", "timeout", "content" };

		static bool IsOption( string arg )
		{
			var eq = arg.IndexOf( '=' );
			return eq > 0 && _options.Contains( arg.Substring( 0, eq ), StringComparer.OrdinalIgnoreCase );
		}

		public static List<string> Ids( IEnumerable<string> args ) => args.Where( a => !IsOption( a ) ).ToList();

		public static Dictionary<string, string> Options( IEnumerable<string> args )
			=> Tools.ParseKeyValList( args.Where( IsOption ).ToList() );
	}

	public abstract class StartDownloadCommand : DirigentControlCommand
	{
		Guid _job;
		bool _wait;
		bool _finished;

		protected StartDownloadCommand( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override bool Finished => _finished;

		/// <summary>Reads the arguments into a request.</summary>
		protected abstract DownloadRequest MakeRequest( Dictionary<string, string> options );

		public override void Execute()
		{
			var options = CliIds.Options( args );
			var request = MakeRequest( options );

			if( Tools.TryGetValueIgnoreKeyCase( options, "comment", out var comment ) ) request.comment = comment;
			_wait = Tools.TryGetValueIgnoreKeyCase( options, "wait", out var wait ) && wait != "0";

			try
			{
				_job = ctrl.DownloadJobs.Start( request );
			}
			catch( Web.AppFilesNotFoundException e )
			{
				throw new Exception( e.Message );
			}

			if( !_wait ) Answer();
		}

		public override void Tick()
		{
			if( !_finished && !ctrl.DownloadJobs.IsJobRunning( _job ) ) Answer();
		}

		void Answer()
		{
			_finished = true;
			var view = ctrl.DownloadJobs.Get( _job );
			WriteResponse( $"DOWNLOAD:{_job}:{view?.ToJson()}" );
			WriteResponse( "END" );
		}
	}

	/// <summary>`DownloadPackage &lt;id&gt; [machine=] [app=] [comment=] [wait=1]` - collects a node or package into a job.</summary>
	[CliResponse( Terminator = ETerminator.End )]
	public class DownloadPackage : StartDownloadCommand
	{
		public DownloadPackage( Master ctrl, string requestorId ) : base( ctrl, requestorId ) { }

		protected override DownloadRequest MakeRequest( Dictionary<string, string> options )
		{
			var ids = CliIds.Ids( args );
			if( ids.Count == 0 ) throw new MissingArgumentException( "id", "VFS node id expected." );

			Tools.TryGetValueIgnoreKeyCase( options, "machine", out var machine );
			Tools.TryGetValueIgnoreKeyCase( options, "app", out var app );

			// one id is the request it always was; several are a selection, one folder each in the archive
			if( ids.Count == 1 )
				return new DownloadRequest() { fileDef = new DownloadFileDefSelection() { id = ids[0], machine = machine, app = app } };

			return new DownloadRequest() { fileDefs = ids.Select( i => new DownloadFileDefSelection() { id = i, machine = machine, app = app } ).ToList() };
		}
	}

	/// <summary>`DownloadApp &lt;appId&gt;[@plan] [comment=] [wait=1]` - collects all the files of an application into a job.</summary>
	[CliResponse( Terminator = ETerminator.End )]
	public class DownloadApp : StartDownloadCommand
	{
		public DownloadApp( Master ctrl, string requestorId ) : base( ctrl, requestorId ) { }

		protected override DownloadRequest MakeRequest( Dictionary<string, string> options )
		{
			var ids = CliIds.Ids( args );
			if( ids.Count == 0 ) throw new MissingArgumentException( "appIdTuple", "AppIdTuple expected." );
			return ids.Count == 1 ? new DownloadRequest() { app = ids[0] } : new DownloadRequest() { apps = ids };
		}
	}

	/// <summary>`GetDownload &lt;jobId&gt;` - the state of a job, as `DOWNLOAD:&lt;jobId&gt;:&lt;json&gt;`.</summary>
	[CliResponse( Terminator = ETerminator.SingleLine )]
	public class GetDownload : DirigentControlCommand
	{
		public GetDownload( Master ctrl, string requestorId ) : base( ctrl, requestorId ) { }

		public override void Execute()
		{
			if( args.Count == 0 ) throw new MissingArgumentException( "id", "download job id expected." );
			if( !Guid.TryParse( args[0], out var id ) ) throw new ArgumentException( "download job id must be a guid" );

			var view = ctrl.DownloadJobs.Get( id ) ?? throw new Exception( $"No download {id}." );
			WriteResponse( $"DOWNLOAD:{id}:{view.ToJson()}" );
		}
	}

	/// <summary>`DeleteDownload &lt;jobId&gt;` - stops the job if it runs and removes it.</summary>
	public class DeleteDownload : DirigentControlCommand
	{
		public DeleteDownload( Master ctrl, string requestorId ) : base( ctrl, requestorId ) { }

		public override void Execute()
		{
			if( args.Count == 0 ) throw new MissingArgumentException( "id", "download job id expected." );
			if( !Guid.TryParse( args[0], out var id ) ) throw new ArgumentException( "download job id must be a guid" );

			try { ctrl.DownloadJobs.Delete( id ); }
			catch( DownloadJobException e ) { throw new Exception( e.Message ); }

			WriteResponse( "ACK" );
		}
	}

	/// <summary>
	/// Where the binary and working folder of one app are, as its agent resolves them:
	/// `GetAppLocation &lt;appId&gt;[@plan]`. Same answer as GET /api/appdefs/{id}/location,
	/// `APPLOCATION:&lt;appIdAsGiven&gt;:&lt;json&gt;` and then `END`, completed asynchronously like <see cref="GetAppFiles"/>.
	/// </summary>
	[CliResponse( Terminator = ETerminator.End )]
	public class GetAppLocation : DirigentControlCommand
	{
		string _id = string.Empty;
		System.Threading.Tasks.Task<List<Web.AppLocationEntry>>? _task;
		bool _finished;

		public GetAppLocation( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override bool Finished => _finished;

		public override void Execute()
		{
			if( args.Count == 0 ) throw new MissingArgumentException( "appIdTuple", "AppIdTuple expected." );
			_id = args[0];

			var query = Web.AppLocationQuery.Prepare( ctrl, _id ); // master thread: picks the definition
			_task = System.Threading.Tasks.Task.Run( () => query.ResolveAsync() );
		}

		public override void Tick()
		{
			if( _task is null || !_task.IsCompleted ) return;

			_finished = true;

			if( _task.IsFaulted )
				throw new Exception( Tools.JustFirstLine( _task.Exception!.GetBaseException().Message ) );

			WriteResponse( $"APPLOCATION:{_id}:{_task.Result.Single().ToJson()}" );
			WriteResponse( "END" );
		}
	}

	/// <summary>
	/// The same for all apps or those of one machine: `GetAppLocations [machine=m1]`.
	/// `APPLOCATIONS:&lt;json array&gt;` and then `END`.
	/// </summary>
	[CliResponse( Terminator = ETerminator.End )]
	public class GetAppLocations : DirigentControlCommand
	{
		System.Threading.Tasks.Task<List<Web.AppLocationEntry>>? _task;
		bool _finished;

		public GetAppLocations( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override bool Finished => _finished;

		public override void Execute()
		{
			var options = Tools.ParseKeyValList( args );
			Tools.TryGetValueIgnoreKeyCase( options, "machine", out var machine );

			var query = Web.AppLocationQuery.PrepareAll( ctrl, machine );
			_task = System.Threading.Tasks.Task.Run( () => query.ResolveAsync() );
		}

		public override void Tick()
		{
			if( _task is null || !_task.IsCompleted ) return;

			_finished = true;

			if( _task.IsFaulted )
				throw new Exception( Tools.JustFirstLine( _task.Exception!.GetBaseException().Message ) );

			WriteResponse( "APPLOCATIONS:" + Web.AppFilesResult.ToJson( _task.Result ) );
			WriteResponse( "END" );
		}
	}

	/// <summary>
	/// The versions of the applications, each listed once: `GetAppVersions [machine=m1]`.
	/// `APPVERSIONS:&lt;json array&gt;` and then `END` - the answer of GET /api/appversions.
	/// </summary>
	[CliResponse( Terminator = ETerminator.End )]
	public class GetAppVersions : DirigentControlCommand
	{
		System.Threading.Tasks.Task<List<Web.AppLocationEntry>>? _task;
		bool _finished;

		public GetAppVersions( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override bool Finished => _finished;

		public override void Execute()
		{
			var options = Tools.ParseKeyValList( args );
			Tools.TryGetValueIgnoreKeyCase( options, "machine", out var machine );

			var query = Web.AppLocationQuery.PrepareAll( ctrl, machine );
			_task = System.Threading.Tasks.Task.Run( () => query.ResolveAsync() );
		}

		public override void Tick()
		{
			if( _task is null || !_task.IsCompleted ) return;

			_finished = true;

			if( _task.IsFaulted )
				throw new Exception( Tools.JustFirstLine( _task.Exception!.GetBaseException().Message ) );

			WriteResponse( "APPVERSIONS:" + Web.AppFilesResult.ToJson( Web.AppVersions.Of( _task.Result ) ) );
			WriteResponse( "END" );
		}
	}

	/// <summary>
	/// The crashes of the programs of the site: `GetCrashes [since=] [until=] [component=] [app=] [machine=] [kind=] [id=] [newestPerApp=] [details=1]`.
	/// Lists are comma-separated. `CRASHES:&lt;json&gt;` and then `END` - the answer of GET /api/crashes.
	/// </summary>
	[CliResponse( Terminator = ETerminator.End )]
	public class GetCrashes : DirigentControlCommand
	{
		System.Threading.Tasks.Task<Crashes.CrashListing>? _task;
		bool _grouped;
		bool _finished;

		public GetCrashes( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override bool Finished => _finished;

		public override void Execute()
		{
			var options = Tools.ParseKeyValList( args );

			Web.CrashQuery query;
			try
			{
				_grouped = Tools.TryGetValueIgnoreKeyCase( options, "groupBy", out var groupBy ) && Crashes.CrashGroups.IsGrouping( groupBy );
				query = Web.CrashQuery.Prepare( ctrl, Web.CrashFilterParser.Parse( options ), Tools.TryGetValueIgnoreKeyCase( options, "details", out var d ) && Web.CrashFilterParser.IsTrue( d ),
					Tools.TryGetValueIgnoreKeyCase( options, "timeout", out var t ) ? Web.CrashFilterParser.ParseTimeout( t ) : null );
			}
			catch( ArgumentException e )
			{
				throw new Exception( e.Message );
			}

			_task = System.Threading.Tasks.Task.Run( () => query.RunAsync() );
		}

		public override void Tick()
		{
			if( _task is null || !_task.IsCompleted ) return;

			_finished = true;

			if( _task.IsFaulted )
				throw new Exception( Tools.JustFirstLine( _task.Exception!.GetBaseException().Message ) );

			WriteResponse( "CRASHES:" + ( _grouped ? Web.AppFilesResult.ToJson( Crashes.CrashGroups.Of( _task.Result ) ) : Web.AppFilesResult.ToJson( _task.Result ) ) );
			WriteResponse( "END" );
		}
	}

	/// <summary>
	/// The common part of the commands drawing a line under files, or taking it away. They start the existing
	/// MarkFiles / ClearFiles / UnmarkFiles script, wait for it - `timeout=&lt;seconds&gt;`, 60 by default, 300 at
	/// most - and answer `MARKED:&lt;json&gt;` (`CLEARED:`, `UNMARKED:`) and `END`: the same report as the REST calls.
	/// </summary>
	public abstract class MarkOperationCommand : DirigentControlCommand
	{
		Web.MarkOperation? _operation;
		DateTime _deadline;
		double _timeoutSec;
		bool _finished;

		protected MarkOperationCommand( Master ctrl, string requestorId )
			: base( ctrl, requestorId )
		{
		}

		public override bool Finished => _finished;

		protected abstract Web.MarkOperation.EKind Kind { get; }

		/// <summary>The part of the answer before the colon: MARKED, CLEARED or UNMARKED.</summary>
		string Prefix => Kind switch
		{
			Web.MarkOperation.EKind.Mark => "MARKED",
			Web.MarkOperation.EKind.Clear => "CLEARED",
			_ => "UNMARKED",
		};

		/// <summary>Picks the files and starts the script. Master thread.</summary>
		protected abstract Web.MarkOperation Start( Dictionary<string, string> options );

		public override void Execute()
		{
			if( CliIds.Ids( args ).Count == 0 ) throw new MissingArgumentException( "id", "an id expected." );

			var options = CliIds.Options( args );

			_timeoutSec = Web.MarkApiController.DefaultWaitSeconds;
			if( Tools.TryGetValueIgnoreKeyCase( options, "timeout", out var timeoutStr ) )
			{
				if( !double.TryParse( timeoutStr, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _timeoutSec ) || _timeoutSec <= 0 )
					throw new ArgumentSyntaxErrorException( "timeout", timeoutStr, "seconds expected" );
			}
			_timeoutSec = Math.Min( _timeoutSec, Web.MarkApiController.MaxWaitSeconds );

			try
			{
				_operation = Start( options );
			}
			catch( Web.AppFilesNotFoundException e )
			{
				throw new Exception( e.Message );
			}

			_deadline = DateTime.UtcNow.AddSeconds( _timeoutSec );
		}

		public override void Tick()
		{
			if( _operation is null || _finished ) return;

			var state = _operation.State( ctrl );

			if( state is null || state.IsAlive )
			{
				if( DateTime.UtcNow < _deadline ) return;

				_finished = true;
				throw new Exception( $"The {Web.MarkOperation.Word( Kind )} has not finished in {_timeoutSec:0} s; it keeps running as script {_operation.ScriptId}." );
			}

			_finished = true;
			var report = _operation.ReportOf( state );
			WriteResponse( $"{Prefix}:{report.ToJson()}" );
			WriteResponse( "END" );
		}
	}

	/// <summary>The file definitions behind `&lt;fileDefId&gt; [machine=] [app=]`.</summary>
	public abstract class MarkFileDefsCommand : MarkOperationCommand
	{
		protected MarkFileDefsCommand( Master ctrl, string requestorId ) : base( ctrl, requestorId ) { }

		protected override Web.MarkOperation Start( Dictionary<string, string> options )
		{
			Tools.TryGetValueIgnoreKeyCase( options, "machine", out var machine );
			Tools.TryGetValueIgnoreKeyCase( options, "app", out var app );

			var ids = CliIds.Ids( args );
			if( ids.Count == 1 )
				return Web.MarkOperation.StartForFileDefs( ctrl, Kind, ids[0], machine, app );

			// several ids: one operation over the union
			var request = new Web.FileSelectionRequest()
			{
				fileDefs = ids.Select( i => new DownloadFileDefSelection() { id = i, machine = machine, app = app } ).ToList(),
			};
			return Web.MarkOperation.StartForSelection( ctrl, Kind, Web.FileSelection.Parse( ctrl, request ) );
		}
	}

	/// <summary>`MarkFiles &lt;fileDefId&gt; [machine=] [app=] [timeout=&lt;s&gt;]` - draws a line under the files; destroys nothing.</summary>
	[CliResponse( Terminator = ETerminator.End )]
	public class MarkFiles : MarkFileDefsCommand
	{
		public MarkFiles( Master ctrl, string requestorId ) : base( ctrl, requestorId ) { }
		protected override Web.MarkOperation.EKind Kind => Web.MarkOperation.EKind.Mark;
	}

	/// <summary>`ClearFiles &lt;fileDefId&gt; [machine=] [app=] [timeout=&lt;s&gt;]` - empties what can be emptied, marks the rest.</summary>
	[CliResponse( Terminator = ETerminator.End )]
	public class ClearFiles : MarkFileDefsCommand
	{
		public ClearFiles( Master ctrl, string requestorId ) : base( ctrl, requestorId ) { }
		protected override Web.MarkOperation.EKind Kind => Web.MarkOperation.EKind.Clear;
	}

	/// <summary>`UnmarkFiles &lt;fileDefId&gt; [machine=] [app=] [timeout=&lt;s&gt;]` - forgets the marks.</summary>
	[CliResponse( Terminator = ETerminator.End )]
	public class UnmarkFiles : MarkFileDefsCommand
	{
		public UnmarkFiles( Master ctrl, string requestorId ) : base( ctrl, requestorId ) { }
		protected override Web.MarkOperation.EKind Kind => Web.MarkOperation.EKind.Unmark;
	}

	/// <summary>The files of the application behind `&lt;appId&gt;[@plan]`.</summary>
	public abstract class MarkAppFilesCommand : MarkOperationCommand
	{
		protected MarkAppFilesCommand( Master ctrl, string requestorId ) : base( ctrl, requestorId ) { }

		protected override Web.MarkOperation Start( Dictionary<string, string> options )
		{
			var ids = CliIds.Ids( args );
			if( ids.Count == 1 )
				return Web.MarkOperation.StartForApp( ctrl, Kind, ids[0] );

			return Web.MarkOperation.StartForSelection( ctrl, Kind, Web.FileSelection.Parse( ctrl, new Web.FileSelectionRequest() { apps = ids } ) );
		}
	}

	/// <summary>`MarkAppFiles &lt;appId&gt;[@plan] [timeout=&lt;s&gt;]`</summary>
	[CliResponse( Terminator = ETerminator.End )]
	public class MarkAppFiles : MarkAppFilesCommand
	{
		public MarkAppFiles( Master ctrl, string requestorId ) : base( ctrl, requestorId ) { }
		protected override Web.MarkOperation.EKind Kind => Web.MarkOperation.EKind.Mark;
	}

	/// <summary>`ClearAppFiles &lt;appId&gt;[@plan] [timeout=&lt;s&gt;]`</summary>
	[CliResponse( Terminator = ETerminator.End )]
	public class ClearAppFiles : MarkAppFilesCommand
	{
		public ClearAppFiles( Master ctrl, string requestorId ) : base( ctrl, requestorId ) { }
		protected override Web.MarkOperation.EKind Kind => Web.MarkOperation.EKind.Clear;
	}

	/// <summary>`UnmarkAppFiles &lt;appId&gt;[@plan] [timeout=&lt;s&gt;]`</summary>
	[CliResponse( Terminator = ETerminator.End )]
	public class UnmarkAppFiles : MarkAppFilesCommand
	{
		public UnmarkAppFiles( Master ctrl, string requestorId ) : base( ctrl, requestorId ) { }
		protected override Web.MarkOperation.EKind Kind => Web.MarkOperation.EKind.Unmark;
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

	[CliResponse( Terminator = ETerminator.SingleLine )]
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
