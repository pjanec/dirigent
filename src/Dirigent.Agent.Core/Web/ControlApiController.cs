using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

using EmbedIO;
using EmbedIO.WebApi;
using EmbedIO.Routing;

namespace Dirigent.Web
{
	/// <summary>The body of the calls that take variables: <c>{"vars":{"NAME":"value"}}</c>.</summary>
	public class VarsBody
	{
		public Dictionary<string, string>? vars { get; set; }
	}

	/// <summary>The body of POST /api/killall.</summary>
	public class KillAllBody
	{
		public string? machine { get; set; }
	}

	/// <summary>The body of POST /api/sharedconfig/reload.</summary>
	public class ReloadBody
	{
		/// <summary>Kill the applications first - as the CLI does unless told otherwise. True when not given.</summary>
		public bool? killApps { get; set; }

		/// <summary>Another file than the one in use.</summary>
		public string? file { get; set; }
	}

	/// <summary>The body of POST /api/scriptdefs/{id}/start.</summary>
	public class ScriptDefStartBody
	{
		/// <summary>JSON, or a string, passed to the script as it is.</summary>
		public JsonElement? args { get; set; }
	}

	/// <summary>The body of POST /api/scripts.</summary>
	public class ScriptStartBody : ScriptDefStartBody
	{
		/// <summary>The script, as the script library names it: "BuiltIns/GetConnectedMachines.cs".</summary>
		public string? name { get; set; }

		/// <summary>Where to run it: null for the master, or a machine id.</summary>
		public string? machine { get; set; }

		/// <summary>The instance id to give it; a new one when not given.</summary>
		public string? id { get; set; }

		public string? title { get; set; }
	}

	/// <summary>The state of a script that was started with ?waitSeconds=, with what it said and returned.</summary>
	public class ScriptRunResult
	{
		public string id { get; set; } = string.Empty;
		public ScriptStateDetails state { get; set; } = new();

		/// <summary>Finished, Failed or Cancelled: how the script ended.</summary>
		public string status { get; set; } = string.Empty;
		public string? text { get; set; }
		public double? progress { get; set; }

		/// <summary>What the script returned (its result, as JSON where it is JSON), or why it failed.</summary>
		public object? data { get; set; }
	}

	/// <summary>
	/// Control with JSON in and out and proper status codes: plans, applications, Kill All, the shared config, variables and scripts.
	/// Each route calls the Master method the matching CLI command calls. See docs/HTTP.md, "Controlling the system".
	/// </summary>
	public class ControlApiController : WebApiController
	{
		readonly Master _master;

		public ControlApiController( Master master )
		{
			_master = master;
		}

		Task Json( object o ) => HttpContext.SendStringAsync( AppFilesResult.ToJson( o ), "application/json", ApiOps.Utf8 );

		async Task Accepted( object o )
		{
			HttpContext.Response.StatusCode = (int) HttpStatusCode.Accepted;
			await Json( o );
		}

		// ---- plans ------------------------------------------------------------------------------------

		[Route( HttpVerbs.Post, "/plandefs/{id}/start" )]
		public Task<PlanState> StartPlan( string id ) => PlanOp( id, "start" );

		[Route( HttpVerbs.Post, "/plandefs/{id}/stop" )]
		public Task<PlanState> StopPlan( string id ) => PlanOp( id, "stop" );

		[Route( HttpVerbs.Post, "/plandefs/{id}/kill" )]
		public Task<PlanState> KillPlan( string id ) => PlanOp( id, "kill" );

		[Route( HttpVerbs.Post, "/plandefs/{id}/restart" )]
		public Task<PlanState> RestartPlan( string id ) => PlanOp( id, "restart" );

