using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.Net;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// Measurements of the idle traffic of a site - not tests: they assert nothing and take minutes, so they run only when
	/// <c>DIRIGENT_MEASURE=1</c> is set (and are skipped otherwise). The report goes to the console and to the file named by
	/// <c>DIRIGENT_MEASURE_OUT</c> (by default dirigent-traffic-measurement.txt in the temp folder).
	/// </summary>
	/// <remarks>
	/// The site is N machines with ten applications each - defined, not running; an agent publishes the state of every app it
	/// has - and an agent and a GUI client on every machine, ticked as in production: agents and GUIs every 500 ms, the master
	/// every 50 ms (its own refresh of the clients is once a second). Master, agents and GUIs share this one process, which a
	/// real site does not, so the measurement is of the bytes on the master's connections, not of anybody's CPU.
	/// <c>DIRIGENT_MEASURE_N</c> overrides the sizes ("10,20,40,80").
	/// </remarks>
	[TestClass()]
	public class TrafficMeasurementTests
	{
		const int AppsPerMachine = 10;
		const double Gigabit = 125_000_000.0; // bytes per second

		static bool Enabled => Environment.GetEnvironmentVariable( "DIRIGENT_MEASURE" ) == "1";

		static string OutFile => Environment.GetEnvironmentVariable( "DIRIGENT_MEASURE_OUT" )
			?? Path.Combine( Path.GetTempPath(), "dirigent-traffic-measurement.txt" );

		static readonly StringBuilder _report = new();

		static void Say( string line = "" )
		{
			Console.WriteLine( line );
			_report.AppendLine( line );
		}

		static void Save() => File.WriteAllText( OutFile, _report.ToString() );

		// ---- the simulated site ------------------------------------------------------------------------------------

		/// <summary>What the master did in the measured seconds.</summary>
		record Measured( int Machines, TrafficStats.Report Traffic, double Seconds, bool GuisWantFeed = true );

		static Scenario Site( int machines )
		{
			var names = Enumerable.Range( 1, machines ).Select( i => $"m{i}" ).ToArray();
			var scenario = new Scenario().Machines( names );
			foreach( var m in names )
				for( int a = 1; a <= AppsPerMachine; a++ )
					scenario.App( $"{m}.a{a}", x => x.LongRunning() );
			return scenario;
		}

		static async Task<Measured> MeasureAsync( int machines, TimeSpan settle, TimeSpan measure, bool guisWantFeed = true )
		{
			var options = new TestBedOptions()
			{
				Scenario = Site( machines ),
				TickPeriodMs = 50,           // the master's tick, as in production
				AgentTickPeriodMs = 500,     // the agents' and the GUIs'
				GuiPerMachine = true,
			};

			using var bed = await TestBed.TestBed.StartAsync( options );

			// the common case of a site: every GUI is minimised, nobody looks
			if( !guisWantFeed ) await bed.SetGuisFeedWantedAsync( false );

			await Task.Delay( settle );
			bed.Master.Traffic.Reset();

			var clock = Stopwatch.StartNew();
			await Task.Delay( measure );
			var report = bed.Master.Traffic.Snapshot();
			clock.Stop();

			return new Measured( machines, report, clock.Elapsed.TotalSeconds, guisWantFeed );
		}

		static void Print( Measured m )
		{
			Say();
			Say( $"=== {m.Machines} machines x {AppsPerMachine} apps, {m.Machines} agents, {m.Machines} GUIs ({( m.GuisWantFeed ? "all want the feed" : "none wants the feed: all minimised" )}); master, {m.Traffic.seconds:0.0} s ===" );

			void Table( string title, List<TrafficStats.TypeTraffic> rows, TrafficStats.DirectionTraffic total )
			{
				Say( $"  {title}" );
				Say( $"    {"type",-28} {"msg/s",10} {"bytes/s",14} {"avg bytes/msg",14}" );
				foreach( var t in rows )
					Say( $"    {t.type,-28} {t.messages / m.Traffic.seconds,10:0.0} {t.bytesPerSecond,14:#,##0} {( t.messages > 0 ? t.bytes / (double) t.messages : 0 ),14:#,##0}" );
				Say( $"    {"TOTAL",-28} {total.messages / m.Traffic.seconds,10:0.0} {total.bytesPerSecond,14:#,##0}" );
			}

			Table( "master SENT", m.Traffic.sent, m.Traffic.sentTotal );
			Table( "master RECEIVED", m.Traffic.received, m.Traffic.receivedTotal );

			var sent = m.Traffic.sentTotal.bytesPerSecond;
			var received = m.Traffic.receivedTotal.bytesPerSecond;
			Say( $"  sent {sent / 1e6:0.00} MB/s = {sent / Gigabit * 100:0.0} % of 1 Gbit;"
				+ $" received {received / 1e6:0.00} MB/s = {received / Gigabit * 100:0.0} %;"
				+ $" both {( sent + received ) / 1e6:0.00} MB/s = {( sent + received ) / Gigabit * 100:0.0} %" );
		}

		/// <summary>The bytes per second of a type, 0 where the run never sent one.</summary>
		static double Rate( Measured m, bool sent, string type )
			=> ( sent ? m.Traffic.sent : m.Traffic.received ).FirstOrDefault( t => t.type == type )?.bytesPerSecond ?? 0;

		/// <summary>
		/// Least-squares fit of y = a*N + b*N^2 (no constant: nothing is sent by no machines), and the power law y = c*N^k
		/// through the logarithms, as the two readings of "how does it grow".
		/// </summary>
		static (double a, double b, double k) Fit( IReadOnlyList<(double n, double y)> points )
		{
			double s11 = 0, s12 = 0, s22 = 0, t1 = 0, t2 = 0;
			foreach( var (n, y) in points )
			{
				s11 += n * n; s12 += n * n * n; s22 += n * n * n * n;
				t1 += n * y; t2 += n * n * y;
			}
			var det = s11 * s22 - s12 * s12;
			var a = det == 0 ? 0 : ( t1 * s22 - t2 * s12 ) / det;
			var b = det == 0 ? 0 : ( s11 * t2 - s12 * t1 ) / det;

			var pos = points.Where( p => p.y > 0 ).Select( p => (x: Math.Log( p.n ), y: Math.Log( p.y )) ).ToList();
			double k = 0;
			if( pos.Count >= 2 )
			{
				var mx = pos.Average( p => p.x );
				var my = pos.Average( p => p.y );
				var den = pos.Sum( p => ( p.x - mx ) * ( p.x - mx ) );
				k = den == 0 ? 0 : pos.Sum( p => ( p.x - mx ) * ( p.y - my ) ) / den;
			}

			return (a, b, k);
		}

		static void PrintGrowth( List<Measured> runs )
		{
			if( runs.Count < 2 ) return;

			Say();
			Say( $"=== growth with the number of machines (N = {string.Join( ", ", runs.Select( r => r.Machines ) )}) ===" );
			Say( "  fit y = a*N + b*N^2 by least squares (bytes/s), power law y ~ N^k, and the value at N = 80" );
			Say( $"    {"",-8} {"type",-28} {"a (per N)",12} {"b (per N^2)",12} {"k",6} {"at 80 (bytes/s)",16}" );

			void Rows( bool sent )
			{
				var types = runs.SelectMany( r => ( sent ? r.Traffic.sent : r.Traffic.received ).Select( t => t.type ) ).Distinct().ToList();
				var rows = new List<(string type, double a, double b, double k, double at80)>();
				foreach( var type in types )
				{
					var (a, b, k) = Fit( runs.Select( r => ((double) r.Machines, Rate( r, sent, type )) ).ToList() );
					rows.Add( (type, a, b, k, a * 80 + b * 80 * 80) );
				}
				foreach( var r in rows.OrderByDescending( r => r.at80 ) )
					Say( $"    {( sent ? "sent" : "received" ),-8} {r.type,-28} {r.a,12:0.0} {r.b,12:0.000} {r.k,6:0.00} {Math.Max( 0, r.at80 ),16:#,##0}" );

				var total = runs.Select( r => ((double) r.Machines, ( sent ? r.Traffic.sentTotal : r.Traffic.receivedTotal ).bytesPerSecond) ).ToList();
				var (ta, tb, tk) = Fit( total );
				var t80 = ta * 80 + tb * 80 * 80;
				Say( $"    {( sent ? "sent" : "received" ),-8} {"TOTAL",-28} {ta,12:0.0} {tb,12:0.000} {tk,6:0.00} {Math.Max( 0, t80 ),16:#,##0}"
					+ $"   = {Math.Max( 0, t80 ) / 1e6:0.0} MB/s = {Math.Max( 0, t80 ) / Gigabit * 100:0.0} % of 1 Gbit" );
			}

			Rows( true );
			Rows( false );

			var measured80 = runs.FirstOrDefault( r => r.Machines == 80 );
			if( measured80 is not null )
				Say( $"  (80 machines were measured: sent {measured80.Traffic.sentTotal.bytesPerSecond / 1e6:0.00} MB/s, received {measured80.Traffic.receivedTotal.bytesPerSecond / 1e6:0.00} MB/s)" );
		}

		[TestMethod()]
		[TestCategory( "Measurement" )]
		public async Task TheIdleTrafficOfTheMasterByTheNumberOfMachines()
		{
			if( !Enabled ) Assert.Inconclusive( "a measurement: set DIRIGENT_MEASURE=1 to run it" );

			var sizes = ( Environment.GetEnvironmentVariable( "DIRIGENT_MEASURE_N" ) ?? "10,20,40,80" )
				.Split( ',', StringSplitOptions.RemoveEmptyEntries ).Select( int.Parse ).ToList();

			Say( $"Idle traffic of the master, {DateTime.Now:yyyy-MM-dd HH:mm}, production timing: agents and GUIs 500 ms, master tick 50 ms, client refresh 1 s." );

			// DIRIGENT_MEASURE_FEED: on = the GUIs look at the feed, off = they are all minimised, both (default) = one run of each
			var feedModes = ( Environment.GetEnvironmentVariable( "DIRIGENT_MEASURE_FEED" ) ?? "both" ).ToLowerInvariant() switch
			{
				"on" => new[] { true },
				"off" => new[] { false },
				_ => new[] { false, true },
			};

			foreach( var wantFeed in feedModes )
			{
				var runs = new List<Measured>();
				foreach( var n in sizes )
				{
					try
					{
						var m = await MeasureAsync( n, TimeSpan.FromSeconds( 5 ), TimeSpan.FromSeconds( 30 ), wantFeed );
						runs.Add( m );
						Print( m );
					}
					catch( Exception e )
					{
						Say( $"=== {n} machines: did not run - {e.GetType().Name}: {Tools.JustFirstLine( e.Message )}" );
						Save();
						break; // a bigger one will not do better
					}
					Save();
				}

				Say();
				Say( wantFeed ? "--- growth, GUIs looking at the feed ---" : "--- growth, all GUIs minimised ---" );
				PrintGrowth( runs );
				Save();
			}

			Say( $"(report in {OutFile})" );
		}

		// ---- the sizes of the messages, offline ----------------------------------------------------------------

		static int Size( MessagePack.MessagePackSerializerOptions options, Net.Message msg )
			=> MessagePack.MessagePackSerializer.Serialize<Net.Message>( msg, options ).Length;

		[TestMethod()]
		[TestCategory( "Measurement" )]
		public void TheSizeOfTheStateMessagesOffline()
		{
			if( !Enabled ) Assert.Inconclusive( "a measurement: set DIRIGENT_MEASURE=1 to run it" );

			_ = new MsgPackCodec(); // sets the options the product uses

			// the same integer-keyed encoding without and with the compression the product applies
			var plain = MessagePack.MessagePackSerializerOptions.Standard;
			var lz4 = MsgPackCodec.Options;

			var now = DateTime.UtcNow;
			var states = new Dictionary<AppIdTuple, AppState>();
			for( int m = 1; m <= 80; m++ )
				for( int a = 1; a <= AppsPerMachine; a++ )
				{
					var running = a % 3 != 0;
					var state = new AppState() { PlanName = m % 2 == 0 ? "plan_normal" : "plan_service" };
					state.Started = running;
					state.Running = running;
					state.Initialized = running;
					state.PlanApplied = true;
					state.PID = running ? 1000 + m * 37 + a : 0;
					state.StartedAt = running ? now.AddMinutes( -( m * 7 + a ) ) : null;
					state.CPU = running ? ( m * a ) % 40 + 0.5f : -1;
					state.Memory = running ? 100 + ( m * a ) % 900 : -1;
					state.RestartsRemaining = 3;
					states[new AppIdTuple( $"m{m}", $"app{a}" )] = state;
				}

			var ident = new ClientIdent( "m17", EMsgRecipCateg.Agent );
			ident.LocalAddresses.Add( "10.20.30.17" );
			ident.Nics.Add( new NicInfo() { Mac = "AA-BB-CC-DD-EE-17", Ip = "10.20.30.17", PrefixLength = 24 } );
			var clientState = new ClientState() { Ident = ident, IP = "10.20.30.17" };
			clientState.Connected = true;

			var plans = new Dictionary<string, PlanState>();
			for( int p = 1; p <= 4; p++ )
			{
				var ps = new PlanState() { Running = p == 1, TimeStarted = now.AddHours( -2 ), PlanAppStates = new Dictionary<AppIdTuple, PlanAppState>() };
				for( int m = 1; m <= 20; m++ )
					for( int a = 1; a <= 4; a++ )
						ps.PlanAppStates[new AppIdTuple( $"m{m + ( p - 1 ) * 20}", $"app{a}" )] = new PlanAppState();
				plans[$"plan{p}"] = ps;
			}

			var messages = new (string name, Net.Message msg, int count)[]
			{
				( "AppsStateMessage (800 apps - the master's to a GUI)", new AppsStateMessage( states, now ), states.Count ),
				( "AppsStateMessage (10 apps - an agent's to the master)", new AppsStateMessage( states.Where( s => s.Key.MachineId == "m17" ).ToDictionary( s => s.Key, s => s.Value ), now ), AppsPerMachine ),
				( "ClientStateMessage", new ClientStateMessage( now, clientState ), 1 ),
				( "MachineStateMessage", new MachineStateMessage( "m17", now, new MachineState() { CPU = 12.5f, MemoryAvailMB = 8123, MemoryTotalMB = 16384 } ), 1 ),
				( "PlansStateMessage (4 plans x 80 apps)", new PlansStateMessage( plans ), plans.Sum( p => p.Value.PlanAppStates.Count ) ),
			};

			Say();
			Say( "=== sizes of the messages, serialized by the product's codec (integer keys, LZ4), framed with the 4-byte header ===" );
			Say( $"    {"message",-58} {"keys",13} {"keys+LZ4",8} {"ratio",7} {"B/item",8} {"LZ4 B/item",11}" );
			foreach( var (name, msg, count) in messages )
			{
				var ms = new MemoryStream();
				MsgPackCodec.Serialize( ms, msg );
				var lz = ms.Position; // what the product sends
				var framed = Size( plain, msg ) + MessageCodec.HeaderLen;
				Say( $"    {name,-58} {framed,13:#,##0} {lz,8:#,##0} {lz / (double) framed,7:0.00} {framed / (double) count,8:0.0} {lz / (double) count,11:0.0}" );
			}
			Say( "  (keys+LZ4 = MessagePackCompression.Lz4BlockArray on the same options; it compresses bodies over 64 bytes, smaller ones are as they were)" );
			Save();
		}
	}
}
