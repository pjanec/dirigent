using Dirigent;
using System.Collections.Generic;
using System.Linq;

public class GetConnectedMachines : Script
{
	public class Machine
	{
		public string Name = "";
		public string? IP = "";
	}

	public class Result
	{
		public List<Machine> Machines = new List<Machine>();
	}

	protected async override System.Threading.Tasks.Task<string?> Run()
	{
		var allClientsState = await GetAllClientStates();
		var machines = allClientsState
				.Where( x => x.Value.Ident.IsAgent ) // ignore non-agent clients (like GUIs etc.)
				.Select( x => new Machine { Name = x.Key, IP = x.Value.IP } )
				.ToList();
		return Serialize( new Result() { Machines=machines } );
	}
}
