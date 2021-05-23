#pragma warning disable CS8602 // Dereference of a possibly null reference.
using Dirigent;

public class DemoScript1 : Script
{
	private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

	bool _started_m1_a = false;
	bool _started_m1_b = false;

	public override void Init()
	{
		log.Info("Init!");
		Coroutine = new Coroutine( Run() );
	}

	public override void Done()
	{
		log.Info("Done!");
		// kill what was started by us
		if( _started_m1_a )
			Ctrl.KillApp("m1.a");

		if( _started_m1_b )
			Ctrl.KillApp("m1.b");
	}

	System.Collections.IEnumerable Run()
	{
		log.Info("Run!");

		// wait for agent m1 to boot
		while( Ctrl.GetClientState("m1") is null ) yield return null;

		// start app "m1.a" defined within "plan1"
		Ctrl.StartApp( "m1.a", "plan1" );
		_started_m1_a = true;
		
		// wait for the app to initialize
		while ( !Ctrl.GetAppState( "m1.a" ).Initialized ) yield return null;

		// start app "m1.b" defined within "plan1"
		Ctrl.StartApp( "m1.b", "plan1" );
		_started_m1_b = true;

		// both apps should be killed in Done() once the coroutine terminates and the script gets disposed

		//yield return new WaitForSeconds(2);
		//Ctrl.KillApp("m1.a");
		
		//yield return new WaitForSeconds(2);
		//Ctrl.KillApp("m1.b");
		
		yield return new WaitForSeconds(2);

		// here to coroutine terminates which deactivates the script
	}
}
