using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System.Diagnostics;

namespace Dirigent.SystemTests
{
	/// <summary>
	/// The shipped `Dirigent.CLI.exe`: what it prints, and what it exits with.
	/// </summary>
	/// <remarks>
	/// The exit code is the whole reason these live at this tier - nothing below it can see a real
	/// process's code or its streams, and every mistake in this area has survived a first
	/// investigation by being read off a console by hand instead.
	/// </remarks>
	[TestClass()]
	public class CliExeTests
	{
		[TestMethod()]
		public void ItAnswersAndExitsZeroAsItAlwaysHas()
		{
			using var world = SystemWorld.Start( "LoggingWorld" );

			// a listing: the lines are printed and the END ends it
			var listing = world.CliExe( "GetAllAppsState" );

			Assert.AreEqual( 0, listing.ExitCode, $"the exe reported success: {listing.All}" );
			Assert.IsTrue( listing.AllLines.Any( l => l.StartsWith( "APP:m1.camera:" ) ),
				$"it printed what the master said: {listing.All}" );
			Assert.IsTrue( listing.AllLines.Any( l => l == "END" ), $"including the terminator: {listing.All}" );

			// a simple command: one ACK
			var started = world.CliExe( "StartApp m1.camera" );

			Assert.AreEqual( 0, started.ExitCode, $"starting an app succeeded: {started.All}" );
			Assert.IsTrue( started.AllLines.Any( l => l.StartsWith( "ACK" ) ),
				$"the master acknowledged it: {started.All}" );

			// and a command nobody knows is a failure, with the reason printed
			var unknown = world.CliExe( "NoSuchCommand" );

			Assert.AreEqual( 4, unknown.ExitCode, $"an error is exit code 4: {unknown.All}" );
			Assert.IsTrue( unknown.AllLines.Any( l => l.StartsWith( "ERROR" ) ),
				$"and it says what was wrong: {unknown.All}" );
		}

		[TestMethod()]
		public void ASingleGetterSucceedsAtOnceInsteadOfTimingOut()
		{
			// The fourth response shape: GetPlanState, GetAppState, GetScriptState and GetClientState
			// answer one line carrying the answer, with no ACK and no END after it. The exe used to
			// wait for the ACK they never send, so it printed the right answer, sat out its
			// five-second read timeout and then reported failure - exit code 4 for a query that had
			// worked. Reported from a live site, whose runbook says to ignore the exit code of these.
			//
			// Nothing about the master's answer changed to fix it: the commands declare their shape
			// and the client honours it. What a telnet client sees is what it always saw.
			//
			// WaitingWorld rather than LoggingWorld, which declares no plan at all - there has to be
			// one for GetPlanState to have an answer.
			using var world = SystemWorld.Start( "WaitingWorld" );

			foreach( var (command, expected) in new[]
			{
				( "GetPlanState never",    "PLAN:never:" ),
				( "GetAppState m1.camera", "APP:m1.camera:" ),
				( "GetClientState m1",     "CLIENT:m1:" ),
			} )
			{
				var clock = Stopwatch.StartNew();
				var run = world.CliExe( command );
				clock.Stop();

				Assert.AreEqual( 0, run.ExitCode,
					$"'{command}' answered, so it succeeded: {run.All}" );

				Assert.IsTrue( run.AllLines.Any( l => l.StartsWith( expected ) ),
					$"'{command}' printed the answer: {run.All}" );

				// the answer was always immediate; it is the exit that used to be five seconds late
				Assert.IsTrue( clock.Elapsed < TimeSpan.FromSeconds( 4 ),
					$"'{command}' returned without waiting out a read timeout ({clock.ElapsedMilliseconds} ms)" );
			}

			// Asking about something that does not exist is still a failure, and still immediate: the
			// master answers these with an empty line, which carries no state for a caller to read.
			// So an unknown name and a real error are the same exit code, which is why a script reads
			// the answer line rather than only the code.
			foreach( var command in new[]
			{
				"GetPlanState nosuchplan",
				"GetAppState m1.nosuchapp",
				$"GetScriptState {Guid.NewGuid()}",
			} )
			{
				var clock = Stopwatch.StartNew();
				var run = world.CliExe( command );
				clock.Stop();

				Assert.AreEqual( 4, run.ExitCode,
					$"'{command}' found nothing, which is not a success: {run.All}" );

				Assert.IsTrue( clock.Elapsed < TimeSpan.FromSeconds( 4 ),
					$"'{command}' failed at once ({clock.ElapsedMilliseconds} ms)" );
			}
		}

