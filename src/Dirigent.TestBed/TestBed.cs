using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dirigent.TestBed.Scenarios;

namespace Dirigent.TestBed
{
	public class TestBedOptions
	{
		/// <summary>
		/// Machine names, used verbatim. Runs stay apart through their own ports, temporary
		/// folder, agent status folder and download folder rather than through mangled ids.
		/// </summary>
		public List<string> Machines = new() { "m1", "m2" };

		/// <summary>
		/// The world to test in. When set, the machines, the shared config and the files that
		/// have to exist beforehand all come from here - which is what lets one description
		/// serve several tiers.
		/// </summary>
		public Scenario? Scenario;

		/// <summary>
		/// Raw SharedConfig.xml body, for the few tests that are about the config text itself.
		/// Ignored when a Scenario is given. These placeholders are substituted:
		///   {m1}, {m2}, ...  the real machine id of that machine
		///   {testapp}        full path of Dirigent.TestApp.exe
		///   {temp}           the per-run temporary folder
		/// </summary>
		public string SharedConfigXml = "<Shared/>";

		/// <summary>Pump period. Short, because tests should not wait on Dirigent's own pacing.</summary>
		public int TickPeriodMs = 20;

		/// <summary>Keep the temporary folder behind for inspection.</summary>
		public bool KeepTempRoot;
	}

	/// <summary>
	/// One process holding a master, a set of agents and an operator, all ticked by a single
	/// pump thread while the test thread asserts. Real TCP runs between them over loopback.
	/// </summary>
	public sealed class TestBed : Disposable
	{
		public string RunTag { get; }
		public string TempRoot { get; }
		public string SharedConfigPath { get; }
		public string TestAppPath { get; }

		/// <summary>Where the agents keep their status file, instead of the machine-global default.</summary>
		public string AgentStatusFolder { get; }

		/// <summary>What %DOWNLOADS% expands to for every component of this bed.</summary>
		public string DownloadFolder { get; }

		public int MasterPort { get; }
		public Operator Operator { get; }

		/// <summary>Where the run's folders are, for tests that need to look at the files.</summary>
		public RenderContext RenderContext { get; }

		/// <summary>machine name as the test knows it -> the real machine id in use</summary>
		public IReadOnlyDictionary<string, string> MachineIds => _machineIds;

		readonly Dictionary<string, string> _machineIds = new();
		readonly Master _master;
		readonly List<Agent> _agents = new();
		readonly Thread _pump;
		readonly CancellationTokenSource _stop = new();
		readonly TestBedOptions _opts;

		volatile Exception? _pumpFault;
		long _ticks;

		TestBed( TestBedOptions opts )
		{
			_opts = opts;
			RunTag = Isolation.NewRunTag();
			TempRoot = Isolation.CreateTempRoot( RunTag );
			TestAppPath = TestAppLocator.Find();

			// The machine ids can stay as the test wrote them: what used to force a per-run
			// suffix was the machine-global agent status file, which now lives under TempRoot.
			var machineNames = opts.Scenario is not null ? opts.Scenario.MachineNames : opts.Machines;
			if( machineNames.Count == 0 )
				throw new ArgumentException( "a test bed needs at least one machine" );

			foreach( var name in machineNames )
				_machineIds[name] = name;

			AgentStatusFolder = Path.Combine( TempRoot, "agentstatus" );
			DownloadFolder = Path.Combine( TempRoot, "downloads" );
			Directory.CreateDirectory( AgentStatusFolder );
			Directory.CreateDirectory( DownloadFolder );

			Diagnostics.EnsureLogCapture();

			RenderContext = new RenderContext( TempRoot, TestAppPath, _machineIds );

			if( opts.Scenario is not null )
			{
				WorldSeeder.Seed( opts.Scenario.Spec, RenderContext );
				SharedConfigPath = WriteSharedConfig(
					SharedConfigRenderer.Render( opts.Scenario.Spec, RenderContext ) );
			}
			else
			{
				SharedConfigPath = WriteSharedConfig( RenderContext.Substitute( opts.SharedConfigXml ) );
			}

			MasterPort = Isolation.FreeTcpPort();
			var cliPort = Isolation.FreeTcpPort();

			var masterConfig = MakeAppConfig( _machineIds.Values.First(), MasterPort, cliPort );
			masterConfig.IsMaster = "1";
			_master = new Master( masterConfig, TempRoot );

			foreach( var machineId in _machineIds.Values )
			{
				var agentConfig = MakeAppConfig( machineId, MasterPort, cliPort );
				_agents.Add( new Agent( agentConfig, machineId ) );
			}

			Operator = new Operator( $"op_{RunTag}", "127.0.0.1", MasterPort, TempRoot );

			_pump = new Thread( PumpLoop ) { IsBackground = true, Name = $"dirigent-testbed-{RunTag}" };
			_pump.Start();
		}

