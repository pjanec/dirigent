using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// What a progress indicator is told during a download, in the order it is told.
	/// </summary>
	/// <remarks>
	/// A status bar is only as good as the numbers behind it, and those come from a script the GUI
	/// never sees the inside of. This reads the states as they are published - every one of them,
	/// not a sample - so that a complaint about what the bar looks like can be answered with what it
	/// was actually told.
	///
	/// The complaint that led to it: two indicators, one flashing past and one apparently frozen at
	/// zero for a long time before jumping to full. Both were the same operation. The first was a
	/// marquee - the moment between the script being started and its first word - and the "frozen"
	/// one was the lookup: resolving a package is one remote call per node, in sequence, and on a
	/// system of two machines and thirty nodes that is the longest part of the whole download, while
	/// nothing about it is measurable in advance. It published 0.0 throughout, which reads as a hung
	/// operation. It now says what it is doing and leaves the bar sweeping, which reads as work.
	/// </remarks>
	[TestClass()]
	public class DownloadProgressShapeTests
	{
		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		const int _nodesPerMachine = 20;

		/// <summary>
		/// A package of the shape a real one has: a couple of dozen declared files spread over two
		/// machines. The count is what matters - every node of another machine costs a remote call,
		/// and the operator waits for the sum of them.
		/// </summary>
		static Scenario World()
		{
			var scenario = Scenario.TwoMachines()
				.App( "m1.big", a => a.LongRunning() )
				.App( "m2.big", a => a.LongRunning() );

			foreach( var machine in new[] { "m1", "m2" } )
			{
				for( int i = 0; i < _nodesPerMachine; i++ )
				{
					var name = $"file{i:00}.log";

					scenario.App( $"{machine}.big",
						a => a.RawXml( $"<File Id='log{i:00}' Title='Log {i:00}'"
									+ $" Path='{{applogs}}\\{name}'/>" ),
						mustExist: true );

					scenario.Seed( $"{machine}.big", name, sizeBytes: 64 * 1024 );
				}
			}

			return scenario.Package( "pkg", "Logs/Everything",
				p => { for( int i = 0; i < _nodesPerMachine; i++ ) p.RefAll( $"log{i:00}" ); } );
		}

		/// <summary>Runs a download and returns every state it published, in order.</summary>
		static async Task<List<ScriptState>> StatesOfADownload( TestBed.TestBed bed )
		{
			var package = await bed.Operator.GetVfsNodeAsync( "pkg" );

			var (instance, completion) = await bed.Operator.StartScriptAsync<
					Scripts.BuiltIn.DownloadZipped.TArgs, Scripts.BuiltIn.DownloadZipped.TResult>(
				Scripts.BuiltIn.DownloadZipped._Name,
				new Scripts.BuiltIn.DownloadZipped.TArgs()
				{
					VfsNode = package,
					VfsNodeNeedsResolving = true, // as the GUI hands it over
				} );

			await completion;

			// the download's own states; a package download also runs a script per machine and one
			// to merge, and those have indicators of their own that this GUI does not show
			return bed.Operator.ScriptStates
					.Where( x => x.Instance == instance )
					.Select( x => x.State )
					.ToList();
		}

		/// <summary>The timeline, for whoever is reading a failure.</summary>
		static string Describe( List<ScriptState> states )
		{
			var text = new StringBuilder();

			foreach( var s in states )
			{
				text.AppendLine( $"  {s.Status,-9}  "
								+ $"{( s.Progress is double p ? $"{(int) ( p * 100 ),3}%" : "  -" )}  {s.Text}" );
			}

			return text.ToString();
		}

		[TestMethod()]
		public async Task TheIndicatorIsAlwaysToldSomethingItCanShow()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World() } );

			var states = await StatesOfADownload( bed );
			var timeline = Describe( states );

			Console.WriteLine( "what a progress indicator was told:" );
			Console.WriteLine( timeline );

			Assert.IsTrue( states.Count > 0, "the download published nothing at all" );

			// 1. Once it has said anything - a number or a phase - it never goes back to saying
			//    nothing. The one bare state is the runner's own "it has started", published before
			//    the script body runs; the indicator sweeps for that moment and then has something
			//    to show for the rest of the operation.
			bool spoken = false;
			foreach( var s in states.Where( x => x.Status == EScriptStatus.Running ) )
			{
				var says = s.Progress.HasValue || !string.IsNullOrWhiteSpace( s.Text );

				Assert.IsFalse( spoken && !says, $"it stopped saying anything at all:\n{timeline}" );
				spoken |= says;
			}

			Assert.IsTrue( spoken, $"the operation never said anything:\n{timeline}" );

			// 2. It never goes backwards - a bar that does reads as a fault in the operation.
			double highest = 0;
			foreach( var s in states.Where( x => x.Progress.HasValue ) )
			{
				Assert.IsTrue( s.Progress!.Value >= highest - 0.0001,
					$"the bar went backwards ({highest:0.00} -> {s.Progress:0.00}):\n{timeline}" );
				highest = Math.Max( highest, s.Progress.Value );
			}

			// 3. The phase that cannot measure itself says so instead of claiming zero.
			var lookup = states.FirstOrDefault(
					x => x.Status == EScriptStatus.Running && !string.IsNullOrWhiteSpace( x.Text ) );

			Assert.IsNotNull( lookup, $"the operation never said what it was doing:\n{timeline}" );
			StringAssert.Contains( lookup!.Text ?? "", "Looking up",
				$"the first thing it does is look the package up, and it says so:\n{timeline}" );
			Assert.IsFalse( lookup.Progress.HasValue,
				$"and it admits it cannot measure that rather than sitting at a number:\n{timeline}" );

			// 4. Once there is a number it stays a number, so the indicator does not flick back and
			//    forth between a sweeping bar and a filled one.
			var firstNumber = states.FindIndex( x => x.Progress.HasValue );
			Assert.IsTrue( firstNumber >= 0, $"no number was ever published:\n{timeline}" );

			foreach( var s in states.Skip( firstNumber ) )
			{
				Assert.IsTrue( s.Progress.HasValue, $"the number disappeared again:\n{timeline}" );
			}

			// 5. And it ends full, so the bar does not stop short of the end.
			Assert.AreEqual( EScriptStatus.Finished, states.Last().Status, timeline );
			Assert.AreEqual( 1.0, states.Last().Progress ?? 0, 0.0001, timeline );
		}

		[TestMethod()]
		public async Task EveryPhaseNamesItself()
		{
			// The lookup is one remote call per node, in sequence - here they are all local, so it
			// costs nothing, but the shape is the same: a phase with no number, then numbers.
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World() } );

			var states = await StatesOfADownload( bed );
			var timeline = Describe( states );

			var phases = states
					.Where( x => x.Status == EScriptStatus.Running && !string.IsNullOrWhiteSpace( x.Text ) )
					.Select( x => x.Text! )
					.Distinct()
					.ToList();

			Assert.IsTrue( phases.Any( p => p.Contains( "Looking up" ) ),
				$"the lookup names itself:\n{timeline}" );
			Assert.IsTrue( phases.Any( p => p.Contains( "Collecting" ) ),
				$"and so does the collection:\n{timeline}" );

			// every phase the operator sees is a sentence about the work, not a bare number
			foreach( var phase in phases )
				Assert.IsTrue( phase.Length > 5, $"'{phase}' says too little:\n{timeline}" );
		}
	}
}
