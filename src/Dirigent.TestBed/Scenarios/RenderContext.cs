using System;
using System.Collections.Generic;
using System.IO;

namespace Dirigent.TestBed.Scenarios
{
	/// <summary>
	/// The concrete facts a scenario needs before it can be turned into config files: where the
	/// run's folders are and what the test application is called. The scenario itself stays free
	/// of paths so it can be written once and rendered for any tier.
	/// </summary>
	public class RenderContext
	{
		public string TempRoot { get; }
		public string TestAppPath { get; }

		readonly Dictionary<string, string> _machineIds;

		public RenderContext( string tempRoot, string testAppPath, IReadOnlyDictionary<string, string>? machineIds = null )
		{
			TempRoot = tempRoot;
			TestAppPath = testAppPath;
			_machineIds = machineIds is null
							? new Dictionary<string, string>()
							: new Dictionary<string, string>( machineIds );
		}

		/// <summary>The real machine id for a name used in the scenario. Identity unless mapped.</summary>
		public string MachineId( string machineName )
			=> _machineIds.TryGetValue( machineName, out var id ) ? id : machineName;

		/// <summary>Working folder of an application, also its StartupDir by default.</summary>
		public string AppDir( string machineName, string appId )
			=> Path.Combine( TempRoot, "apps", MachineId( machineName ), appId );

		/// <summary>Where an application's log files go, and what its log VFS node points at.</summary>
		public string AppLogsDir( string machineName, string appId )
			=> Path.Combine( AppDir( machineName, appId ), "logs" );

		/// <summary>Folder for files belonging to a machine rather than to an application.</summary>
		public string MachineDir( string machineName )
			=> Path.Combine( TempRoot, "machines", MachineId( machineName ) );

		/// <summary>
		/// Substitutes the placeholders a scenario or a raw XML fragment may use.
		/// </summary>
		public string Substitute( string text, string? machineName = null, string? appId = null )
		{
			text = text.Replace( "{temp}", TempRoot );
			text = text.Replace( "{testapp}", TestAppPath );

			foreach( var (name, id) in _machineIds )
				text = text.Replace( "{" + name + "}", id );

			if( machineName is not null && appId is not null )
			{
				text = text.Replace( "{appdir}", AppDir( machineName, appId ) );
				text = text.Replace( "{applogs}", AppLogsDir( machineName, appId ) );
			}

			if( machineName is not null )
				text = text.Replace( "{machinedir}", MachineDir( machineName ) );

			return text;
		}
	}
}