		/// <summary>
		/// Brings up the world and returns once the master, every agent and the operator are
		/// connected and the app definitions have reached the operator.
		/// </summary>
		public static async Task<TestBed> StartAsync( TestBedOptions opts )
		{
			var bed = new TestBed( opts );
			try
			{
				await bed.WaitUntilAsync(
					async () =>
					{
						if( !bed.Operator.IsConnected ) return false;
						var clients = await bed.Operator.GetAllClientsStateAsync();
						var connected = clients.Where( x => x.Value.Connected ).Select( x => x.Key ).ToHashSet();
						return bed._machineIds.Values.All( connected.Contains );
					},
					TimeSpan.FromSeconds( 20 ),
					"the master, every agent and the operator are connected" );

				return bed;
			}
			catch
			{
				bed.Dispose();
				throw;
			}
		}

		protected override void Dispose( bool disposing )
		{
			base.Dispose( disposing );
			if( !disposing ) return;

			// Dirigent deliberately leaves managed applications running when an agent goes away,
			// which is right in production and wrong here: a test that starts an app and does not
			// kill it would leave it behind for good. Read the survivors while the pump can still
			// answer, then make sure they are gone.
			var survivors = CollectRunningProcesses();

			_stop.Cancel();
			if( _pump.IsAlive && !_pump.Join( TimeSpan.FromSeconds( 5 ) ) )
			{
				// the pump is stuck; nothing safe left to do but say so
				Console.WriteLine( $"[testbed {RunTag}] pump thread did not stop within 5 s" );
			}

			Operator.Dispose();
			foreach( var agent in _agents ) SafeDispose( agent );
			SafeDispose( _master );

			KillProcesses( survivors );

			if( !_opts.KeepTempRoot )
				Isolation.DeleteTempRoot( TempRoot );
			else
				Console.WriteLine( $"[testbed {RunTag}] kept {TempRoot}" );
		}

		static void SafeDispose( IDisposable? d )
		{
			try { d?.Dispose(); } catch( Exception ex ) { Console.WriteLine( $"[testbed] dispose failed: {ex.Message}" ); }
		}

		/// <summary>
		/// The pids of applications still running, taken from the state the operator holds.
		/// Bounded by a timeout so a stuck pump cannot hang the teardown.
		/// </summary>
		List<int> CollectRunningProcesses()
		{
			var pids = new List<int>();
			try
			{
				var task = Operator.GetAllAppsStateAsync();
				if( !task.Wait( TimeSpan.FromSeconds( 2 ) ) ) return pids;

				foreach( var (_, state) in task.Result )
				{
					if( state.Running && state.PID > 0 )
						pids.Add( state.PID );
				}
			}
			catch( Exception ex )
			{
				Console.WriteLine( $"[testbed {RunTag}] could not read app states for cleanup: {ex.Message}" );
			}
			return pids;
		}

		/// <summary>
		/// Kills the given processes and their children, but only those that really are our test
		/// application - a pid from a stale state could by then belong to something else entirely.
		/// </summary>
		void KillProcesses( List<int> pids )
		{
			foreach( var pid in pids )
			{
				try
				{
					using var process = Process.GetProcessById( pid );
					if( process.HasExited ) continue;

					if( !IsOurTestApp( process ) )
					{
						Console.WriteLine( $"[testbed {RunTag}] pid {pid} is not the test app, leaving it alone" );
						continue;
					}

					process.Kill( entireProcessTree: true );
					process.WaitForExit( 5000 );
				}
				catch( ArgumentException )
				{
					// already gone between the read and now, which is the good case
				}
				catch( Exception ex )
				{
					Console.WriteLine( $"[testbed {RunTag}] could not kill pid {pid}: {ex.Message}" );
				}
			}
		}

		bool IsOurTestApp( Process process )
		{
			try
			{
				var path = process.MainModule?.FileName;
				return !string.IsNullOrEmpty( path )
					&& string.Equals( path, TestAppPath, StringComparison.OrdinalIgnoreCase );
			}
			catch( Exception )
			{
				// cannot see the module, so cannot prove it is ours - do not kill it
				return false;
			}
		}

		// ---- the pump ---------------------------------------------------------------

		void PumpLoop()
		{
			while( !_stop.IsCancellationRequested )
			{
				try
				{
					_master.Tick();
					foreach( var agent in _agents ) agent.Tick();
					Operator.Tick();
					Interlocked.Increment( ref _ticks );
				}
				catch( Exception ex )
				{
					// Remember the first fault and keep pumping: a test that is mid-wait needs
					// the reason, and stopping the pump would only produce a timeout instead.
					_pumpFault ??= ex;
				}

				Thread.Sleep( _opts.TickPeriodMs );
			}
		}