		[TestMethod()]
		public void ItWaitsForAScriptToFinishBeforeItReturns()
		{
			// What a plan step or a batch file needs: the exe must not report success at the ACK,
			// which says only that the master accepted the command. It waits for the END that
			// WaitForScript sends when the script is really over.
			using var world = SystemWorld.Start( "WaitingWorld" );

			// a script waiting for a machine no agent serves - it cannot finish on its own. The
			// relaxed JSON goes in single quotes, doubled inside, which is the form that survives
			// both parsers (see docs/CLI.md).
			var hanging = Guid.NewGuid().ToString();

			var clock = Stopwatch.StartNew();
			var timedOut = world.CliExe(
					$"StartScript {hanging} BuiltIns/RunPlanWhenMachinesOnline.cs '{{Plan:''never''}}'"
					+ $" ; WaitForScript {hanging} timeout=4" );
			clock.Stop();

			Assert.IsTrue( clock.Elapsed >= TimeSpan.FromSeconds( 3 ),
				$"it waited for the script rather than returning at the ACK: "
				+ $"{clock.Elapsed.TotalSeconds:0} s, answered {timedOut.All}" );

			Assert.AreEqual( 4, timedOut.ExitCode,
				$"the wait timed out, which is a failed command: {timedOut.All}" );

			Assert.IsTrue( timedOut.AllLines.Any(
					l => l.StartsWith( "ERROR" ) && l.Contains( "did not finish" ) ),
				$"and the reason is the timeout: {timedOut.All}" );

			// and a script that does finish: success, once it is over
			var finishing = Guid.NewGuid().ToString();

			var ok = world.CliExe( $"StartScript {finishing} BuiltIns/ListVfsNodes.cs"
					+ $" ; WaitForScript {finishing} timeout=30" );

			Assert.AreEqual( 0, ok.ExitCode, $"the script finished: {ok.All}" );
			Assert.IsTrue( ok.AllLines.Any( l => l == "END" ), $"the wait ended with END: {ok.All}" );

			var state = world.Cli( $"GetScriptState {finishing}" );
			StringAssert.Contains( state, "Finished",
				$"it really had finished by the time the exe returned: {state}" );
		}

		[TestMethod()]
		public void HelpAndVersionAreAnsweredOnStdoutAndPrintedOnce()
		{
			// Every executable advertises --version two lines from the bottom of its own help screen,
			// and used to answer it with "Option 'version' is unknown" and exit code 2. The cause was
			// the argument array: the parser was handed Environment.GetCommandLineArgs(), which leads
			// with the executable's own path, so a positional value was always present and
			// CommandLineParser's built-in handling of a lone --version never triggered.
			//
			// The stream matters as much as the exit code: a version a script cannot read without
			// redirecting stderr is not much of a version. And the parser's own writer used to put
			// the usage text on stderr for a bad command line while the executable wrote the same
			// text itself, so one mistyped option produced the whole option list twice, on two
			// streams.
			//
			// No world needed - this never reaches a master.
			var tools = new[]
			{
				( Project: "Dirigent.CLI",           Exe: "Dirigent.CLI.exe" ),
				( Project: "Dirigent.Agent.Console", Exe: "Dirigent.Agent.exe" ),
				( Project: "Dirigent.Agent.Starter", Exe: "Dirigent.Agent.Starter.exe" ),
			};

			foreach( var tool in tools )
			{
				var exe = SystemWorld.Tool( tool.Project, tool.Exe );
				var program = Path.GetFileNameWithoutExtension( tool.Exe );

				foreach( var option in new[] { "--version", "--help" } )
				{
					var run = SystemWorld.RunTool( exe, new[] { option } );

					Assert.AreEqual( 0, run.ExitCode,
						$"{tool.Exe} {option} is a question answered, not an error: {run.All}" );

					Assert.IsFalse( run.All.Contains( "is unknown" ),
						$"{tool.Exe} {option} was rejected as an unknown option: {run.All}" );

					Assert.IsTrue( run.StdOut.Contains( program ),
						$"{tool.Exe} {option} answered on stdout, where a script can read it: "
						+ $"stdout='{run.StdOut.Trim()}' stderr='{run.StdErr.Trim()}'" );

					Assert.AreEqual( string.Empty, run.StdErr.Trim(),
						$"{tool.Exe} {option} wrote nothing to stderr: '{run.StdErr.Trim()}'" );
				}

				// and a genuine mistake still fails, with the usage text at most once - the point
				// being that it is not printed once per writer. The Starter prints none of it: it is
				// a background watchdog and says its piece through Debug.WriteLine.
				var bad = SystemWorld.RunTool( exe, new[] { "--nosuchoption" } );

				Assert.AreNotEqual( 0, bad.ExitCode,
					$"{tool.Exe} still refuses an unknown option: {bad.All}" );

				var listings = bad.All.Split( "--masterPort" ).Length - 1;
				Assert.IsTrue( listings <= 1,
					$"{tool.Exe} printed the option list at most once, not once per writer (saw {listings})" );
			}
		}
	}
}
