using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using EmbedIO;
using EmbedIO.WebApi;
using EmbedIO.Routing;
using System.Linq;

namespace Dirigent.Web
{
    public class AppStateDetails
    {
        public string code { get; set; } = string.Empty;
		public string flags { get; set; } = string.Empty;

		/// <summary>The plan the application was last started in; null for none.</summary>
		public string? plan { get; set; }

		/// <summary>The process id while it runs; null when it does not.</summary>
		public int? pid { get; set; }

		/// <summary>When the current (or last) process was started, and when it ended (null while it runs) - UTC, by its agent's clock.</summary>
		public DateTime? startedAt { get; set; }
		public DateTime? endedAt { get; set; }

		/// <summary>How the process ended: only once it was started and is no longer running, and was not killed.</summary>
		public int? exitCode { get; set; }

		/// <summary>Restarts left before the agent gives up; -1 = without limit; null = not counted (never restarted).</summary>
		public int? restartsRemaining { get; set; }

		/// <summary>CPU usage in percent, GPU usage in percent and memory in MB of the process, as its agent last measured.</summary>
		public float cpu { get; set; }
		public float gpu { get; set; }
		public float memoryMB { get; set; }

		/// <summary>Seconds since the agent last reported this state, null if it never has; over 3 the state is considered offline.</summary>
		public double? reportAgeSeconds { get; set; }
		public bool offline { get; set; }

		public AppStateDetails()
		{
		}

		public AppStateDetails( Dirigent.AppDef appDef, Dirigent.PlanState? planState, Dirigent.AppState appState )
		{
			code = Tools.GetAppStateText( appState, planState, appDef );
			flags = Tools.GetAppStateFlags( appState );
			plan = string.IsNullOrEmpty( appState.PlanName ) ? null : appState.PlanName;
			pid = appState.Running && appState.PID > 0 ? appState.PID : null;
			startedAt = appState.StartedAt;
			endedAt = appState.EndedAt;
			exitCode = appState.Started && !appState.Running && !appState.Killed ? appState.ExitCode : null;
			restartsRemaining = appState.RestartsRemaining == Dirigent.AppState.RESTARTS_UNITIALIZED ? null : appState.RestartsRemaining;
			cpu = appState.CPU;
			gpu = appState.GPU;
			memoryMB = appState.Memory;
			// a state no agent has reported yet carries the zero time, not a moment
			reportAgeSeconds = appState.LastChange.Year < 2000 ? null : Math.Round( ( DateTime.UtcNow - appState.LastChange ).TotalSeconds, 1 );
			offline = appState.IsOffline;
		}
    }

    public class AppState
    {
        public string id { get; set; } = string.Empty;

		/// <summary>The application's Component; null if it names none.</summary>
		public string? component { get; set; }
        public AppStateDetails state { get; set; } = new();

		/// <summary>
		/// The last unexpected exits of the application (it ended and Dirigent had not killed it), newest first, at most 10 -
		/// kept by the master in memory, so lost when it restarts. Null where the route does not tell.
		/// </summary>
		[System.Text.Json.Serialization.JsonIgnore( Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull )]
		public List<Dirigent.Crashes.AppExit>? lastExits { get; set; }

		public AppState()
		{
		}

		public AppState( Dirigent.AppDef appDef, Dirigent.PlanState? planState, Dirigent.AppState appState, List<Dirigent.Crashes.AppExit>? lastExits = null )
		{
			this.lastExits = lastExits;
			this.id = appDef.ToString();
			this.component = string.IsNullOrEmpty( appDef.Component ) ? null : appDef.Component;
			this.state = new AppStateDetails( appDef, planState, appState );
		}
    }

    public class AppDef
    {
        public string id { get; set; } = string.Empty;
        //public string ExeFullPath { get; set; } = string.Empty;
        //public string CmdLineArgs { get; set; } = string.Empty;
		public string groups { get; set; } = string.Empty;

		/// <summary>The software component the application belongs to (its Component attribute); null if it names none.</summary>
		public string? component { get; set; }

		/// <summary>The machine the application runs on.</summary>
		public string machine { get; set; } = string.Empty;

		/// <summary>The program, exactly as the config declares it (variables not expanded).</summary>
		public string exeFullPath { get; set; } = string.Empty;

		/// <summary>The working folder, as declared.</summary>
		public string startupDir { get; set; } = string.Empty;

		/// <summary>The command line arguments, as declared.</summary>
		public string cmdLineArgs { get; set; } = string.Empty;

		public AppDef()
		{
		}

