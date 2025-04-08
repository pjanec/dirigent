using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Dirigent.Scripts.BuiltIn
{

	/// <summary>
	/// Collects all machine names from given plan, waits for all of them to be online and then starts the plan.
	/// </summary>
	public class RunPlanWhenMachinesOnline : Script
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

		public static readonly string _Name = "BuiltIns/RunPlanWhenMachinesOnline.cs";

		public class TArgs
		{
			/// <summary>
			/// What plan to run when all machines are online. This plan is scanned for apps' machine names.
			/// </summary>
			public string Plan = "";

			/// <summary>
			/// How many seconds to wait for all machines to be online; if 0 then wait indefinitely.
			/// If the timeout is reached the script fails (plan is not started).
			/// </summary>
			public double Timeout = 0;
		};

		
		protected override async Task<string?> Run()
		{
			var args = Deserialize<TArgs>( Args );
			if( args is null ) throw new NullReferenceException("No args provided");
			if( string.IsNullOrEmpty( args.Plan ) ) throw new NullReferenceException("Plan name not specified");

			var machines = await ExtractMachinesFromPlan( args.Plan );

			await WaitForMachinesOnline( machines, args.Timeout );

			await SetStatus( $"Starting plan {args.Plan}" );
			await StartPlan( args.Plan );
			
			// nothing to return...
			return null;
		}

		async Task<List<string>> ExtractMachinesFromPlan( string planName )
		{
			var plan = await GetPlanDef( planName );
			if( plan is null ) throw new Exception($"Plan '{planName}' not found");

			var machines = new List<string>();
			foreach( var app in plan.AppDefs )
			{
				machines.Add( app.Id.MachineId );
			}

			return machines.Distinct().ToList();
		}

		async Task WaitForMachinesOnline( List<string> machines, double timeoutSec )
		{
			var startTime = DateTime.Now;
			var timeout = TimeSpan.FromSeconds( timeoutSec );

			var machinesOffline = new List<string>( machines );
			
			while( true )
			{
				if( CancellationToken.IsCancellationRequested )
				{
					throw new TaskCanceledException();
				}

				int numOffline = 0;
				foreach( var machine in machines )
				{
					var state = await GetClientState( machine );
					if( state is null || !state.Connected )
					{
						numOffline++;
					}
					else
					{
						machinesOffline.Remove( machine );
					}
				}


				if( numOffline == 0 )
				{
					await SetStatus( $"All machines online" );
					break;
				}
				else
				{
					await SetStatus( $"Waiting for {numOffline} more machines. [{string.Join( ", ", machinesOffline )}]" );
				}


				if( timeoutSec > 0 && DateTime.Now - startTime > timeout )
				{
					throw new Exception($"Timed out waiting for machines: [{string.Join(", ", machinesOffline )}]");
				}

				await Task.Delay( 1000 );
			}
		}

	}

}
