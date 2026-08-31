using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Dirigent;

class AgentStarter
{
    const string UnderlyingApp = "Dirigent.Agent.exe";

	// WARNING: requires --machineId <machineName>  argument to be passed in, otherwise it won't find the status file and won't restart dirigent on crash!
	static int Main(string[] args)
    {
		var ac = new AppConfig();
		if( ac.HadErrors )
        {
			Debug.WriteLine( $"[Dirigent.Agent.Starter] Command line parsing error." );
            return 1;
        }

        string StatusFileName = AgentStateSaverLoader.GetStatusFilePath( ac.MachineId, ac.AgentStatusFolder );
        Debug.WriteLine($"[Dirigent.Agent.Starter] Using status file {StatusFileName}");

        var pid = ac.Pid; // PID of the underrlying app, passed by the app itself when it is starteng this Starter

        var argsList = args.ToList();

		// if not --machineId argument is present in the args list, fail
		var machineIdArg = $"--machineId {ac.MachineId}";
		if( !argsList.Any( arg => arg.StartsWith( "--machineId", StringComparison.OrdinalIgnoreCase ) ) )
		{
			Debug.WriteLine( $"[Dirigent.Agent.Starter] Missing --machineId argument." );
            return 2;
		}

		while( true)
        {
			// run the dirigent from the same folder as us
			string thisProcessPath = Process.GetCurrentProcess().MainModule.FileName;
			string thisProcessDir = Path.GetDirectoryName( thisProcessPath );
			string underlyingAppPath = Path.Combine( thisProcessDir, UnderlyingApp );

            string argsJoined = string.Join(" ", args);



            try
            {
                Process process = null;

                if( pid <= 0 ) // no pid given, need to start the underlying app
                {
        			Debug.WriteLine( $"[Dirigent.Agent.Starter] Starting {underlyingAppPath} with args: {argsJoined}" );

                    try
                    {
			            process = new Process();
                        process.StartInfo.FileName = underlyingAppPath;
                        process.StartInfo.Arguments = argsJoined;
                        process.StartInfo.CreateNoWindow = true;
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.WorkingDirectory = Directory.GetCurrentDirectory();

			            process.Start();
                    }
			        catch( Exception ex )
			        {
				        Debug.WriteLine( $"[Dirigent.Agent.Starter] Exception starting {UnderlyingApp}: {ex.Message}" );
				        return 1;
			        }
                }
                else // pid given, find the process and wait for it to crash
                {
        			Debug.WriteLine( $"[Dirigent.Agent.Starter] Adopting {underlyingAppPath} with pid {pid}" );
                    try
                    {
                        process = Process.GetProcessById( pid );
                    }
                    catch( ArgumentException ) // process does not exist - invalid pid given
                    {
				        Debug.WriteLine( $"[Dirigent.Agent.Starter] PID {pid} not found, running new instance of dirigent" );
                        pid = -1; // reset the pid so we start a new instance
						continue; // start a new instance
					}
                    catch( Exception ex )
					{
				        Debug.WriteLine( $"[Dirigent.Agent.Starter] Failed to get process with PID {pid}: {ex}" );
                        return 1;
					}
                }

       			Debug.WriteLine( $"[Dirigent.Agent.Starter] Waiting for {underlyingAppPath} to terminate." );
				try
				{
					process.WaitForExit();
				}
				catch( Exception ex )
				{
					Debug.WriteLine( $"[Dirigent.Agent.Starter] Exception waiting for {UnderlyingApp} to exit: {ex.Message}" );
					return 1;
				}

                Debug.WriteLine($"[Dirigent.Agent.Starter] {UnderlyingApp} exited.");

                if( File.Exists(StatusFileName) )
                {
                    Debug.WriteLine($"[Dirigent.Agent.Starter] {UnderlyingApp} did not exit gracefully. Restarting...");
                }
                else
                {
                    Debug.WriteLine($"[Dirigent.Agent.Starter] {UnderlyingApp} exited gracefully. Exiting.");
                    return 0;
                }

                Thread.Sleep(1000); // Backoff before restart
                pid = -1; // do not try adopting, start new instance
			}
			catch( Exception ex )
			{
				Debug.WriteLine( $"[Dirigent.Agent.Starter] Exception: {ex.Message}" );
				return 1;
			}
		}
    }
}
