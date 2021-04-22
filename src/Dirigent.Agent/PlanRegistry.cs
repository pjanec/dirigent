using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dirigent.Common;

namespace Dirigent.Agent
{
	/// <summary>
	/// Script associated with the plan (optional)
	/// Can calculate plan status, control the application sequencing etc.
	/// Receives plan control commands (StartPlan, StopPlan, KillPlan, RestartPlan)
	/// Ticked each frame.
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class PlanScript
	{
	}


	/// <summary>
	/// Description of plan's current state
	/// </summary>
	public class Plan
	{
		public string Name => this.Def.Name;

		public PlanState State = new();

		public List<AppDef> AppDefs => Def.AppDefs;

		public PlanScript? Script;

		public double StartTimeout { get; set; }

		public System.Collections.Generic.IEnumerable<AppDef> getAppDefs() { return Def.AppDefs; }

		public PlanDef Def;

		public Plan( PlanDef def )
		{
			Def = def;
		}

		/// <summary>
		/// Finds app def by Id. Throws on failure.
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public AppDef FindApp( AppIdTuple id )
		{
			var appDef = AppDefs.Find( (ad) => ad.Id == id );
			if( appDef is not null )
			{
				return appDef;
			}
			else
			{
				throw new UnknownAppInPlanException( id, Name );
			}
		}

	}

	/// <summary>
	/// List of currently known plans including their current status
	/// </summary>
	public class PlanRegistry
	{
		private Dictionary<string, Plan> _plans = new Dictionary<string, Plan>();
		public Dictionary<string, Plan> Plans => _plans;

		public Dictionary<string, PlanState> PlanStates => Plans.Values.ToDictionary( p => p.Name, p => p.State );
		
		public void SetAll( IEnumerable<PlanDef> allDefs )
		{
			_plans.Clear();

			foreach( var pd in allDefs )
			{
				var plan = new Plan( pd );

				_plans[plan.Name] = plan;
			}

		}

		/// <summary>
		/// Finds plan ba name. Throws if failed.
		/// </summary>
		public Plan FindPlan( string planName )
		{
			if( Plans.TryGetValue( planName, out var p ) )
			{
				return p;
			}
			else
			{
				throw new UnknownPlanName( planName );
			}
		}

		/// <summary>
		/// Finds app def in a plan. Throws if failed.
		/// </summary>
		public AppDef FindAppInPlan( string planName, AppIdTuple id )
		{
#pragma warning disable CS8603 // Possible null reference return.
			return FindPlan( planName ).FindApp( id );
#pragma warning restore CS8603 // Possible null reference return.
		}


	}
}
