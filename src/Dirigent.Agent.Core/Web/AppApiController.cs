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

		public AppStateDetails()
		{
		}

		public AppStateDetails( Dirigent.AppDef appDef, Dirigent.PlanState? planState, Dirigent.AppState appState )
		{
			code = Tools.GetAppStateText( appState, planState, appDef );
			flags = Tools.GetAppStateFlags( appState );
		}
    }

    public class AppState
    {
        public string id { get; set; } = string.Empty;
        public AppStateDetails state { get; set; } = new();

		public AppState()
		{
		}

		public AppState( Dirigent.AppDef appDef, Dirigent.PlanState? planState, Dirigent.AppState appState )
		{
			this.id = appDef.ToString();
			this.state = new AppStateDetails( appDef, planState, appState );
		}
    }

    public class AppDef
    {
        public string id { get; set; } = string.Empty;
        //public string ExeFullPath { get; set; } = string.Empty;
        //public string CmdLineArgs { get; set; } = string.Empty;
		public string groups { get; set; } = string.Empty;

		public AppDef()
		{
		}

		public AppDef( Dirigent.AppDef x )
		{
			id = x.Id.ToString();
			groups = x.Groups;
		}
    }

    public class AppApiController : WebApiController
    {
		private Master _master;

		public AppApiController( Master master )
		{
			_master = master;
		}

		// Gets all plans defs.
		[Route( HttpVerbs.Get, "/appdefs" )]
		public async Task<IEnumerable<AppDef>> GetAllAppDefs()
		{
			List<AppDef> res = new List<AppDef>();
			var op = _master.AddSynchronousOp( () =>
			{
				res = (from x in _master.GetAllAppDefs() select new AppDef( x.Value )).ToList();
			} );
			await op.WaitAsync();
			if( op.Exception != null ) throw op.Exception;
			return res;
		}

		// Gets single app def.
		[Route( HttpVerbs.Get, "/appdefs/{id}" )]
		public async Task<AppDef> GetAppDef( string id )
		{
			AppDef res = new AppDef();
			var op = _master.AddSynchronousOp( () =>
			{
				var (appIdTuple, planName) = Tools.ParseAppIdWithPlan( id );

				var ad = _master.GetAppDef( appIdTuple );

				if( ad is null )
					throw HttpException.NotFound();

				res = new AppDef( ad );
			} );
			await op.WaitAsync();
			return res;
		}

		AppState GetAppStateInternal( string id )
		{
			var (appIdTuple, planName) = Tools.ParseAppIdWithPlan( id );

			var appDef = _master.GetAppDef( appIdTuple );
			var planState = _master.GetPlanState( planName ?? "" );
			var appState = _master.GetAppState( appIdTuple );

			if( appDef is null || appState is null )
				throw  HttpException.NotFound();

			return new AppState( appDef, planState, appState );
		}

		// Gets one concrete app state.
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
		[Route( HttpVerbs.Get, "/appstates" )]
		public async Task<IEnumerable<AppState>> GetAllAppsStates()
		{
			List<AppState> res = new List<AppState>();
			var op = _master.AddSynchronousOp( () =>
			{
				res = (from kv in _master.GetAllAppStates() select GetAppStateInternal( kv.Key.ToString() )).ToList();
			} );
			await op.WaitAsync();
			if( op.Exception != null ) throw op.Exception;
			return res;
		}
	}
}