		// ---- waiting ---------------------------------------------------------------

		/// <summary>
		/// The only way a test is allowed to wait. There is no virtual time in Dirigent, so a
		/// fixed sleep would be a guess; this waits for the fact itself and explains what it
		/// was waiting for when it gives up.
		/// </summary>
		public async Task WaitUntilAsync( Func<Task<bool>> condition, TimeSpan timeout, string because )
		{
			var sw = Stopwatch.StartNew();
			Exception? lastConditionError = null;

			while( sw.Elapsed < timeout )
			{
				ThrowIfPumpFaulted();

				try
				{
					if( await condition() ) return;
					lastConditionError = null;
				}
				catch( Exception ex )
				{
					// a condition may legitimately fail while the world is still coming up
					lastConditionError = ex;
				}

				await Task.Delay( 25 );
			}

			ThrowIfPumpFaulted();

			var detail = lastConditionError is null ? "" : $"\nlast condition error: {lastConditionError.Message}";
			throw new TimeoutException(
				$"Timed out after {timeout.TotalSeconds:0.#} s waiting until {because}.{detail}\n\n{await DescribeAsync()}" );
		}

		public Task WaitUntilAsync( Func<bool> condition, TimeSpan timeout, string because )
			=> WaitUntilAsync( () => Task.FromResult( condition() ), timeout, because );

		void ThrowIfPumpFaulted()
		{
			var fault = _pumpFault;
			if( fault is not null )
				throw new Exception( $"the test bed pump faulted: {fault.Message}", fault );
		}

		// ---- diagnostics -----------------------------------------------------------

		/// <summary>
		/// What the world looked like at the moment of failure. Without this, every timeout
		/// looks the same and debugging these tests is miserable.
		/// </summary>
		public async Task<string> DescribeAsync()
		{
			var sb = new StringBuilder();
			sb.AppendLine( $"test bed {RunTag}: master port {MasterPort}, {Interlocked.Read( ref _ticks )} ticks, temp {TempRoot}" );

			try
			{
				sb.AppendLine( "clients:" );
				foreach( var (id, state) in await Operator.GetAllClientsStateAsync() )
					sb.AppendLine( $"    {id,-24} connected={state.Connected} ip={state.IP}" );

				sb.AppendLine( "app definitions:" );
				foreach( var (id, _) in await Operator.GetAllAppsDefAsync() )
					sb.AppendLine( $"    {id}" );

				sb.AppendLine( "app states:" );
				foreach( var (id, st) in await Operator.GetAllAppsStateAsync() )
					sb.AppendLine( $"    {id,-24} started={st.Started} running={st.Running} killed={st.Killed}"
									+ $" initialized={st.Initialized} exitCode={st.ExitCode} pid={st.PID} planName={st.PlanName}" );

				var notifications = Operator.Notifications;
				if( notifications.Count > 0 )
				{
					sb.AppendLine( "notifications:" );
					foreach( var n in notifications )
						sb.AppendLine( $"    [{n.Category}] {n.Message}" );
				}
			}
			catch( Exception ex )
			{
				sb.AppendLine( $"    (could not read state: {ex.Message})" );
			}

			sb.Append( Diagnostics.RecentLog( 40 ) );
			return sb.ToString();
		}

		// ---- setup helpers ---------------------------------------------------------

		AppConfig MakeAppConfig( string machineId, int masterPort, int cliPort )
		{
			return new AppConfig()
			{
				MachineId = machineId,
				MasterIP = "127.0.0.1",
				MasterPort = masterPort,
				CliPort = cliPort,
				HttpPort = 0,            // the web server binds http://*:port, which needs a URL ACL
				SharedCfgFileName = SharedConfigPath,
				LocalCfgFileName = "",   // no tools needed yet
				RootForRelativePaths = TempRoot,
				AgentStatusFolder = AgentStatusFolder,
				DownloadFolder = DownloadFolder,
				TickPeriod = _opts.TickPeriodMs,
				MasterTickPeriod = _opts.TickPeriodMs,
				LogFileName = "",
				StartupPlan = "",
				StartupScript = "",
				IsMaster = "0",
				Debug = "1",             // do not swallow exceptions
			};
		}

		string WriteSharedConfig( string xml )
		{
			var path = Path.Combine( TempRoot, "SharedConfig.xml" );
			File.WriteAllText( path, xml, Encoding.UTF8 );
			return path;
		}

		/// <summary>The real machine id behind a name the test used, e.g. "m1" -> "m1_a3f0c1".</summary>
		public string Machine( string name ) => _machineIds[name];

		/// <summary>An app id tuple on one of the run's machines, e.g. App("m1","hello").</summary>
		public AppIdTuple App( string machineName, string appId ) => new AppIdTuple( Machine( machineName ), appId );
	}
}
