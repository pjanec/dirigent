using Dirigent;

public class DemoScript1 : Script
{
	private static readonly log4net.ILog log =
		log4net.LogManager.GetLogger(
			System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType
		);

	public class Result
	{
		public int Code;
	}

	protected async override System.Threading.Tasks.Task<byte[]?> Run()
	{
		if( TryDeserializeArgs<string>( out var strArgs ) )
		{
			log.Info($"Init with string args: '{strArgs}'");
		}

		log.Info("Run!");

		await SetStatus("Waiting for m1 to boot");

		// wait for agent m1 to boot
		while( await GetClientState("m1") is null )
			await Wait(100);

		// start app "m1.a" defined within "plan1"
		await StartApp( "m1.a", "plan1" );
		
		// wait for the app to initialize
		await SetStatus("Waiting for m1.a to initialize");
		while ( !(await GetAppState("m1.a"))!.Initialized )
			await Wait(100);

		// start app "m1.b" defined within "plan1"
		await StartApp( "m1.b", "plan1" );

		await Wait(2000);

		// run action on where this script was issued from
		await RunAction(
			Requestor,	// we are starting the action on behalf of the original requestor of this script
			new ToolActionDef { Name= "Notepad", Args="C:/Request/From/DemoScript1.cs" },
			Requestor // we want the action to run on requestor's machine
		);

		//SetStatus("Waiting before throwing exception");
		//await Delay(4000);
		//throw new Exception( "Demo exception" );

		await SetStatus("Waiting before terminating");
		await Wait(4000);

		await KillApp( "m1.a" );
		await KillApp( "m1.b" );

		return SerializeResult( new Result { Code = 17 } );
	}
}
