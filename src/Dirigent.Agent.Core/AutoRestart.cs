using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent;

#pragma warning disable 8600, 8602, 8604 // Dereference of a possibly null reference

public class AutoRestart
{
	private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

    const string StarterApp = "Dirigent.Starter.exe";

	public static void RunAutorestarter( AppConfig ac )
	{
		var args = Environment.GetCommandLineArgs().ToList();

		// If there is no PID given on command line, it means we have been started by the user, not the autorestarter.
		// In such a case we run the autorestarter and give it our PID.
		if( args.Any( arg => arg.StartsWith( "--pid", StringComparison.OrdinalIgnoreCase ) ) )
			return; // pid present, we were started from the Starter, no need to run it

		string thisProcessPath = Process.GetCurrentProcess().MainModule.FileName;
		string thisProcessDir = Path.GetDirectoryName( thisProcessPath );
		string starterAppPath = Path.Combine( thisProcessDir, StarterApp );

		// if there is no "--machineId" argument present in the args list, add one - Starter needs it
		var machineIdArg = $"--machineId {ac.MachineId}";
		if( !args.Any( arg => arg.StartsWith( "--machineId", StringComparison.OrdinalIgnoreCase ) ) )
		{
			args.Add( machineIdArg );
		}

		// add our pid for the Starter so it knows what process to monitor for crash
		var thisProcessPid = Process.GetCurrentProcess().Id;
		var pidArg = $"--pid {thisProcessPid}";
		args.Add( pidArg );

		string argsJoined = string.Join( " ", args );

		log.Debug( $"Starting {StarterApp} with args: {argsJoined}" );

		Process process = new Process();
		process.StartInfo.FileName = StarterApp;
		process.StartInfo.Arguments = argsJoined;
		process.StartInfo.CreateNoWindow = true;
		process.StartInfo.UseShellExecute = false;
		process.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();

		try
		{
			process.Start();
		}
		catch( Exception ex )
		{
			log.Error( ex.ToString() );
		}
	}
}