		async Task<PlanState> PlanOp( string id, string what )
		{
			VarsBody? body = null;
			if( what is "start" or "restart" ) body = await ApiOps.ReadBody<VarsBody>( HttpContext ); else await ApiOps.DiscardBody( HttpContext );
			var vars = ApiOps.CheckVars( body?.vars );
			var wait = ApiOps.WaitSeconds( HttpContext );

			var t0 = DateTime.UtcNow.AddSeconds( -1 );
			var wasRunning = false;

			await ApiOps.OnMaster( _master, () =>
			{
				var ps = _master.GetPlanState( id ) ?? throw HttpException.NotFound( $"No plan '{id}' is defined." );
				wasRunning = ps.Running;

				switch( what )
				{
					case "start": _master.StartPlan( "", id, vars ); break;
					case "stop": _master.StopPlan( "", id ); break;
					case "kill": _master.KillPlan( "", id ); break;
					default: _master.RestartPlan( "", id, vars ); break;
				}
			} );

			var reached = false;

			if( wait > 0 && what is "start" or "restart" or "kill" )
			{
				var needFresh = what == "restart" || !wasRunning;

				bool Done()
				{
					var ps = _master.GetPlanState( id );
					if( ps is null ) return false;

					if( what == "kill" ) return !ps.Running && !ps.Killing;

					var terminal = ps.OpStatus == Dirigent.PlanState.EOpStatus.Success || ps.OpStatus == Dirigent.PlanState.EOpStatus.Failure;
					return terminal && ( !needFresh || ( ps.Running && ps.TimeStarted >= t0 && ps.Phase.Length == 0 ) );
				}

				// let the plan have its tick, so that a status left over from before is not taken for the answer
				await Task.Delay( 250 );

				reached = await ApiOps.WaitFor( _master, Done, wait );
				if( !reached )
					throw ApiOps.Timeout( what == "kill" ? $"The kill of plan '{id}'" : $"Plan '{id}' reaching Success or Failure", wait );
			}

			var res = await ApiOps.OnMaster( _master, () => new PlanState( id, _master.GetPlanState( id )! ) );
			HttpContext.Response.StatusCode = (int) ( reached ? HttpStatusCode.OK : HttpStatusCode.Accepted );
			return res;
		}

		// ---- applications -----------------------------------------------------------------------------

		[Route( HttpVerbs.Post, "/appdefs/{id}/start" )]
		public Task<AppState> StartApp( string id ) => AppOp( id, "start" );

		[Route( HttpVerbs.Post, "/appdefs/{id}/kill" )]
		public Task<AppState> KillApp( string id ) => AppOp( id, "kill" );

		[Route( HttpVerbs.Post, "/appdefs/{id}/restart" )]
		public Task<AppState> RestartApp( string id ) => AppOp( id, "restart" );

		/// <summary>Master thread only.</summary>
		AppState AppStateOf( AppIdTuple appId, string? planName )
		{
			var appDef = _master.GetAppDef( appId ) ?? throw HttpException.NotFound( $"No application '{appId}' is defined." );
			var appState = _master.GetAppState( appId ) ?? throw HttpException.NotFound( $"No state of application '{appId}'." );
			return new AppState( appDef, _master.GetPlanState( planName ?? "" ), appState, _master.ExitHistory.Of( appId ) );
		}

		async Task<AppState> AppOp( string id, string what )
		{
			VarsBody? body = null;
			if( what is "start" or "restart" ) body = await ApiOps.ReadBody<VarsBody>( HttpContext ); else await ApiOps.DiscardBody( HttpContext );
			var vars = ApiOps.CheckVars( body?.vars );
			var wait = ApiOps.WaitSeconds( HttpContext );

			var (appId, planName) = Tools.ParseAppIdWithPlan( id );
			if( string.IsNullOrEmpty( appId.AppId ) )
				throw HttpException.BadRequest( $"'{id}' is not an application id: \"<machine>.<app>[@<plan>]\" expected." );

			var wasRunning = false;
			var oldPid = 0;
			var failedBefore = false;

			await ApiOps.OnMaster( _master, () =>
			{
				if( !string.IsNullOrEmpty( planName ) )
				{
					var plan = _master.GetPlanDef( planName ) ?? throw HttpException.NotFound( $"No plan '{planName}' is defined." );
					if( !plan.AppDefs.Any( a => a.Id.Equals( appId ) ) )
						throw HttpException.NotFound( $"Application {appId} is not in plan {planName}." );
				}
				else if( _master.GetAppDef( appId ) is null )
				{
					throw HttpException.NotFound( $"No application '{appId}' is defined." );
				}

				// nobody to tell: the request would go nowhere, and a 202 would say it had been done
				if( _master.GetClientState( appId.MachineId ) is not { Connected: true } )
					throw new HttpException( HttpStatusCode.Conflict, $"Machine {appId.MachineId} is not connected." );

				var st = _master.GetAppState( appId );
				wasRunning = st?.Running ?? false;
				oldPid = st?.PID ?? 0;
				failedBefore = st?.StartFailed ?? false;

				switch( what )
				{
					case "start": _master.StartApp( "", appId, planName, 0, vars ); break;
					case "kill": _master.KillApp( "", appId ); break;
					default: _master.RestartApp( "", appId, vars ); break;
				}
			} );

			var reached = false;

			if( wait > 0 )
			{
				var failed = false;

				bool Done()
				{
					var st = _master.GetAppState( appId );
					if( st is null ) return false;

					if( what == "kill" ) return !st.Running && !st.Dying;

					// A start that failed will not become running: say so now rather than at the end of the wait.
					// Only a failure that appears during the wait counts - the agent clears and sets the flag within
					// one launch, so a flag left from an earlier failure cannot be told from a new one.
					if( st.StartFailed && !failedBefore ) { failed = true; return true; }

					if( what == "restart" ) return st.Running && st.Initialized && st.PID != oldPid;
					return st.Running && st.Initialized;
				}

				await Task.Delay( 250 );

				reached = await ApiOps.WaitFor( _master, Done, wait );
				if( failed )
					throw new HttpException( HttpStatusCode.Conflict, $"Application {appId} failed to start - see the log of the agent on {appId.MachineId}." );
				if( !reached )
					throw ApiOps.Timeout( what == "kill" ? $"The death of {appId}" : $"Application {appId} running and initialized", wait );
			}

			var res = await ApiOps.OnMaster( _master, () => AppStateOf( appId, planName ) );
			HttpContext.Response.StatusCode = (int) ( reached ? HttpStatusCode.OK : HttpStatusCode.Accepted );
			return res;
		}