		public AppDef( Dirigent.AppDef x )
		{
			id = x.Id.ToString();
			groups = x.Groups;
			component = string.IsNullOrEmpty( x.Component ) ? null : x.Component;
			machine = x.Id.MachineId;
			exeFullPath = x.ExeFullPath;
			startupDir = x.StartupDir;
			cmdLineArgs = x.CmdLineArgs;
		}
    }

    public class AppApiController : WebApiController
    {
		private Master _master;

		public AppApiController( Master master )
		{
			_master = master;
		}

		// Gets all apps defs.
		[Scope( ScopeAttribute.Read )]
		[Route( HttpVerbs.Get, "/appdefs" )]
		public async Task<IEnumerable<AppDef>> GetAllAppsDef()
		{
			List<AppDef> res = new List<AppDef>();
			var op = _master.AddSynchronousOp( () =>
			{
				res = (from x in _master.GetAllAppsDef() select new AppDef( x.Value )).ToList();
			} );
			await op.WaitAsync();
			if( op.Exception != null ) throw op.Exception;
			return res;
		}

		// Gets single app def.
		[Scope( ScopeAttribute.Read )]
		[Route( HttpVerbs.Get, "/appdefs/{id}" )]
		public async Task<AppDef> GetAppDef( string id )
		{
			AppDef res = new AppDef();
			var op = _master.AddSynchronousOp( () =>
			{
				var (appIdTuple, planName) = ApiOps.ParseAppId( id );

				var ad = _master.GetAppDef( appIdTuple );

				if( ad is null )
					throw HttpException.NotFound( $"No application '{appIdTuple}' is defined." );

				res = new AppDef( ad );
			} );
			await op.WaitAsync();
			if( op.Exception != null ) throw op.Exception;
			return res;
		}

		// Where the binary and the working folder of an app are, as its agent resolves them.
		// id is "m1.a" or "m1.a@plan1", the definition picked as for the files.
		[Scope( ScopeAttribute.Read )]
		[Route( HttpVerbs.Get, "/appdefs/{id}/location" )]
		public async Task GetAppLocation( string id )
		{
			AppLocationQuery? query = null;
			var op = _master.AddSynchronousOp( () => { query = AppLocationQuery.Prepare( _master, id ); } );
			await op.WaitAsync();

			if( op.Exception is AppFilesNotFoundException )
				throw HttpException.NotFound( op.Exception.Message );
			if( op.Exception is ArgumentSyntaxErrorException )
				throw HttpException.BadRequest( op.Exception.Message );
			if( op.Exception != null ) throw op.Exception;

			var entry = ( await query!.ResolveAsync() ).Single();
			await HttpContext.SendStringAsync( entry.ToJson(), "application/json", ApiOps.Utf8 );
		}

		// The same for all the apps, or those of one machine (?machine=m1).
		[Scope( ScopeAttribute.Read )]
		[Route( HttpVerbs.Get, "/applocations" )]
		public async Task GetAppLocations()
		{
			var machine = HttpContext.GetRequestQueryData()["machine"];

			AppLocationQuery? query = null;
			var op = _master.AddSynchronousOp( () =>
			{
				// unknown like everywhere else - but a machine only an app record names (no <Machine>) is known enough
				if( !string.IsNullOrEmpty( machine ) && _master.GetMachineDef( machine ) is null
					&& !_master.GetAllAppsDef().Any( kv => kv.Key.MachineId == machine ) )
					throw HttpException.NotFound( $"No machine '{machine}' is known." );

				query = AppLocationQuery.PrepareAll( _master, machine );
			} );
			await op.WaitAsync();
			if( op.Exception != null ) throw op.Exception;

			var entries = await query!.ResolveAsync();
			await HttpContext.SendStringAsync( AppFilesResult.ToJson( entries ), "application/json", ApiOps.Utf8 );
		}

		// The versions of the applications, each listed once with the machines and apps that have it (?machine=m1).
		[Scope( ScopeAttribute.Read )]
		[Route( HttpVerbs.Get, "/appversions" )]
		public async Task GetAppVersions()
		{
			var machine = HttpContext.GetRequestQueryData()["machine"];

			AppLocationQuery? query = null;
			var op = _master.AddSynchronousOp( () =>
			{
				if( !string.IsNullOrEmpty( machine ) && _master.GetMachineDef( machine ) is null
					&& !_master.GetAllAppsDef().Any( kv => kv.Key.MachineId == machine ) )
					throw HttpException.NotFound( $"No machine '{machine}' is known." );

				query = AppLocationQuery.PrepareAll( _master, machine );
			} );
			await op.WaitAsync();
			if( op.Exception != null ) throw op.Exception;

			var versions = AppVersions.Of( await query!.ResolveAsync() );
			await HttpContext.SendStringAsync( AppFilesResult.ToJson( versions ), "application/json", ApiOps.Utf8 );
		}

