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

		public AppDef? FindApp( AppIdTuple appId, string? exceptionRecipient=null )
		{
			var appDef = AppDefs.Find( (ad) => ad.AppIdTuple == appId );
			if( appDef is not null )
			{
				return appDef;
			}
			else
			if( exceptionRecipient is not null )
			{
				throw new RemoteOperationErrorException( exceptionRecipient, $"Plan {Name} does not contain app {appId}" );
			}
			else
			{
				return null;
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

		public Plan? FindPlan( string planName, string? exceptionRecipient=null )
		{
			if( Plans.TryGetValue( planName, out var p ) )
			{
				return p;
			}
			else if( exceptionRecipient is not null )
			{
				throw new RemoteOperationErrorException( exceptionRecipient, $"No plan with name {planName}" );
			}
			else
			{
				return null;
			}
		}

		public AppDef? FindAppInPlan( string planName, AppIdTuple appId, string? exceptionRecipient=null )
		{
			return FindPlan( planName, exceptionRecipient )?.FindApp( appId, exceptionRecipient );
		}


	}
}