		// ---- the system -------------------------------------------------------------------------------

		[Route( HttpVerbs.Post, "/killall" )]
		public async Task KillAll()
		{
			var body = await ApiOps.ReadBody<KillAllBody>( HttpContext );
			var machine = body?.machine;

			await ApiOps.OnMaster( _master, () =>
			{
				if( !string.IsNullOrEmpty( machine ) && _master.GetMachineDef( machine ) is null && _master.GetClientState( machine ) is null )
					throw HttpException.NotFound( $"No machine '{machine}' is known." );

				_master.KillAll( "", new KillAllArgs() { MachineId = machine ?? string.Empty } );
			} );

			await Accepted( new { message = string.IsNullOrEmpty( machine ) ? "Kill All was issued." : $"Kill All was issued for machine {machine}." } );
		}

		[Route( HttpVerbs.Post, "/sharedconfig/reload" )]
		public async Task ReloadSharedConfig()
		{
			var body = await ApiOps.ReadBody<ReloadBody>( HttpContext );

			await ApiOps.OnMaster( _master, () =>
			{
				try
				{
					// like the CLI: the applications are killed first unless told not to
					_master.ReloadSharedConfig( "", new ReloadSharedConfigArgs() { KillApps = body?.killApps ?? true, FileName = body?.file ?? string.Empty } );
				}
				catch( Exception e ) when ( e is System.IO.FileNotFoundException or System.IO.DirectoryNotFoundException )
				{
					throw HttpException.BadRequest( Tools.JustFirstLine( e.Message ) );
				}
				catch( Exception e ) when ( e is System.Xml.XmlException )
				{
					throw HttpException.BadRequest( "The shared config is not valid XML: " + Tools.JustFirstLine( e.Message ) );
				}
			} );

			// the reload is done when the call returns: it is made on the master's own thread
			await Json( new { message = "The shared config was reloaded.", path = _master.SharedConfigFile, loadedAt = _master.SharedConfigLoadedAt } );
		}

		[Route( HttpVerbs.Post, "/vars" )]
		public async Task SetVars()
		{
			var body = await ApiOps.ReadBody<VarsBody>( HttpContext );
			var vars = ApiOps.CheckVars( body?.vars ) ?? throw HttpException.BadRequest( "A body {\"vars\":{\"NAME\":\"value\"}} is needed." );

			await ApiOps.OnMaster( _master, () => _master.SetVars( "", string.Join( "::", vars.Select( x => $"{x.Key}={x.Value}" ) ) ) );

			await Accepted( new { message = $"{vars.Count} variable(s) were sent to the agents." } );
		}

		// ---- scripts ----------------------------------------------------------------------------------

		static string? ArgsText( JsonElement? args )
		{
			if( args is null ) return null;
			return args.Value.ValueKind switch
			{
				JsonValueKind.Undefined or JsonValueKind.Null => null,
				JsonValueKind.String => args.Value.GetString(),
				_ => args.Value.GetRawText(),
			};
		}

