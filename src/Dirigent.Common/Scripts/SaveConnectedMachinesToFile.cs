using Dirigent;
using System.Collections.Generic;
using System.Linq;

public class SaveConnectedMachinesToFile : Script
{
	public class Parameters
	{
		public string OutputFile = ""; // the name of the output file
	}

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
		var args = Deserialize<Parameters>(Args);
		if( args is null ) throw new System.NullReferenceException("No args provided");
		if( string.IsNullOrEmpty(args.OutputFile) ) throw new System.NullReferenceException("OutputFile not specified");

		var allClientsState = await GetAllClientsState();
		var machines = allClientsState
				.Where( x => x.Value.Ident!.IsAgent  // ignore non-agent clients (like GUIs etc.)
				          && x.Value.Connected ) // take just currently connected ones
				.Select( x => new Machine { Name = x.Key, IP = x.Value.IP } )
				.ToList();
		
		var result = new Result() { Machines = machines };

		Newtonsoft.Json.JsonSerializerSettings settings = new Newtonsoft.Json.JsonSerializerSettings();
		settings.Formatting = Newtonsoft.Json.Formatting.Indented;
		var json = Newtonsoft.Json.JsonConvert.SerializeObject(result, settings);
		
		System.IO.File.WriteAllText( args.OutputFile, json );

		return null;
	}
}
