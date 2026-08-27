using System;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent.TestBed.Scenarios
{
	/// <summary>
	/// The fluent way to build a <see cref="ScenarioSpec"/>. Presets cover the common worlds,
	/// mixins cover the common application behaviours, and everything stays mutable so a test can
	/// change the one thing it is actually about instead of restating a whole config.
	/// </summary>
	public class Scenario
	{
		public ScenarioSpec Spec { get; } = new();

		// ---- presets ----------------------------------------------------------------

		public static Scenario Empty() => new Scenario();

		public static Scenario OneMachine() => new Scenario().Machines( "m1" );

		public static Scenario TwoMachines() => new Scenario().Machines( "m1", "m2" );

		/// <summary>
		/// Two machines with an idle application on each - the smallest world in which app
		/// control and per-machine routing can be observed.
		/// </summary>
		public static Scenario TwoMachinesWithIdlers()
			=> TwoMachines()
				.App( "m1.idler", a => a.LongRunning() )
				.App( "m2.idler", a => a.LongRunning() );

		/// <summary>
		/// Applications that write log files, each exposing them as a "log" VFS node, plus a
		/// package collecting every such node. The world the log-collection tests need.
		/// </summary>
		public static Scenario LoggingApps()
			=> TwoMachines()
				// deliberately no file shares: a tier-1 bed has no real Windows share, and declaring
				// one would send the slaves through \\127.0.0.1\C$, which needs an elevated token.
				// Every "machine" here is this one, so each slave writes to the folder directly.
				.App( "m1.camera", a => a.LongRunning().WritesLog().WithLogNode() )
				.App( "m1.tracker", a => a.LongRunning().WritesLog().WithLogNode() )
				.App( "m2.recorder", a => a.LongRunning().WritesLog().WithLogNode() )
				.Package( "logs.all", "Logs/All apps", p => p.RefAll( "log" ) );

		// ---- machines ---------------------------------------------------------------

		public Scenario Machines( params string[] names )
		{
			foreach( var name in names )
			{
				if( Spec.Machines.Any( m => m.Name == name ) ) continue;
				Spec.Machines.Add( new MachineSpec() { Name = name } );
			}
			return this;
		}

		public Scenario Share( string machineName, string shareName, string path )
		{
			var machine = Spec.Machines.FirstOrDefault( m => m.Name == machineName )
				?? throw new ArgumentException( $"machine '{machineName}' is not part of this scenario" );
			machine.Shares[shareName] = path;
			return this;
		}

		// ---- applications -----------------------------------------------------------

		/// <param name="idTuple">"machine.app"</param>
		public Scenario App( string idTuple, Action<AppBuilder>? configure = null )
		{
			var (machineName, appId) = SplitTuple( idTuple );
			Machines( machineName );

			var spec = new AppSpec() { MachineName = machineName, AppId = appId };
			Spec.Apps.Add( spec );
			configure?.Invoke( new AppBuilder( spec ) );
			return this;
		}

		/// <summary>Changes an application already in the scenario - the per-test delta.</summary>
		public Scenario App( string idTuple, Action<AppBuilder> configure, bool mustExist )
		{
			if( !mustExist ) return App( idTuple, configure );

			var (machineName, appId) = SplitTuple( idTuple );
			var spec = Spec.Apps.FirstOrDefault( a => a.MachineName == machineName && a.AppId == appId )
				?? throw new ArgumentException( $"app '{idTuple}' is not part of this scenario" );
			configure( new AppBuilder( spec ) );
			return this;
		}

		// ---- plans ------------------------------------------------------------------

		public Scenario Plan( string name, params string[] appIdTuples )
		{
			var plan = new PlanSpec() { Name = name };
			foreach( var tuple in appIdTuples )
			{
				var (machineName, appId) = SplitTuple( tuple );
				plan.Apps.Add( new PlanAppSpec() { MachineName = machineName, AppId = appId } );
			}
			Spec.Plans.Add( plan );
			return this;
		}

		/// <summary>A plan containing every application of the scenario.</summary>
		public Scenario PlanWithEverything( string name = "all" )
			=> Plan( name, Spec.Apps.Select( a => $"{a.MachineName}.{a.AppId}" ).ToArray() );

		// ---- packages ---------------------------------------------------------------

		public Scenario Package( string id, string? title, Action<PackageBuilder>? configure = null )
		{
			var spec = new PackageSpec() { Id = id, Title = title };
			Spec.Packages.Add( spec );
			configure?.Invoke( new PackageBuilder( spec ) );
			return this;
		}

		// ---- files that must already exist ------------------------------------------

		/// <param name="idTuple">"machine.app", or just "machine" for a machine-level file</param>
		public Scenario Seed( string idTuple, string fileName, double ageDays = 0, int sizeBytes = 64, string? content = null )
		{
			var (machineName, appId) = idTuple.Contains( '.' ) ? SplitTuple( idTuple ) : (idTuple, "");
			Spec.Seeds.Add( new SeedSpec()
			{
				MachineName = machineName,
				AppId = appId,
				FileName = fileName,
				AgeDays = ageDays,
				SizeBytes = sizeBytes,
				Content = content,
			} );
			return this;
		}

		// ---- escape hatch -----------------------------------------------------------

		public Scenario RawXml( string xml )
		{
			Spec.ExtraSharedXml.Add( xml );
			return this;
		}

		public IReadOnlyList<string> MachineNames => Spec.Machines.Select( m => m.Name ).ToList();

		static (string machineName, string appId) SplitTuple( string idTuple )
		{
			int dot = idTuple.IndexOf( '.' );
			if( dot <= 0 || dot == idTuple.Length - 1 )
				throw new ArgumentException( $"'{idTuple}' should be \"machine.app\"" );
			return ( idTuple.Substring( 0, dot ), idTuple.Substring( dot + 1 ) );
		}
	}

	/// <summary>
	/// Application behaviours, each one being a test application switch plus whatever config goes
	/// with it, so that no test has to spell the combination out twice.
	/// </summary>
	public class AppBuilder
	{
		readonly AppSpec _spec;

		internal AppBuilder( AppSpec spec ) { _spec = spec; }

		/// <summary>Idles until killed.</summary>
		public AppBuilder LongRunning() => AddArgs( "--run-forever" );

		/// <summary>Terminates on its own after the given time, with the given exit code.</summary>
		public AppBuilder ExitsAfter( double seconds, int exitCode = 0 )
			=> AddArgs( $"--exit-after {Inv( seconds )} --exit-code {exitCode}" );

		/// <summary>Appends to a log file in its log folder, which the log VFS node points at.</summary>
		public AppBuilder WritesLog( string fileName = "app.log", double everySeconds = 0.5 )
			=> AddArgs( $"--write-log \"{{applogs}}\\{fileName}\" --every {Inv( everySeconds )}" );

		/// <summary>Writes a readiness marker late, for init detectors and plan sequencing.</summary>
		public AppBuilder ReadyAfter( double seconds, string fileName = "ready.txt" )
			=> AddArgs( $"--ready-after {Inv( seconds )} --ready-file \"{{appdir}}\\{fileName}\"" );

		/// <summary>Dumps its environment, for testing variable propagation.</summary>
		public AppBuilder PrintsEnvironment( string fileName = "env.txt" )
			=> AddArgs( $"--print-env \"{{appdir}}\\{fileName}\"" );

		/// <summary>Refuses close requests, for testing soft kill escalation.</summary>
		public AppBuilder IgnoresClose() => AddArgs( "--ignore-close" );

		/// <summary>Starts child processes, for testing kill tree.</summary>
		public AppBuilder SpawnsChildren( int count = 2 ) => AddArgs( $"--spawn-children {count}" );

		/// <summary>
		/// Exposes the app's recent log files as a VFS node. Defaults match the intent of
		/// "the recent logs": the newest handful, nothing older than two days.
		/// </summary>
		public AppBuilder WithLogNode(
			string id = "log",
			string title = "Recent logs",
			string mask = "*.log",
			int maxFiles = 10,
			double maxSeconds = 2 * 24 * 3600 )
		{
			_spec.VfsNodes.Add( new VfsSpec()
			{
				Kind = VfsKind.NewestFiles,
				Id = id,
				Title = title,
				Path = "{applogs}",
				Mask = mask,
				MaxFiles = maxFiles,
				MaxSeconds = maxSeconds,
			} );
			return this;
		}

		/// <summary>Exposes a whole folder as a VFS node.</summary>
		public AppBuilder WithFolderNode( string id, string path, string? mask = null,
				double? maxSeconds = null, int? maxFiles = null, long? maxTotalBytes = null )
		{
			_spec.VfsNodes.Add( new VfsSpec()
			{
				Kind = VfsKind.Folder,
				Id = id,
				Path = path,
				Mask = mask,
				MaxSeconds = maxSeconds,
				MaxFiles = maxFiles,
				MaxTotalBytes = maxTotalBytes,
			} );
			return this;
		}

		/// <summary>
		/// Only for tests about the window style itself - everything else stays minimized so a
		/// run does not interrupt whoever is at the machine.
		/// </summary>
		public AppBuilder WindowStyle( WindowStyleSpec style ) { _spec.WindowStyle = style; return this; }

		public AppBuilder Exe( string path ) { _spec.ExeFullPath = path; return this; }
		public AppBuilder Args( string args ) { _spec.CmdLineArgs = args; return this; }
		public AppBuilder StartupDir( string dir ) { _spec.StartupDir = dir; return this; }
		public AppBuilder Attribute( string name, string value ) { _spec.Attributes[name] = value; return this; }
		public AppBuilder Env( string name, string value ) { _spec.EnvVars[name] = value; return this; }
		public AppBuilder RawXml( string xml ) { _spec.ExtraXml.Add( xml ); return this; }

		public AppBuilder Volatile() => Attribute( "Volatile", "1" );
		public AppBuilder RestartOnCrash() => Attribute( "RestartOnCrash", "1" );

		AppBuilder AddArgs( string args )
		{
			_spec.CmdLineArgs = string.IsNullOrEmpty( _spec.CmdLineArgs )
									? args
									: _spec.CmdLineArgs + " " + args;
			return this;
		}

		static string Inv( double d ) => d.ToString( System.Globalization.CultureInfo.InvariantCulture );
	}

	public class PackageBuilder
	{
		readonly PackageSpec _spec;

		internal PackageBuilder( PackageSpec spec ) { _spec = spec; }

		/// <summary>Every node with this id, on any machine, in any application or none.</summary>
		public PackageBuilder RefAll( string id )
		{
			_spec.Children.Add( new VfsSpec()
			{
				Kind = VfsKind.Ref,
				Id = id,
				RefMachineId = "*",
				RefAppId = "*",
			} );
			return this;
		}

		/// <summary>Nodes with this id belonging to one machine.</summary>
		public PackageBuilder RefMachine( string id, string machineName )
		{
			_spec.Children.Add( new VfsSpec()
			{
				Kind = VfsKind.Ref,
				Id = id,
				RefMachineId = machineName,
				RefAppId = "*",
			} );
			return this;
		}
	}
}
