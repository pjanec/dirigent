using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dirigent.TestBed.Scenarios
{
	/// <summary>
	/// A scenario written out as a real world on disk: a SharedConfig.xml, the application folders
	/// with their seeded files, and a description of what was made.
	/// </summary>
	/// <remarks>
	/// The tier-1 bed builds its world in the same way but keeps its components in one process. This
	/// is the same rendering for the tiers that start the shipped executables instead, so that every
	/// tier consumes one scenario model rather than keeping a second copy of what a world looks like.
	/// </remarks>
	public static class WorldOnDisk
	{
		/// <summary>
		/// Renders a scenario preset into a folder and returns what it contains. The folder becomes
		/// the root of the world and every path in it is absolute.
		/// </summary>
		/// <param name="scenarioName">a key of <see cref="Scenario.Presets"/></param>
		/// <param name="root">where to write it; created if missing, written into if not empty</param>
		/// <param name="testAppPath">
		/// the stand-in application the world's applications point at. Found automatically when not
		/// given.
		/// </param>
		public static World Write( string scenarioName, string root, string? testAppPath = null )
			=> Write( Scenario.ByName( scenarioName ), scenarioName, root, testAppPath );

		/// <summary>As above, for a scenario built by hand rather than named.</summary>
		public static World Write( Scenario scenario, string scenarioName, string root,
				string? testAppPath = null )
		{
			root = Path.GetFullPath( root );
			Directory.CreateDirectory( root );

			var testApp = string.IsNullOrEmpty( testAppPath )
					? TestAppLocator.Find()
					: Path.GetFullPath( testAppPath! );

			if( !File.Exists( testApp ) )
				throw new FileNotFoundException( $"test application not found: {testApp}" );

			var ctx = new RenderContext( root, testApp );

			WorldSeeder.Seed( scenario.Spec, ctx );

			var sharedConfig = Path.Combine( root, "SharedConfig.xml" );
			File.WriteAllText( sharedConfig, SharedConfigRenderer.Render( scenario.Spec, ctx ) );

			// An agent insists on a local config: the setting defaults to "LocalConfig.xml" and a
			// missing file takes the process down at startup. A real deployment has one, so a
			// rendered world has one too - empty, since tools and folder watchers belong to no test
			// yet.
			File.WriteAllText( Path.Combine( root, "LocalConfig.xml" ), _localConfigXml );

			// the folders the agents are told to use, so a run touches nothing of a real installation
			foreach( var folder in new[] { "agentstatus", "downloads", "logs" } )
				Directory.CreateDirectory( Path.Combine( root, folder ) );

			return Describe( scenarioName, scenario, ctx, root, testApp, sharedConfig );
		}

		static World Describe( string name, Scenario scenario, RenderContext ctx,
				string root, string testApp, string sharedConfig )
			=> new World()
			{
				Scenario = name,
				Root = root,
				SharedConfig = sharedConfig,
				LocalConfig = Path.Combine( root, "LocalConfig.xml" ),
				TestApp = testApp,
				AgentStatusFolder = Path.Combine( root, "agentstatus" ),
				DownloadFolder = Path.Combine( root, "downloads" ),
				LogFolder = Path.Combine( root, "logs" ),
				Machines = scenario.Spec.Machines.Select( m => m.Name ).ToList(),
				Apps = scenario.Spec.Apps.Select( a => new WorldApp()
				{
					Machine = a.MachineName,
					App = a.AppId,
					IdTuple = $"{a.MachineName}.{a.AppId}",
					Dir = ctx.AppDir( a.MachineName, a.AppId ),
					LogsDir = ctx.AppLogsDir( a.MachineName, a.AppId ),
				} ).ToList(),

				// the ids a caller can name: the per-app nodes and the packages
				VfsNodes = scenario.Spec.Apps.SelectMany( a => a.VfsNodes ).Select( n => n.Id )
						.Concat( scenario.Spec.Packages.Select( p => p.Id ) )
						.Distinct().OrderBy( x => x ).ToList(),
			};

		const string _localConfigXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<!-- written by WorldOnDisk; every agent needs one of these next to its working folder -->
<Local>
</Local>
";

		/// <summary>What was written, and where.</summary>
		public class World
		{
			public string Scenario = "";
			public string Root = "";
			public string SharedConfig = "";
			public string LocalConfig = "";
			public string TestApp = "";
			public string AgentStatusFolder = "";
			public string DownloadFolder = "";
			public string LogFolder = "";
			public List<string> Machines = new();
			public List<WorldApp> Apps = new();
			public List<string> VfsNodes = new();

			/// <summary>The machine hosting the master: the first one the scenario names.</summary>
			public string MasterMachine => Machines.Count > 0 ? Machines[0] : string.Empty;

			public string LogOf( string machine ) => Path.Combine( LogFolder, $"{machine}.log" );
		}

		public class WorldApp
		{
			public string Machine = "";
			public string App = "";
			public string IdTuple = "";
			public string Dir = "";
			public string LogsDir = "";
		}
	}
}
