using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;

namespace Dirigent.TestBed.Gen
{
	/// <summary>
	/// Writes a scenario out as a real world on disk: a SharedConfig.xml, the application folders
	/// with their seeded files, and a manifest describing what was made.
	/// </summary>
	/// <remarks>
	/// This exists so that tier 2 and tier 3 consume the same scenario model as tier 1 instead of
	/// keeping a second copy of the same worlds in PowerShell.
	/// </remarks>
	static class Program
	{
		const string Usage = @"
Dirigent.TestBed.Gen - render a test scenario to a folder

  --scenario <name>   which world to write (see --list)
  --out <dir>         where to write it; becomes the root of the world
  --testApp <path>    Dirigent.TestApp.exe to point the applications at
                      (found automatically when not given)
  --force             write into --out even if it is not empty
  --list              print the known scenarios and exit
";

		static int Main( string[] args )
		{
			try
			{
				var opts = Parse( args );

				if( opts.List )
				{
					foreach( var name in Scenario.Presets.Keys.OrderBy( x => x ) )
						Console.WriteLine( name );
					return 0;
				}

				if( string.IsNullOrEmpty( opts.Scenario ) || string.IsNullOrEmpty( opts.Out ) )
				{
					Console.Error.WriteLine( "--scenario and --out are both required." );
					Console.Error.WriteLine( Usage );
					return 2;
				}

				return Generate( opts );
			}
			catch( Exception e )
			{
				Console.Error.WriteLine( $"ERROR: {e.Message}" );
				return 1;
			}
		}

		static int Generate( Options opts )
		{
			var scenario = Scenario.ByName( opts.Scenario! );
			var root = Path.GetFullPath( opts.Out! );

			if( Directory.Exists( root ) && Directory.EnumerateFileSystemEntries( root ).Any() && !opts.Force )
			{
				Console.Error.WriteLine( $"ERROR: {root} is not empty; use --force to write into it anyway." );
				return 2;
			}

			Directory.CreateDirectory( root );

			var testApp = string.IsNullOrEmpty( opts.TestApp )
					? TestAppLocator.Find()
					: Path.GetFullPath( opts.TestApp! );

			if( !File.Exists( testApp ) )
				throw new FileNotFoundException( $"test application not found: {testApp}" );

			// the world's paths are absolute and rooted here, exactly as a tier-1 bed does it
			var ctx = new RenderContext( root, testApp );

			WorldSeeder.Seed( scenario.Spec, ctx );

			var sharedConfig = Path.Combine( root, "SharedConfig.xml" );
			File.WriteAllText( sharedConfig, SharedConfigRenderer.Render( scenario.Spec, ctx ) );

			// An agent insists on a local config: the setting defaults to "LocalConfig.xml" and a
			// missing file takes the process down at startup. A real deployment has one, so a generated
			// world has one too - empty, since tools and folder watchers belong to no test yet.
			File.WriteAllText( Path.Combine( root, "LocalConfig.xml" ), LocalConfigXml );

			// folders the agents are told to use, so a run touches nothing of the real installation
			foreach( var folder in new[] { "agentstatus", "downloads", "logs" } )
				Directory.CreateDirectory( Path.Combine( root, folder ) );

			var manifest = Describe( opts.Scenario!, scenario, ctx, root, testApp, sharedConfig );
			File.WriteAllText( Path.Combine( root, "world.json" ), Tools.Serialize( manifest ) );

			Console.WriteLine( root );
			return 0;
		}

		static Manifest Describe( string name, Scenario scenario, RenderContext ctx,
				string root, string testApp, string sharedConfig )
			=> new Manifest()
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
				Apps = scenario.Spec.Apps.Select( a => new ManifestApp()
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

		const string LocalConfigXml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<!-- written by Dirigent.TestBed.Gen; every agent needs one of these next to its working folder -->
<Local>
</Local>
";

		// ---- the manifest ---------------------------------------------------------------

		public class Manifest
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
			public List<ManifestApp> Apps = new();
			public List<string> VfsNodes = new();
		}

		public class ManifestApp
		{
			public string Machine = "";
			public string App = "";
			public string IdTuple = "";
			public string Dir = "";
			public string LogsDir = "";
		}

		// ---- arguments ------------------------------------------------------------------

		class Options
		{
			public string? Scenario;
			public string? Out;
			public string? TestApp;
			public bool Force;
			public bool List;
		}

		static Options Parse( string[] args )
		{
			var opts = new Options();

			for( int i = 0; i < args.Length; i++ )
			{
				string Next( string what )
				{
					if( i + 1 >= args.Length ) throw new ArgumentException( $"{what} needs a value" );
					return args[++i];
				}

				switch( args[i].ToLowerInvariant() )
				{
					case "--scenario": opts.Scenario = Next( "--scenario" ); break;
					case "--out":      opts.Out = Next( "--out" ); break;
					case "--testapp":  opts.TestApp = Next( "--testApp" ); break;
					case "--force":    opts.Force = true; break;
					case "--list":     opts.List = true; break;
					case "--help":
					case "-h":
						Console.WriteLine( Usage );
						Environment.Exit( 0 );
						break;
					default:
						throw new ArgumentException( $"unknown argument '{args[i]}'{Usage}" );
				}
			}

			return opts;
		}
	}
}
