using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;

namespace Dirigent.SystemTests
{
	/// <summary>
	/// Crashes for real: two programs of the test app that crash on purpose under a plan, the Windows event log of this machine, the
	/// real agent and master processes - and GET /api/crashes finding them with the right app, plan and uptime.
	/// </summary>
	[TestClass()]
	public class CrashTests
	{
		/// <summary>Whether Windows wrote an Application Error event for the test program since the time - it does not when WER is switched off (or throttles).</summary>
		static int WindowsRecordedCrashesOfTheTestApp( DateTime sinceUtc )
		{
			var xpath = "*[System[Provider[@Name='Application Error'] and EventID=1000 and TimeCreated[@SystemTime>='"
						+ sinceUtc.ToString( "yyyy-MM-ddTHH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture ) + "']]]";

			try
			{
				var query = new System.Diagnostics.Eventing.Reader.EventLogQuery( "Application", System.Diagnostics.Eventing.Reader.PathType.LogName, xpath );
				using var reader = new System.Diagnostics.Eventing.Reader.EventLogReader( query );
				var n = 0;
				for( var rec = reader.ReadEvent(); rec is not null; rec = reader.ReadEvent() )
				{
					using( rec )
						if( rec.ToXml().Contains( "Dirigent.TestApp.exe", StringComparison.OrdinalIgnoreCase ) ) n++;
				}
				return n;
			}
			catch( Exception )
			{
				return 0;
			}
		}

		[TestMethod()]
		public void ProgramsThatReallyCrashAreFoundWithTheirAppPlanAndUptime()
		{
			var started = DateTime.UtcNow.AddSeconds( -5 );

			using var world = SystemWorld.Start( "CrashWorld", withHttp: true );
			using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds( 60 ) };
			var root = $"http://127.0.0.1:{world.HttpPort}/api";

			var sinceText = Uri.EscapeDataString( started.ToString( "yyyy-MM-ddTHH:mm:ss'Z'" ) );

			world.Cli( "StartPlan pc" );

			// The crashed processes stay until Windows Error Reporting lets them go, which can take very long, and then belong to nobody:
			// the ones this test crashed are taken down at its end.
			var crashedPids = new System.Collections.Generic.HashSet<int>();
			try
			{
				Run();
			}
			finally
			{
				try
				{
					var states = JsonDocument.Parse( http.GetStringAsync( $"{root}/appstates" ).GetAwaiter().GetResult() ).RootElement;
					foreach( var s in states.EnumerateArray() )
						if( s.GetProperty( "state" ).GetProperty( "pid" ).ValueKind == JsonValueKind.Number )
							crashedPids.Add( s.GetProperty( "state" ).GetProperty( "pid" ).GetInt32() );
				}
				catch( Exception ) { }

				foreach( var pid in crashedPids )
				{
					try { System.Diagnostics.Process.GetProcessById( pid ).Kill(); }
					catch( Exception ) { } // gone already
				}

				// what holds them: Windows Error Reporting, with a clone of the process that stays with it
				try
				{
					using var searcher = new System.Management.ManagementObjectSearcher( "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'WerFault.exe'" );
					foreach( System.Management.ManagementObject wer in searcher.Get() )
					{
						var line = wer["CommandLine"] as string ?? "";
						if( crashedPids.Any( pid => line.Contains( $" -p {pid} " ) ) )
						{
							try { System.Diagnostics.Process.GetProcessById( Convert.ToInt32( wer["ProcessId"] ) ).Kill( entireProcessTree: true ); }
							catch( Exception ) { }
						}
					}
				}
				catch( Exception ) { }
			}

			void Run()
			{
			var lastText = "";
			JsonElement[] Crashes()
			{
				var response = http.GetAsync( $"{root}/crashes?since={sinceText}&machine=m1&kind=crash,managed,exit" ).GetAwaiter().GetResult();
				var text = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
				lastText = text;
				Assert.AreEqual( System.Net.HttpStatusCode.OK, response.StatusCode, text );
				var list = JsonDocument.Parse( text ).RootElement.GetProperty( "crashes" ).EnumerateArray().Select( e => e.Clone() ).ToArray();
				foreach( var e in list )
					if( e.GetProperty( "processId" ).ValueKind == JsonValueKind.Number ) crashedPids.Add( e.GetProperty( "processId" ).GetInt32() );
				return list;
			}

			bool Found( JsonElement[] list, string app, string kind )
				=> list.Any( e => e.GetProperty( "app" ).GetString() == app && e.GetProperty( "kind" ).GetString() == kind );

			int Exits( string app )
			{
				var states = JsonDocument.Parse( http.GetStringAsync( $"{root}/appstates" ).GetAwaiter().GetResult() ).RootElement;
				var state = states.EnumerateArray().First( s => s.GetProperty( "id" ).GetString()!.StartsWith( app ) );
				return state.GetProperty( "lastExits" ).GetArrayLength();
			}

			// Windows writes its events seconds after the crash. The crashed process lives on until Windows Error Reporting is
			// done with it (on some machines for minutes), so the master may not have written its exit down yet: the process is
			// then still the one the app state says runs, and its pid tells the app, plan and uptime all the same.
			try
			{
				world.WaitUntil( "both crashes are listed from the event log", TimeSpan.FromSeconds( 90 ), () =>
				{
					var list = Crashes();
					return Found( list, "m1.native", "managed" ) && Found( list, "m1.managed", "managed" );
				} );
			}
			catch( Exception e ) when( WindowsRecordedCrashesOfTheTestApp( started ) == 0 )
			{
				Assert.Inconclusive( "Windows on this machine wrote no Application Error event for the crashed test program (is Windows Error Reporting disabled?): " + e.Message );
			}
			catch( Exception e )
			{
				throw new Exception( $"Windows recorded the crashes, the list does not show them: {e.Message}{Environment.NewLine}the last answer: {lastText}{Environment.NewLine}{world.TailOfEveryLog( 40 )}", e );
			}

			var all = Crashes();

			// a fatal access violation of a .NET program is written by the runtime too (1026), so it is of kind managed, with the code of Windows
			var native = all.First( e => e.GetProperty( "app" ).GetString() == "m1.native" && e.GetProperty( "kind" ).GetString() == "managed" );
			Assert.AreEqual( "Native", native.GetProperty( "component" ).GetString() );
			Assert.AreEqual( "pc", native.GetProperty( "plan" ).GetString() );
			Assert.AreEqual( "0xc0000005", native.GetProperty( "exceptionCode" ).GetString() );
			StringAssert.Contains( native.GetProperty( "exceptionText" ).GetString(), "AccessViolationException" );
			Assert.AreEqual( 2, native.GetProperty( "apps" ).GetArrayLength(), "the two apps share the program; the pid told them apart" );
			var uptime = native.GetProperty( "uptimeSeconds" ).GetDouble();
			Assert.IsTrue( uptime >= 1.5 && uptime < 100, $"it crashed two seconds after the start (Windows may take a while to write the event): {uptime}" );

			var managed = all.First( e => e.GetProperty( "app" ).GetString() == "m1.managed" && e.GetProperty( "kind" ).GetString() == "managed" );
			Assert.AreEqual( "Managed", managed.GetProperty( "component" ).GetString() );
			Assert.AreEqual( "pc", managed.GetProperty( "plan" ).GetString() );
			Assert.AreEqual( "0xe0434352", managed.GetProperty( "exceptionCode" ).GetString() );
			StringAssert.Contains( managed.GetProperty( "exceptionText" ).GetString(), "TestApp crashes on purpose" );

			// the exit, whenever the master writes it down, is the crash the event describes: no event-less duplicate stands next to it
			Assert.IsFalse( Found( all, "m1.native", "exit" ), lastText );
			Assert.IsFalse( Found( all, "m1.managed", "exit" ), lastText );
			try { world.WaitUntil( "the master has written both exits down", TimeSpan.FromSeconds( 20 ), () => Exits( "m1.native" ) == 1 && Exits( "m1.managed" ) == 1 ); }
			catch( Exception ) { /* Windows may keep the crashed processes for long: then there is no exit to merge */ }
			var later = Crashes();
			Assert.IsFalse( Found( later, "m1.native", "exit" ), lastText );
			Assert.IsFalse( Found( later, "m1.managed", "exit" ), lastText );

			// by component, by app with a wildcard; newest per app
			string Ask( string q ) => http.GetStringAsync( $"{root}/crashes?since={sinceText}&{q}" ).GetAwaiter().GetResult();
			Assert.AreEqual( 1, JsonDocument.Parse( Ask( "component=Managed&kind=managed" ) ).RootElement.GetProperty( "crashes" ).GetArrayLength() );
			Assert.AreEqual( 0, JsonDocument.Parse( Ask( "component=Nothing" ) ).RootElement.GetProperty( "crashes" ).GetArrayLength() );
			Assert.AreEqual( 1, JsonDocument.Parse( Ask( "app=*.native&newestPerApp=1&kind=managed" ) ).RootElement.GetProperty( "crashes" ).GetArrayLength() );
			}
		}

