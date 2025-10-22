using Dirigent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

public class KillAllAppsOnMachine : Script
{
	public class Parameters
	{
		public string MachineName = ""; // the name of the machine where to kill apps
		public double TimeoutSeconds = 10; // how long to wait for apps to die before giving up
	}

	public class Result
	{
		public int NumLeftRunning = 0; // number of apps that should have been killed but were still running after timeout
	}

	protected async override System.Threading.Tasks.Task<string?> Run()
	{
		var args = Deserialize<Parameters>(Args);
		if( args is null ) throw new System.NullReferenceException("No args provided");

		var result = new Result() { NumLeftRunning = 0 };

		// find all apps that might be running on the specified machine
		var allAppsDef = await GetAllAppsDef();
		var appsOnMachine = allAppsDef
			.Where( x => x.Value.Id.MachineId.Equals( args.MachineName, System.StringComparison.OrdinalIgnoreCase ) )
			.Select( x => x.Key )
			.ToList();

		
		// kill them all
		foreach( var appId in appsOnMachine )
		{
			await KillApp( appId.ToString() );
		}

		// wait for them to die until timeout
		var timeout = System.DateTime.UtcNow.AddSeconds( args.TimeoutSeconds );
		while( System.DateTime.UtcNow < timeout )
		{
			// check how many are still running
			var allAppsState = await GetAllAppsState();
			var stillRunning = allAppsState
				.Where( x => appsOnMachine.Contains( x.Key ) && (x.Value.Running || x.Value.Dying) )
				.Select( x => x.Key )
				.ToList();

			result.NumLeftRunning = stillRunning.Count;

			await SetStatus($"{stillRunning.Count} apps to kill...");

			if( stillRunning.Count == 0 )
			{
				// all done
				break;
			}

			// wait a bit before next check
			await Wait( 500 );
		}

		return Serialize( result );
	}
}