		// Gets the actual files of one app instance, with local and UNC paths.
		// id is "m1.a" or "m1.a@plan1"; ?content=false skips scanning the folders.
		[Scope( ScopeAttribute.Read )]
		[Route( HttpVerbs.Get, "/appdefs/{id}/files" )]
		public async Task GetAppFiles( string id )
		{
			var contentArg = HttpContext.GetRequestQueryData()["content"];
			var includeContent = !string.Equals( contentArg, "false", StringComparison.OrdinalIgnoreCase )
								&& contentArg != "0";

			// the definitions are read on the master thread, the resolving (which waits for the
			// agents) is not - see AppFilesQuery
			AppFilesQuery? query = null;
			var op = _master.AddSynchronousOp( () =>
			{
				query = AppFilesQuery.Prepare( _master, id );
			} );
			await op.WaitAsync();

			if( op.Exception is AppFilesNotFoundException )
				throw HttpException.NotFound( op.Exception.Message );
			if( op.Exception is ArgumentSyntaxErrorException )
				throw HttpException.BadRequest( op.Exception.Message );
			if( op.Exception != null ) throw op.Exception;

			var result = await query!.ResolveAsync( includeContent );

			await HttpContext.SendStringAsync( result.ToJson(), "application/json", ApiOps.Utf8 );
		}

		// Gets the files of any top-level VFS node (a file package, typically), addressed as scripts do.
		// ?machine=m1&app=a narrow the selection; ?content=false skips scanning the folders.
		[Scope( ScopeAttribute.Read )]
		[Route( HttpVerbs.Get, "/filedefs/{id}/files" )]
		public async Task GetFiles( string id )
		{
			var q = HttpContext.GetRequestQueryData();
			var contentArg = q["content"];
			var includeContent = !string.Equals( contentArg, "false", StringComparison.OrdinalIgnoreCase )
								&& contentArg != "0";

			// the matching definitions are found on the master thread, the resolving is not
			FileDefFilesQuery? query = null;
			var op = _master.AddSynchronousOp( () => { query = FileDefFilesQuery.Prepare( _master, id, q["machine"], q["app"] ); } );
			await op.WaitAsync();

			if( op.Exception is AppFilesNotFoundException )
				throw HttpException.NotFound( op.Exception.Message );
			if( op.Exception is ArgumentException )
				throw HttpException.BadRequest( op.Exception.Message );
			if( op.Exception != null ) throw op.Exception;

			var result = await query!.ResolveAsync( includeContent );

			await HttpContext.SendStringAsync( result.ToJson(), "application/json", ApiOps.Utf8 );
		}

		AppState GetAppStateInternal( string id )
		{
			var (appIdTuple, planName) = ApiOps.ParseAppId( id );

			var appDef = _master.GetAppDef( appIdTuple ) ?? throw HttpException.NotFound( $"No application '{appIdTuple}' is defined." );
			if( !string.IsNullOrEmpty( planName ) && _master.GetPlanDef( planName ) is null )
				throw HttpException.NotFound( $"No plan '{planName}' is defined." );
			var planState = _master.GetPlanState( planName ?? "" );
			var appState = _master.GetAppState( appIdTuple ) ?? throw HttpException.NotFound( $"Application '{appIdTuple}' has no state." );

			return new AppState( appDef, planState, appState, _master.ExitHistory.Of( appIdTuple ) );
		}

		// Gets one concrete app state.
		[Scope( ScopeAttribute.Read )]
		[Route( HttpVerbs.Get, "/appstates/{id}" )]
		// the plural is the name documented; the singular is the older one and stays
		[Route( HttpVerbs.Get, "/appstate/{id}" )]
		public async Task<AppState> GetAppState( string id )
		{
			AppState res = new AppState();
			var op = _master.AddSynchronousOp( () =>
			{
				res = GetAppStateInternal( id );
			} );
			await op.WaitAsync();
			if( op.Exception != null ) throw op.Exception;
			return res;
		}

		// Gets all plans states.
		[Scope( ScopeAttribute.Read )]
		[Route( HttpVerbs.Get, "/appstates" )]
		public async Task<IEnumerable<AppState>> GetAllAppsState()
		{
			List<AppState> res = new List<AppState>();
			var op = _master.AddSynchronousOp( () =>
			{
				// a state outlives its definition (see docs/Apps.md); such an app is not listed rather than failing the whole list
				res = (from kv in _master.GetAllAppsState() where _master.GetAppDef( kv.Key ) is not null select GetAppStateInternal( kv.Key.ToString() )).ToList();
			} );
			await op.WaitAsync();
			if( op.Exception != null ) throw op.Exception;
			return res;
		}
	}
}
