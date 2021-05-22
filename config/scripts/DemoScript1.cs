using Dirigent;

public class DemoScript1 : Script
{
	bool _started_m1_a = false;
	bool _started_m1_b = false;

	public override void Init()
	{
		Coroutine = new Coroutine( Run() );
	}

	public override void Done()
	{
		// kill what was started by us
		if( _started_m1_a )
			Ctrl.KillApp("m1.a");

		if( _started_m1_b )
			Ctrl.KillApp("m1.b");
	}

	System.Collections.IEnumerable Run()
	{
		Ctrl.StartApp( "m1.a", "plan1" );
		_started_m1_a = true;
		
		while ( !Ctrl.GetAppState( "m1.a" ).Initialized ) yield return null;

		Ctrl.StartApp( "m1.b", "plan1" );
		_started_m1_b = true;

		// both apps should be killed in Done() once the coroutine terminates and the script gets disposed

		//yield return new WaitForSeconds(2);
		//Ctrl.KillApp("m1.a");
		
		//yield return new WaitForSeconds(2);
		//Ctrl.KillApp("m1.b");
		
		yield return new WaitForSeconds(2);
		// here to coroutine terminates
	}
}
