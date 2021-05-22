using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System;

namespace Dirigent
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
	/// List of currently known plans including their current status
	/// </summary>
	public class PlanRegistry
	{
		private Dictionary<string, Plan> _plans = new Dictionary<string, Plan>();
		public Dictionary<string, Plan> Plans => _plans;

		public Dictionary<string, PlanState> PlanStates => Plans.Values.ToDictionary( p => p.Name, p => p.State );

		public Action<PlanDef>? PlanDefUpdated;


		Master _master;
		public PlanRegistry( Master master )
		{
			_master = master;
		}
		
		public void SetAll( IEnumerable<PlanDef> allDefs )
		{
			_plans.Clear();

			foreach( var pd in allDefs )
			{
				var plan = new Plan( pd, _master );

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
		//[return: MaybeNull]
		public PlanApp FindAppInPlan( string planName, AppIdTuple id )
		{
			return FindPlan( planName ).FindApp( id );
		}

		public void Tick()
		{
			foreach( var p in _plans.Values )
			{
				p.Tick();
			}
		}

		// send updated appdef to everybody interested
		public void AppDefUpdated( string planName, AppIdTuple id )
		{
			var plan = FindPlan( planName ); // throws if failed
			var app = plan.FindApp( id ); // throws if failed
			PlanDefUpdated?.Invoke( plan.Def );
		}



	}
}
