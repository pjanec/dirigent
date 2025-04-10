using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent;

// Saves the status of local apps to a status file for the purpose of post-crash recovery.
// On restore, adopts the processes that are still running.
// If the status file exists on Dirigent startup, it means that the Dirigent have not terminated gracefuly (crashed or was killed etc.)
// Does not include stuff happening on master only:
//  - script status
//  - plan status
public class AgentStateSaverLoader : Disposable
{
	private static readonly log4net.ILog log = log4net.LogManager.GetLogger( System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType );

	static JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
	{
		TypeNameHandling = TypeNameHandling.All, // save runtime types
		Formatting = Formatting.Indented,
	};


	string _statusFilePath;
	LocalAppsRegistry _localAppsRegistry;

	public AgentStateSaverLoader( string machineId, LocalAppsRegistry localAppsRegistry )
	{
		_localAppsRegistry = localAppsRegistry;

		_statusFilePath = GetStatusFilePath(machineId);
	}

	protected override void Dispose( bool disposing )
	{
		if( disposing )
		{
			// this happens on graceful exit
			Clear();
		}
		base.Dispose( disposing );
	}

	public static string GetStatusFilePath( string machineId )
	{
		string localAppDataPath = Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData );
		string appSpecificPath = Path.Combine( localAppDataPath, "Dirigent" );
		return Path.Combine( appSpecificPath, $"agent_status_{machineId}.json" );
	}

	// Deletes the status file.
	// This should be done on graceful exit.
	public void Clear()
	{
		if( !Exists() )
			return;

		File.Delete( _statusFilePath );
	}

	// return true if saved state exists
	public bool Exists()
	{
		return File.Exists( _statusFilePath );
	}


	class LocalAppStatus
	{
		public int? ProcessId;
		public string? ProcessName;
		//public DateTime ProcessStartTime;	 // TODO: add support for this, for safer detection of still running processes.
		public Dictionary<string, string> EnvVars = new();
		public AppDef? Def;

		public LocalAppStatus()
		{
		}

		public LocalAppStatus( LocalApp localApp )
		{
			var proc = localApp.Process;
			if( proc != null )
			{
				ProcessId = proc.Id;
				ProcessName = proc.ProcessName;
				EnvVars = new( localApp.RecentVars );
				Def = localApp.RecentAppDef;
			}
		}
	}

	class AgentStatus
	{
		public Dictionary<string, LocalAppStatus> Apps = new(); // appId => app status
	}


	// Creates or overwrites the file with the current status.
	// This should be called periodically and after each change like StartApp/KillApp.
	public void Save()
	{
		var agentStatus = new AgentStatus();
		agentStatus.Apps = _localAppsRegistry.Apps
			.Where( kv => kv.Value.AppState.Running )
			.ToDictionary( kv => kv.Key.ToString(), kv => new LocalAppStatus( kv.Value ) );

		// write to file
		Directory.CreateDirectory( Path.GetDirectoryName( _statusFilePath )??"" );
		var content = JsonConvert.SerializeObject( agentStatus, _jsonSerializerSettings );
		File.WriteAllText( _statusFilePath, content );
	}

	// tries to adopt
	public bool Restore()
	{
		if( !Exists() )
			return false;

		log.Debug( $"Restoring agent status from {_statusFilePath}" );


		// load status from file
		AgentStatus agentStatus = new();
		try
		{
			var content = File.ReadAllText( _statusFilePath );
			agentStatus = JsonConvert.DeserializeObject<AgentStatus>( content, _jsonSerializerSettings ) ?? new AgentStatus();
		}
		catch( Exception ex )
		{
			log.Error( $"Error loading status file: {ex.Message}" );
			return false;
		}

		var processes = System.Diagnostics.Process.GetProcesses();

		// go over saved apps
		// if they are among currently known local apps, try to adopt it
		foreach( var (appId, appState) in agentStatus.Apps )
		{
			var appIdTuple = new AppIdTuple( appId );

			if( _localAppsRegistry.Apps.TryGetValue( appIdTuple, out var localApp ) )
			{
				// if app was running back then, try to adopt it if it is still running
				if( appState.ProcessId != null )
				{
					var pid = appState.ProcessId.Value;

					// try to find the process
					var proc = processes.FirstOrDefault( p => p.Id == pid && p.ProcessName == appState.ProcessName );
					if( proc != null )
					{
						if( appState.Def is null )
							log.Error( $"AppDef is null for {appId} in status file" );
						else
						{
							localApp.AdoptRunning( pid, appState.Def, appState.EnvVars );
						}
					}
				}
			}
		}

		return true;
	}
}
