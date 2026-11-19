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

	/// <summary>What a plan's marking of files came to; see Plans.md. Part of the planstate answer only once a plan has marked.</summary>
	public class PlanMarkResultDetails
	{
		public int marked { get; set; }
		public int cleared { get; set; }
		public int skipped { get; set; }
		public int failed { get; set; }
		public bool timedOut { get; set; }
		public bool cancelled { get; set; }
		public List<string> errors { get; set; } = new();

		public PlanMarkResultDetails() { }

		public PlanMarkResultDetails( Dirigent.PlanMarkResult r )
		{
			marked = r.Marked; cleared = r.Cleared; skipped = r.Skipped; failed = r.Failed;
			timedOut = r.TimedOut; cancelled = r.Cancelled; errors = r.Errors.ToList();
		}
	}

	/// <summary>
	/// The state of a plan that is marking files or has marked them. A separate type so that a plan that never does
	/// has the same answer as ever - the extra fields are there only when they mean something.
	/// </summary>
	public class PlanStateDetailsWithMarking : PlanStateDetails
	{
		/// <summary>What the plan is doing before it launches the first app, e.g. "Marking files (pkg.logs)"; absent when it is not.</summary>
		public string phase { get; set; } = string.Empty;
		public PlanMarkResultDetails? markResult { get; set; }

		public PlanStateDetailsWithMarking() { }

		public PlanStateDetailsWithMarking( Dirigent.PlanState ps ) : base( ps )
		{
			phase = ps.Phase;
			markResult = ps.MarkResult is null ? null : new PlanMarkResultDetails( ps.MarkResult );
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
			this.state = ps.Phase.Length > 0 || ps.MarkResult is not null ? new PlanStateDetailsWithMarking( ps ) : new PlanStateDetails( ps );
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
		public async Task<IEnumerable<PlanDef>> GetAllPlansDef()
		{
			List<PlanDef> res = new();
			var op = _master.AddSynchronousOp( () =>
			{
				res = (from pd in _master.GetAllPlansDef() select new PlanDef( pd )).ToList();
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
			if( op.Exception != null ) throw op.Exception;
			return res;
		}

		// Gets one concrete plans state.
		[Route( HttpVerbs.Get, "/planstates/{id}" )]
		// the plural is the name documented; the singular is the older one and stays
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
			if( op.Exception != null ) throw op.Exception;
			return res;
		}

		// Gets all plans states.
		[Route( HttpVerbs.Get, "/planstates" )]
		public async Task<IEnumerable<PlanState>> GetAllPlansState()
		{
			List<PlanState> res = new();
			var op = _master.AddSynchronousOp( () =>
			{
				res = (from kv in _master.GetAllPlansState() select new PlanState( kv.Key, kv.Value )).ToList();
			} );
			await op.WaitAsync();
			if( op.Exception != null ) throw op.Exception;
			return res;
		}

	}
}