		static Guid ParseGuid( string text, string what )
			=> Guid.TryParse( text, out var g ) ? g : throw HttpException.BadRequest( $"'{text}' is not a {what} guid." );

		// the same operation under two names: scriptdefs/{id}/start is of a piece with plandefs and appdefs, scripts/{id}/start pairs with scripts/{id}/kill
		[Route( HttpVerbs.Post, "/scriptdefs/{id}/start" )]
		[Route( HttpVerbs.Post, "/scripts/{id}/start" )]
		public async Task StartScriptDef( string id )
		{
			var guid = ParseGuid( id, "script" );
			var body = await ApiOps.ReadBody<ScriptDefStartBody>( HttpContext );
			var args = ArgsText( body?.args );
			var wait = ApiOps.WaitSeconds( HttpContext );

			await ApiOps.OnMaster( _master, () =>
			{
				if( _master.GetScriptDef( guid ) is null ) throw HttpException.NotFound( $"no script definition {id}; start a script by name with POST /api/scripts" );
				_master.StartSingletonScript( "", guid, args );
			} );

			await AnswerScript( guid, wait );
		}

		[Route( HttpVerbs.Post, "/scripts" )]
		public async Task StartScript()
		{
			var body = await ApiOps.ReadBody<ScriptStartBody>( HttpContext ) ?? throw HttpException.BadRequest( "A body {\"name\":\"BuiltIns/...\"} is needed." );
			if( string.IsNullOrWhiteSpace( body.name ) ) throw HttpException.BadRequest( "'name' - the script to start - is needed." );

			var guid = string.IsNullOrEmpty( body.id ) ? Guid.NewGuid() : ParseGuid( body.id, "script" );
			var args = ArgsText( body.args );
			var wait = ApiOps.WaitSeconds( HttpContext );
			var machine = body.machine;

			await ApiOps.OnMaster( _master, () =>
			{
				if( string.IsNullOrEmpty( machine ) )
				{
					// the master can tell whether it has such a script; an agent's own script files it cannot see
					if( !_master.ScriptFactory.Exists( body.name! ) )
						throw HttpException.NotFound( $"No script '{body.name}' - not a built-in one, and no such file next to the shared config." );

					_master.StartSingletonScript( "", guid, body.name, args );
				}
				else
				{
					if( _master.GetClientState( machine ) is null && _master.GetMachineDef( machine ) is null )
						throw HttpException.NotFound( $"No machine '{machine}' is known." );

					_master.StartScriptOn( "", machine, guid, body.name!, args, string.IsNullOrEmpty( body.title ) ? body.name! : body.title );
				}
			} );

			await AnswerScript( guid, wait );
		}

		/// <summary>202 {id} - or, when waiting, the state the script ended in, with what it returned.</summary>
		async Task AnswerScript( Guid guid, double wait )
		{
			if( wait <= 0 )
			{
				await Accepted( new { id = guid.ToString() } );
				return;
			}

			await Task.Delay( 300 );

			if( !await ApiOps.WaitFor( _master, () => _master.GetScriptState( guid )?.IsAlive == false, wait ) )
				throw ApiOps.Timeout( $"The end of script {guid}", wait );

			var res = await ApiOps.OnMaster( _master, () =>
			{
				var st = _master.GetScriptState( guid )!;

				object? data = st.Data;
				if( !string.IsNullOrEmpty( st.Data ) )
				{
					try { data = JsonDocument.Parse( st.Data ).RootElement.Clone(); }
					catch( JsonException ) { }
				}

				return new ScriptRunResult()
				{
					id = guid.ToString(),
					state = new ScriptStateDetails( st ),
					status = st.Status.ToString(),
					text = st.Text,
					progress = st.Progress,
					data = data,
				};
			} );

			await Json( res );
		}

		[Route( HttpVerbs.Post, "/scripts/{id}/kill" )]
		public async Task KillScript( string id )
		{
			await ApiOps.DiscardBody( HttpContext );
			var guid = ParseGuid( id, "script" );

			await ApiOps.OnMaster( _master, () =>
			{
				if( _master.GetScriptState( guid ) is null && _master.GetScriptDef( guid ) is null )
					throw HttpException.NotFound( $"No script '{id}' is known." );

				_master.KillScript( "", guid );
			} );

			await Accepted( new { message = $"Script {guid} was told to stop." } );
		}
	}
}
