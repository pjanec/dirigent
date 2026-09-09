using Dirigent;
using Dirigent.TestBed.Scenarios;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Dirigent.SystemTests
{
	/// <summary>
	/// A world of real Dirigent processes on this machine, driven over the command-line interface -
	/// the road an operator or a CI job takes.
	/// </summary>
	/// <remarks>
	/// What only this tier can show: that the shipped executables start at all, that an agent finds
	/// its configuration, that a killed agent adopts its applications when it comes back, that the
	/// remote-control surfaces answer, and that a run leaves nothing behind. Everything else belongs
	/// one tier down, in `Dirigent.IntegrationTests`, which runs the same worlds in one process and
	/// in a fraction of the time.
	///
	/// The worlds are described once, in C#, by the scenario model in `Dirigent.TestBed`;
	/// <see cref="WorldOnDisk"/> renders one to a folder and everything here works from that.
	/// </remarks>
	public sealed class SystemWorld : IDisposable
	{
		/// <summary>How long to wait for a world to come up before giving up on it.</summary>
		public static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds( 40 );

		/// <summary>
		/// How long to wait for an application to reach a state. Generous on purpose: a real process
		/// is starting on a machine that may be busy with a build, and a ceiling costs nothing when
		/// things are quick.
		/// </summary>
		public static readonly TimeSpan AppTimeout = TimeSpan.FromSeconds( 90 );

		public WorldOnDisk.World Files { get; }
		public string Scenario { get; }
		public string Root => Files.Root;

		public int MasterPort { get; }
		public int CliPort { get; }

		/// <summary>The web server's port, or -1 when it was not asked for (0 would mean 8877).</summary>
		public int HttpPort { get; }

		readonly List<Agent> _agents = new();
		bool _disposed;

		/// <summary>One of the processes this world started.</summary>
		public sealed class Agent
		{
			public string Machine = "";
			public Process Process = null!;
			public bool IsMaster;
		}

		public IReadOnlyList<Agent> Agents => _agents;

		/// <summary>
		/// Takes over a process a test started itself, so that the teardown kills it and looks for
		/// its applications. A test that restarts an agent has to hand it over, or the world would
		/// forget about it and leave it running.
		/// </summary>
		public void Adopt( string machine, Process process )
			=> _agents.Add( new Agent() { Machine = machine, Process = process, IsMaster = false } );

		SystemWorld( WorldOnDisk.World files, string scenario, int masterPort, int cliPort, int httpPort )
		{
			Files = files;
			Scenario = scenario;
			MasterPort = masterPort;
			CliPort = cliPort;
			HttpPort = httpPort;
		}

		// ---- bringing one up ------------------------------------------------------------

		/// <summary>
		/// Renders the scenario, starts a master hosting the first machine's agent and one agent per
		/// further machine, and returns once every one of them has connected.
		/// </summary>
		/// <param name="withHttp">also open the web server, for tests of the REST surface</param>
		/// <param name="visible">
		/// leave the console windows visible instead of minimized. Off by default: a test run must
		/// not throw windows at whoever is using the machine.
		/// </param>
		public static SystemWorld Start( string scenario = "LoggingWorld", bool withHttp = false,
				bool visible = false )
		{
			var root = Path.Combine( Path.GetTempPath(), "DirigentTier3",
					Guid.NewGuid().ToString( "N" ).Substring( 0, 6 ) );

			var files = WorldOnDisk.Write( scenario, root );

			var world = new SystemWorld( files, scenario,
					masterPort: FreePort(), cliPort: FreePort(),
					httpPort: withHttp ? FreePort() : -1 );

			try
			{
				world.StartAgents( visible );
				world.WaitUntilEverybodyIsConnected();
			}
			catch( Exception e )
			{
				// a world that never came up is worse than useless; take it down and say why, with
				// the agent logs, which are the only place the reason is written
				var log = world.TailOfEveryLog();
				world.Dispose( keepRoot: true );

				throw new Exception(
						$"the world did not come up: {e.Message}\n{log}\nIts folder was kept: {root}", e );
			}

			return world;
		}

		void StartAgents( bool visible )
		{
			foreach( var machine in Files.Machines )
			{
				var isMaster = machine == Files.MasterMachine;

				var args = new List<string>()
				{
					"--machineId", machine,
					"--mode", "daemon",
					"--masterIp", "127.0.0.1",
					"--masterPort", MasterPort.ToString(),
					"--sharedConfigFile", Files.SharedConfig,
					"--localConfigFile", Files.LocalConfig,
					"--agentStatusFolder", Files.AgentStatusFolder,
					"--downloadFolder", Files.DownloadFolder,
					"--logFile", Files.LogOf( machine ),
					"--rootForRelativePaths", Files.Root,
					"--isMaster", isMaster ? "1" : "0",
				};

				if( isMaster )
				{
					args.AddRange( new[] { "--CLIPort", CliPort.ToString(), "--httpPort", HttpPort.ToString() } );
				}

				_agents.Add( new Agent()
				{
					Machine = machine,
					IsMaster = isMaster,
					Process = StartProcess( Tool( "Dirigent.Agent.Console", "Dirigent.Agent.exe" ),
										args, Files.Root, visible ),
				} );
			}
		}

		void WaitUntilEverybodyIsConnected()
			=> WaitUntil( "the master answers and every agent is connected", StartTimeout, () =>
			{
				var lines = CliList( "GetAllClientsState", TimeSpan.FromSeconds( 3 ) );

				return Files.Machines.All(
						m => lines.Any( l => l.StartsWith( $"CLIENT:{m}:1:", StringComparison.Ordinal ) ) );
			} );

		// ---- taking one down ------------------------------------------------------------

		public void Dispose() => Dispose( keepRoot: false );

		/// <summary>
		/// Kills everything the world started - the agents, and any application they left running -
		/// and removes its folder.
		/// </summary>
		/// <remarks>
		/// Dirigent deliberately leaves managed applications running when an agent goes away, which
		/// is right in production and wrong here. They are found before the agents are killed, while
		/// they can still be recognised as their children.
		/// </remarks>
		public void Dispose( bool keepRoot )
		{
			if( _disposed ) return;
			_disposed = true;

			// ask nicely first, so the agents kill their applications the way they normally would
			try { Cli( "KillAll", TimeSpan.FromSeconds( 5 ) ); }
			catch( Exception e ) { Trace.WriteLine( $"KillAll did not get through: {e.Message}" ); }

			var survivors = ApplicationProcesses();

			foreach( var agent in _agents )
			{
				try
				{
					if( !agent.Process.HasExited ) agent.Process.Kill();
					agent.Process.WaitForExit( 5000 );
				}
				catch( Exception e ) { Trace.WriteLine( $"killing {agent.Machine} failed: {e.Message}" ); }
			}

			foreach( var pid in survivors )
			{
				try { Process.GetProcessById( pid ).Kill(); }
				catch { } // already gone, which is the outcome that was wanted anyway
			}

			if( keepRoot ) return;

			// the processes need a moment to let go of their files
			for( int attempt = 0; attempt < 15; attempt++ )
			{
				try
				{
					Directory.Delete( Files.Root, true );
					return;
				}
				catch { Thread.Sleep( 200 ); }
			}

			if( Directory.Exists( Files.Root ) )
				Trace.WriteLine( $"could not remove {Files.Root}" );
		}

		/// <summary>
		/// The ids of the applications this world is running.
		/// </summary>
		/// <remarks>
		/// By parent process id, because the test application's executable is shared by every world
		/// and its command line does not always mention one. Only children of this world's agents
		/// count, so a run cannot kill anything belonging to another run - or to a real installation.
		/// </remarks>
		public List<int> ApplicationProcesses()
		{
			var found = new HashSet<int>();

			foreach( var agent in _agents )
			{
				int pid;
				try { pid = agent.Process.Id; }
				catch { continue; }

				try
				{
					// the product's own helper, which is what its KillTree uses
					foreach( var child in WinApi.GetChildProcesses( pid ) ) found.Add( child );
				}
				catch( Exception e )
				{
					Trace.WriteLine( $"could not look for children of {pid}: {e.Message}" );
				}
			}

			return found.OrderBy( x => x ).ToList();
		}

		/// <summary>
		/// Processes of the given executable whose command line mentions the given text.
		/// </summary>
		/// <remarks>
		/// For looking after a world that is already gone: once its agents have been killed there is
		/// no parentage left to follow, and the only thing still tying an application to this run is
		/// the world's own folder in its command line. The executable is shared by every world, so
		/// matching on the name alone would blame another run - or a real installation - for a leak.
		/// </remarks>
		public static List<int> ProcessesMentioning( string exeName, string text )
		{
			var found = new List<int>();

			using var searcher = new System.Management.ManagementObjectSearcher(
					$"SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = '{exeName}'" );

			foreach( var row in searcher.Get() )
			{
				var cmdLine = row["CommandLine"] as string;
				if( cmdLine is not null && cmdLine.Contains( text, StringComparison.OrdinalIgnoreCase ) )
					found.Add( Convert.ToInt32( row["ProcessId"] ) );
			}

			return found;
		}

		// ---- talking to it --------------------------------------------------------------

		/// <summary>
		/// Sends one text command to the master over the command port and returns its single-line
		/// answer. Throws when the master answers ERROR.
		/// </summary>
		public string Cli( string command, TimeSpan? timeout = null )
			=> Converse( command, list: false, timeout ?? TimeSpan.FromSeconds( 10 ) ).Single();

		/// <summary>
		/// As <see cref="Cli"/>, for a command that answers with a list terminated by END. The
		/// terminator is not returned.
		/// </summary>
		public List<string> CliList( string command, TimeSpan? timeout = null )
			=> Converse( command, list: true, timeout ?? TimeSpan.FromSeconds( 10 ) );

		List<string> Converse( string command, bool list, TimeSpan timeout )
		{
			using var client = new TcpClient();

			if( !client.ConnectAsync( IPAddress.Loopback, CliPort ).Wait( timeout ) )
				throw new Exception( $"no connection to the command port {CliPort} within {timeout.TotalSeconds:0} s" );

			using var stream = client.GetStream();
			stream.ReadTimeout = (int) timeout.TotalMilliseconds;

			const string reqId = "sys";

			// no BOM, and "\n" rather than the platform's newline: the master reads lines
			using var writer = new StreamWriter( stream, new UTF8Encoding( false ) ) { NewLine = "\n", AutoFlush = true };
			writer.WriteLine( $"[{reqId}] {command}" );

			using var reader = new StreamReader( stream, Encoding.UTF8 );

			var answers = new List<string>();

			while( true )
			{
				var raw = reader.ReadLine();
				if( raw is null )
					throw new Exception( $"the master closed the connection while answering '{command}'" );

				var line = raw.Trim();
				if( line.StartsWith( $"[{reqId}] ", StringComparison.Ordinal ) )
					line = line.Substring( reqId.Length + 3 ).Trim();

				if( line.StartsWith( "ERROR", StringComparison.Ordinal ) )
					throw new Exception( $"'{command}' was refused: {line}" );

				if( !list ) return new List<string>() { line };
				if( line == "END" ) return answers;

				answers.Add( line );
			}
		}

		/// <summary>What the shipped Dirigent.CLI.exe printed, and the code it exited with.</summary>
		/// <remarks>
		/// <see cref="Cli"/> talks to the command port directly, which is what most tests want. This
		/// goes through the executable, so that its own read loop and exit code are what is being
		/// tested.
		/// </remarks>
		public ToolRun CliExe( string command )
			=> RunTool( Tool( "Dirigent.CLI", "Dirigent.CLI.exe" ),
					new[] { "--masterIp", "127.0.0.1", "--CLIPort", CliPort.ToString(), command } );

		// ---- scripts --------------------------------------------------------------------

		/// <summary>
		/// Runs a Dirigent script and returns its result as raw JSON - StartScript with JSON
		/// arguments, then GetScriptState until it is over. The whole non-GUI interface to the file
		/// subsystem, and to anything else scripted.
		/// </summary>
		public string RunScript( string script, string? arguments = null, TimeSpan? timeout = null )
		{
			var guid = Guid.NewGuid().ToString();

			var request = $"StartScript {guid} {script}";
			if( !string.IsNullOrEmpty( arguments ) ) request += $" '{arguments}'";

			var ack = Cli( request );
			if( ack != "ACK" ) throw new Exception( $"starting {script} was not acknowledged: '{ack}'" );

			WaitUntil( $"{script} finishes", timeout ?? TimeSpan.FromSeconds( 60 ), () =>
			{
				var state = ScriptState( guid );
				return state is not null && state.Status is EScriptStatus.Finished
						or EScriptStatus.Failed or EScriptStatus.Cancelled;
			} );

			var final = ScriptState( guid ) ?? throw new Exception( $"{script} left no state behind" );

			if( final.Status != EScriptStatus.Finished )
				throw new Exception( $"{script} ended as {final.Status}: {final.Text} {final.Data}" );

			return final.Data ?? string.Empty;
		}

		/// <summary>As <see cref="RunScript"/>, with the result deserialized.</summary>
		public T? RunScript<T>( string script, string? arguments = null, TimeSpan? timeout = null )
		{
			var data = RunScript( script, arguments, timeout );
			return string.IsNullOrEmpty( data ) ? default : Tools.Deserialize<T>( data );
		}

		/// <summary>The state of one script instance, or null while the master does not know it.</summary>
		public ScriptState? ScriptState( string guid )
		{
			var line = Cli( $"GetScriptState {guid}" );
			if( string.IsNullOrEmpty( line ) ) return null;

			var match = Regex.Match( line, @"^SCRIPT:([0-9a-fA-F\-]{36}):(.*)$" );
			if( !match.Success ) throw new Exception( $"unexpected answer to GetScriptState: '{line}'" );

			return Tools.Deserialize<ScriptState>( match.Groups[2].Value );
		}

		// ---- waiting --------------------------------------------------------------------

		/// <summary>
		/// Waits for a condition to become true, and says what it was waiting for when it does not.
		/// </summary>
		/// <remarks>
		/// There is no virtual time in Dirigent, so a fixed sleep is a guess that fails on a loaded
		/// machine. Everything that waits, waits on a condition. A world still starting up refuses
		/// connections, so an exception from the condition is a "not yet" until the deadline.
		/// </remarks>
		public void WaitUntil( string because, TimeSpan timeout, Func<bool> condition, int pollMs = 250 )
		{
			var deadline = DateTime.UtcNow + timeout;
			string? lastError = null;

			while( DateTime.UtcNow < deadline )
			{
				try
				{
					if( condition() ) return;
					lastError = null;
				}
				catch( Exception e )
				{
					lastError = e.Message;
				}

				Thread.Sleep( pollMs );
			}

			var detail = lastError is null ? "" : $"\nlast error: {lastError}";
			throw new Exception( $"timed out after {timeout.TotalSeconds:0} s waiting until {because}{detail}" );
		}

		/// <summary>
		/// Waits until an application's state flags all appear, e.g. "R" for running.
		/// </summary>
		public void WaitAppState( string app, string flags, TimeSpan? timeout = null )
			=> WaitUntil( $"{app} reports '{flags}'", timeout ?? AppTimeout, () =>
			{
				var line = Cli( $"GetAppState {app}" );
				if( string.IsNullOrEmpty( line ) ) return false;

				// APP:<idTuple>:<flags>:<exitCode>:<age>:...
				var parts = line.Split( ':' );
				if( parts.Length < 3 ) return false;

				return flags.All( f => parts[2].Contains( f ) );
			} );

		/// <summary>The state flags of an application, field 2 of its APP: line.</summary>
		public string AppFlags( string app )
		{
			var parts = Cli( $"GetAppState {app}" ).Split( ':' );
			return parts.Length < 3 ? string.Empty : parts[2];
		}

		// ---- diagnosis ------------------------------------------------------------------

		/// <summary>The tail of every agent log, for a failure that needs explaining.</summary>
		public string TailOfEveryLog( int lines = 25 )
		{
			var text = new StringBuilder();

			foreach( var log in Directory.EnumerateFiles( Files.LogFolder, "*.log" ) )
			{
				text.AppendLine();
				text.AppendLine( $"--- {Path.GetFileName( log )} (last {lines} lines) ---" );

				try
				{
					// read a copy: the agent still has it open for writing
					using var file = new FileStream( log, FileMode.Open, FileAccess.Read,
													FileShare.ReadWrite | FileShare.Delete );
					using var reader = new StreamReader( file );

					var all = reader.ReadToEnd().Split( '\n' );
					foreach( var line in all.Skip( Math.Max( 0, all.Length - lines ) ) )
						text.AppendLine( line.TrimEnd() );
				}
				catch( Exception e ) { text.AppendLine( $"(could not be read: {e.Message})" ); }
			}

			return text.Length == 0 ? $"(no agent logs under {Files.LogFolder})" : text.ToString();
		}

		// ---- the machinery underneath ---------------------------------------------------

		/// <summary>A TCP port nobody is listening on right now.</summary>
		public static int FreePort()
		{
			var listener = new TcpListener( IPAddress.Loopback, 0 );
			listener.Start();
			try { return ( (IPEndPoint) listener.LocalEndpoint ).Port; }
			finally { listener.Stop(); }
		}

		/// <summary>The repository root, found by walking up from the test binaries.</summary>
		public static string RepoRoot()
		{
			var dir = new DirectoryInfo( AppContext.BaseDirectory );

			while( dir is not null && !File.Exists( Path.Combine( dir.FullName, "src", "Dirigent.NetCore.sln" ) ) )
				dir = dir.Parent;

			return dir?.FullName
				?? throw new Exception( $"could not find the repository root above {AppContext.BaseDirectory}" );
		}

		/// <summary>The most recently built copy of an executable belonging to a project.</summary>
		public static string Tool( string project, string exe )
		{
			var projectDir = Path.Combine( RepoRoot(), "src", project );
			if( !Directory.Exists( projectDir ) ) throw new Exception( $"no such project folder: {projectDir}" );

			var found = new DirectoryInfo( projectDir )
					.EnumerateFiles( exe, SearchOption.AllDirectories )
					.OrderByDescending( f => f.LastWriteTimeUtc )
					.FirstOrDefault();

			return found?.FullName
				?? throw new Exception( $"{exe} not found under {projectDir}."
						+ " Build the solution first: dotnet build src\\Dirigent.NetCore.sln" );
		}

		static Process StartProcess( string exe, IEnumerable<string> args, string workingDir, bool visible )
		{
			var psi = new ProcessStartInfo( exe )
			{
				WorkingDirectory = workingDir,
				UseShellExecute = true,   // so the window style applies
				WindowStyle = visible ? ProcessWindowStyle.Normal : ProcessWindowStyle.Minimized,
			};

			foreach( var arg in args ) psi.ArgumentList.Add( arg );

			return Process.Start( psi )
				?? throw new Exception( $"could not start {exe}" );
		}

		/// <summary>What one run of a tool printed, on each stream, and how it ended.</summary>
		public sealed class ToolRun
		{
			public int ExitCode;
			public string StdOut = "";
			public string StdErr = "";

			/// <summary>Both streams, whitespace squeezed, for a failure message.</summary>
			public string All => Squeeze( $"{StdOut} {StdErr}" );

			public IReadOnlyList<string> OutLines => StdOut
					.Split( '\n' ).Select( l => l.Trim() ).Where( l => l.Length > 0 ).ToList();

			public IReadOnlyList<string> AllLines => $"{StdOut}\n{StdErr}"
					.Split( '\n' ).Select( l => l.Trim() ).Where( l => l.Length > 0 ).ToList();

			static string Squeeze( string text ) => Regex.Replace( text, @"\s+", " " ).Trim();
		}

		/// <summary>
		/// Runs a tool to completion with its streams kept apart.
		/// </summary>
		/// <remarks>
		/// Apart, and not merged, because which stream a thing arrived on is itself worth asserting -
		/// a version a script cannot read without redirecting stderr is not much of a version.
		/// </remarks>
		public static ToolRun RunTool( string exe, IEnumerable<string> args, TimeSpan? timeout = null )
		{
			var psi = new ProcessStartInfo( exe )
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
			};

			foreach( var arg in args ) psi.ArgumentList.Add( arg );

			using var process = Process.Start( psi ) ?? throw new Exception( $"could not start {exe}" );

			// read both before waiting, or a tool that fills a pipe deadlocks against us
			var stdout = process.StandardOutput.ReadToEndAsync();
			var stderr = process.StandardError.ReadToEndAsync();

			if( !process.WaitForExit( (int) ( timeout ?? TimeSpan.FromSeconds( 60 ) ).TotalMilliseconds ) )
			{
				try { process.Kill( entireProcessTree: true ); } catch { }
				throw new Exception( $"{Path.GetFileName( exe )} did not finish within the timeout" );
			}

			return new ToolRun()
			{
				ExitCode = process.ExitCode,
				StdOut = stdout.GetAwaiter().GetResult(),
				StdErr = stderr.GetAwaiter().GetResult(),
			};
		}

		/// <summary>What to tell somebody who wants to look at a world by hand.</summary>
		public string Describe()
		{
			var text = new StringBuilder();

			text.AppendLine( $"Dirigent tier-3 world '{Scenario}'" );
			text.AppendLine( $"  folder        {Root}" );
			text.AppendLine( $"  shared config {Files.SharedConfig}" );
			text.AppendLine( $"  master port   {MasterPort}" );
			text.AppendLine( $"  command port  {CliPort}" );

			if( HttpPort > 0 ) text.AppendLine( $"  web server    http://127.0.0.1:{HttpPort}/" );

			text.AppendLine( $"  machines      {string.Join( ", ", Files.Machines )}" );
			text.AppendLine( $"  applications  {string.Join( ", ", Files.Apps.Select( a => a.IdTuple ) )}" );
			text.AppendLine( $"  file nodes    {string.Join( ", ", Files.VfsNodes )}" );

			return text.ToString();
		}
	}
}
