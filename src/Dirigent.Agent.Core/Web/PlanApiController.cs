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
    public class PlanStateDetails
    {
        public string code { get; set; } = string.Empty;
		public string flags { get; set; } = string.Empty;

		public PlanStateDetails()
		{
		}

		public PlanStateDetails( Dirigent.PlanState ps )
		{
			code = Tools.GetPlanStateText( ps );
			flags = Tools.GetPlanStateFlags( ps );
		}
    }

    public class PlanState
    {
        public string id { get; set; } = string.Empty;
        public PlanStateDetails state { get; set; } = new();

		public PlanState()
		{
		}

		public PlanState( string id, Dirigent.PlanState ps )
		{
			this.id = id;
			this.state = new PlanStateDetails( ps );
		}
    }

    public class PlanDef
    {
        public string id { get; set; } = string.Empty;

        public List<AppDef> appDefs  { get; set; } = new();

		public string groups { get; set; } = string.Empty;

		public PlanDef()
		{
		}

		public PlanDef( Dirigent.PlanDef pd )
		{
			id = pd.Name;
			appDefs = (from ad in pd.AppDefs select new AppDef( ad )).ToList();
			groups = pd.Groups;
		}

    }

    public class PlanApiController : WebApiController
    {
		private Master _master;

		public PlanApiController( Master master )
		{
			_master = master;
		}

		// Gets all plans defs.
		[Route( HttpVerbs.Get, "/plandefs" )]
		public async Task<IEnumerable<PlanDef>> GetAllPlansDefs()
		{
			List<PlanDef> res = new();
			var op = _master.AddSynchronousOp( () =>
			{
				res = (from pd in _master.GetAllPlanDefs() select new PlanDef( pd )).ToList();
			} );
			await op.WaitAsync();
			if( op.Exception != null ) throw op.Exception;
			return res;
		}

		// Gets single plan def.
		[Route( HttpVerbs.Get, "/plandefs/{id}" )]
		public async Task<PlanDef> GetPlanDef( string id )
		{
			PlanDef res = new();
			var op = _master.AddSynchronousOp( () =>
			{
				var pd = _master.GetPlanDef( id );

				if( pd is null )
					throw HttpException.NotFound();

				res = new PlanDef( pd );
			} );
			await op.WaitAsync();
			return res;
		}

		// Gets one concrete plans state.
		[Route( HttpVerbs.Get, "/planstate/{id}" )]
		public async Task<PlanState> GetPlanState( string id )
		{
			PlanState res = new();
			var op = _master.AddSynchronousOp( () =>
			{
				var ps = _master.GetPlanState( id );

				if( ps is null )
					throw HttpException.NotFound();

				res = new PlanState( id, ps );
			} );
			await op.WaitAsync();
			return res;
		}

		// Gets all plans states.
		[Route( HttpVerbs.Get, "/planstates" )]
		public async Task<IEnumerable<PlanState>> GetAllPlansStates()
		{
			List<PlanState> res = new();
			var op = _master.AddSynchronousOp( () =>
			{
				res = (from kv in _master.GetAllPlanStates() select new PlanState( kv.Key, kv.Value )).ToList();
			} );
			await op.WaitAsync();
			return res;
		}

	}
}