		[TestMethod()]
		public void AnUntilBeforeSinceIsA400AndABadTimeToo()
		{
			using var world = SystemWorld.Start( "LoggingWorld", withHttp: true );
			using var http = new HttpClient() { Timeout = TimeSpan.FromSeconds( 30 ) };
			var root = $"http://127.0.0.1:{world.HttpPort}/api";

			var response = http.GetAsync( $"{root}/crashes?since=2026-11-21T10:00:00Z&until=2026-11-21T09:00:00Z" ).GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.BadRequest, response.StatusCode, response.Content.ReadAsStringAsync().GetAwaiter().GetResult() );

			Assert.AreEqual( System.Net.HttpStatusCode.BadRequest, http.GetAsync( $"{root}/crashes?since=yesterday-ish" ).GetAwaiter().GetResult().StatusCode );

			// and a plain call answers: the listing, the machines, the totals
			var ok = http.GetAsync( $"{root}/crashes" ).GetAwaiter().GetResult();
			var text = ok.Content.ReadAsStringAsync().GetAwaiter().GetResult();
			Assert.AreEqual( System.Net.HttpStatusCode.OK, ok.StatusCode, text );
			var doc = JsonDocument.Parse( text ).RootElement;
			Assert.IsTrue( doc.GetProperty( "machines" ).GetArrayLength() >= 2, text );
			Assert.IsTrue( doc.TryGetProperty( "totals", out _ ), text );
		}
	}
}
