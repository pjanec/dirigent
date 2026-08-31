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
				// deliberately no file shares. Not because loopback SMB does not work - \\127.0.0.1\C$
				// is reachable, unelevated, for a domain account with admin rights (UAC filters the
				// network-logon token of *local* accounts only). It is that whether it works depends
				// on the account the tests run as: a local admin account gets a filtered token unless
				// elevated, and an account outside Administrators is refused by C$'s own ACL, since a
				// purpose-made share cannot be created without admin rights either.
				// Every "machine" here is this one anyway, so each slave writes to the folder directly.
				.App( "m1.camera", a => a.LongRunning().WritesLog().WithLogNode() )
				.App( "m1.tracker", a => a.LongRunning().WritesLog().WithLogNode() )
				.App( "m2.recorder", a => a.LongRunning().WritesLog().WithLogNode() )
				.Package( "logs.all", "Logs/All apps", p => p.RefAll( "log" ) );

		/// <summary>
		/// LoggingApps with the log folders already populated: one file from yesterday and one from
		/// nine days ago per application. The nodes ask for nothing older than two days, so a correct
		/// collection takes the first and leaves the second.
		/// </summary>
		public static Scenario LoggingWorld()
		{
			var scenario = LoggingApps();

			foreach( var app in new[] { "m1.camera", "m1.tracker", "m2.recorder" } )
			{
				scenario.Seed( app, "recent.log", ageDays: 1 );
				scenario.Seed( app, "ancient.log", ageDays: 9 );
			}

			return scenario;
		}

		/// <summary>
		/// The logging world plus a plan naming a machine no agent will ever serve.
		/// </summary>
		/// <remarks>
		/// For anything that needs an operation which cannot finish on its own - a script waiting for
		/// that plan's machines waits for ever, which is how a timeout or a wait is observed without
		/// sleeping through a real one.
		/// </remarks>
		public static Scenario WaitingWorld()
			=> LoggingWorld()
				.RawXml( "<Plan Name='never'><App AppIdTuple='ghost.app' ExeFullPath='[cmd]'/></Plan>" );

		/// <summary>
		/// The presets addressable by name, for callers that get the name as text - the scenario
		/// generator, and through it the tier-2 PowerShell driver.
		/// </summary>
		public static readonly IReadOnlyDictionary<string, Func<Scenario>> Presets
				= new Dictionary<string, Func<Scenario>>( StringComparer.OrdinalIgnoreCase )
		{
			{ "OneMachine", OneMachine },
			{ "TwoMachines", TwoMachines },
			{ "TwoMachinesWithIdlers", TwoMachinesWithIdlers },
			{ "LoggingApps", LoggingApps },
			{ "LoggingWorld", LoggingWorld },
			{ "WaitingWorld", WaitingWorld },
		};

		/// <summary>The named preset, or an exception naming the ones that do exist.</summary>
		public static Scenario ByName( string name )
		{
			if( Presets.TryGetValue( name, out var preset ) ) return preset();

			throw new ArgumentException(
				$"unknown scenario '{name}'; known: {string.Join( ", ", Presets.Keys )}" );
		}

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

		/// <summary>
		/// The address the config declares for a machine, which is not how the bed reaches it: every
		/// machine here answers on loopback. Only for tests about the declared address itself.
		/// </summary>
		public Scenario DeclaredIp( string machineName, string ip )
		{
			var machine = Spec.Machines.FirstOrDefault( m => m.Name == machineName )
				?? throw new ArgumentException( $"machine '{machineName}' is not part of this scenario" );
			machine.Ip = ip;
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

		/// <summary>
		/// A plan whose entries carry their own settings - dependencies, init conditions, volatility.
		/// Those belong on the plan's copy of an application, not on the standalone definition, where
		/// Dirigent rejects a dependency it cannot resolve.
		/// </summary>
		/// <example>
		/// Plan( "ordered", p =&gt; p.App( "m1.first" ).App( "m1.second", a =&gt; a.DependsOn( "first" ) ) )
		/// </example>
		public Scenario Plan( string name, Action<PlanBuilder> configure )
		{
			var plan = new PlanSpec() { Name = name };
			Spec.Plans.Add( plan );
			configure( new PlanBuilder( plan ) );
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
		/// <param name="incompressible">
		/// Fill it with data that does not compress, so that collecting it costs what a real log of
		/// that size costs. Needed by anything measuring or interrupting a collection.
		/// </param>
		public Scenario Seed( string idTuple, string fileName, double ageDays = 0, int sizeBytes = 64,
				string? content = null, bool incompressible = false )
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
				Incompressible = incompressible,
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
	/// <summary>
	/// The settings that are plain XML attributes on an &lt;App&gt; element, shared by an application
	/// definition and by a plan's entry for it - a plan entry is a whole definition too.
	/// </summary>
	public abstract class AppAttributeBuilder<T> where T : AppAttributeBuilder<T>
	{
		protected abstract Dictionary<string, string> Attributes { get; }

		public T Attribute( string name, string value ) { Attributes[name] = value; return (T) this; }

		/// <summary>Started and then forgotten; its exit does not fail a plan.</summary>
		public T Volatile() => Attribute( "Volatile", "1" );

		/// <summary>Launched again if it terminates unexpectedly.</summary>
		public T RestartOnCrash() => Attribute( "RestartOnCrash", "1" );

		/// <summary>
		/// Applications this one waits for in a plan. Each is "app" for one on the same machine or
		/// "machine.app" for one elsewhere; waiting means running <em>and initialized</em>.
		/// </summary>
		public T DependsOn( params string[] apps )
			=> Attribute( "Dependencies", string.Join( ";", apps ) );

		/// <summary>
		/// Counts as initialized this many seconds after launch, instead of immediately. This is what
		/// makes a dependency wait for something observable.
		/// </summary>
		public T InitializedAfter( double seconds )
			=> Attribute( "InitCondition", $"timeout {Inv( seconds )}" );

		/// <summary>Counts as initialized when it exits with this code - for one-shot utilities.</summary>
		public T InitializedOnExitCode( int exitCode = 0 )
			=> Attribute( "InitCondition", $"exitcode {exitCode}" );

		/// <summary>Killing it takes its whole process tree, not just the process itself.</summary>
		public T KillTree() => Attribute( "KillTree", "1" );

		/// <summary>Asked to close its main window before being killed outright.</summary>
		public T KillSoftly() => Attribute( "KillSoftly", "1" );

		/// <summary>Adopted rather than restarted if found already running.</summary>
		public T AdoptIfAlreadyRunning() => Attribute( "AdoptIfAlreadyRunning", "1" );

		/// <summary>Minimum delay before the next application of a plan is launched.</summary>
		public T SeparationInterval( double seconds )
			=> Attribute( "SeparationInterval", Inv( seconds ) );

		/// <summary>How long a kill waits for the process to go away before reporting it dead.</summary>
		public T MinKillingTime( double seconds )
			=> Attribute( "MinKillingTime", Inv( seconds ) );

		protected static string Inv( double d ) => d.ToString( System.Globalization.CultureInfo.InvariantCulture );
	}

	/// <summary>An application of a plan, with the plan's own settings for it.</summary>
	public class PlanAppBuilder : AppAttributeBuilder<PlanAppBuilder>
	{
		readonly PlanAppSpec _spec;
		internal PlanAppBuilder( PlanAppSpec spec ) { _spec = spec; }
		protected override Dictionary<string, string> Attributes => _spec.Attributes;
	}

	public class PlanBuilder
	{
		readonly PlanSpec _spec;
		internal PlanBuilder( PlanSpec spec ) { _spec = spec; }

		/// <param name="idTuple">"machine.app" of an application already in the scenario</param>
		public PlanBuilder App( string idTuple, Action<PlanAppBuilder>? configure = null )
		{
			var parts = idTuple.Split( '.', 2 );
			if( parts.Length != 2 )
				throw new ArgumentException( $"'{idTuple}' should be 'machine.app'" );

			var app = new PlanAppSpec() { MachineName = parts[0], AppId = parts[1] };
			_spec.Apps.Add( app );
			configure?.Invoke( new PlanAppBuilder( app ) );
			return this;
		}

		public PlanBuilder Attribute( string name, string value )
		{
			_spec.Attributes[name] = value;
			return this;
		}
	}

	public class AppBuilder : AppAttributeBuilder<AppBuilder>
	{
		readonly AppSpec _spec;

		internal AppBuilder( AppSpec spec ) { _spec = spec; }

		protected override Dictionary<string, string> Attributes => _spec.Attributes;

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

		/// <summary>
		/// Starts child processes, for testing kill tree, and writes their pids to children.txt in its
		/// working folder so a test can check on them without asking Windows who they are.
		/// </summary>
		public AppBuilder SpawnsChildren( int count = 2 )
			=> AddArgs( $"--spawn-children {count} --children-file \"{{appdir}}\\children.txt\"" );

		/// <summary>
		/// Exposes the app's recent log files as a VFS node. Defaults match the intent of
		/// "the recent logs": the newest handful, nothing older than two days.
		/// </summary>
		public AppBuilder WithLogNode(
			string id = "log",
			string title = "Recent logs",
			string mask = "*.log",
			int maxFiles = 10,
			double maxSeconds = 2 * 24 * 3600,
			long? tailBytes = null,
			bool? clearable = null )
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
				TailBytes = tailBytes,
				Clearable = clearable,
			} );
			return this;
		}

		/// <summary>
		/// Exposes one named file of the application's folder as a VFS node - a configuration file,
		/// typically, which is what a package holds besides the logs.
		/// </summary>
		public AppBuilder WithFileNode( string id, string fileName, string? title = null,
				bool? clearable = null )
		{
			_spec.VfsNodes.Add( new VfsSpec()
			{
				Kind = VfsKind.NewestFiles,
				Id = id,
				Title = title ?? fileName,
				Path = "{applogs}",
				Mask = fileName,
				MaxFiles = 1,
				Clearable = clearable,
			} );
			return this;
		}

		/// <summary>Exposes a whole folder as a VFS node.</summary>
		public AppBuilder WithFolderNode( string id, string path, string? mask = null,
				double? maxSeconds = null, int? maxFiles = null, long? maxTotalBytes = null,
				long? tailBytes = null, bool? clearable = null )
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
				TailBytes = tailBytes,
				Clearable = clearable,
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
		public AppBuilder Env( string name, string value ) { _spec.EnvVars[name] = value; return this; }
		public AppBuilder RawXml( string xml ) { _spec.ExtraXml.Add( xml ); return this; }

		/// <summary>
		/// A soft-kill sequence with an explicit patience: the close request is given this long
		/// before the process is killed anyway. Everything else an &lt;App&gt; can carry as an attribute
		/// is on the base class, since a plan entry takes the same settings.
		/// </summary>
		public AppBuilder ClosePolitelyFirst( double timeoutSeconds = 1.0 )
			=> RawXml( $"<SoftKill><Close timeout=\"{Inv( timeoutSeconds )}\"/></SoftKill>" );

		AppBuilder AddArgs( string args )
		{
			_spec.CmdLineArgs = string.IsNullOrEmpty( _spec.CmdLineArgs )
									? args
									: _spec.CmdLineArgs + " " + args;
			return this;
		}

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
