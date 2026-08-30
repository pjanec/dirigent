using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DownloadZipped = Dirigent.Scripts.BuiltIn.DownloadZipped;
using DownloadZippedSlave = Dirigent.Scripts.BuiltIn.DownloadZippedSlave;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// What a long operation says about itself while it runs, and what it leaves behind when it is
	/// stopped half way.
	/// </summary>
	[TestClass()]
	public class ScriptProgressTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 60 );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		/// <summary>
		/// A world holding enough incompressible data that collecting it takes long enough to watch -
		/// and to interrupt. Compressible filler would be swallowed at gigabytes per second and there
		/// would be nothing to see.
		/// </summary>
		static Scenario BulkyWorld( int fileCount = 4, int fileSizeBytes = 32 * 1024 * 1024 )
		{
			var scenario = Scenario.OneMachine()
				.App( "m1.camera", a => a
					.LongRunning()
					.WithFolderNode( "logs", "{applogs}", mask: "*.log" ) );

			for( int i = 0; i < fileCount; i++ )
				scenario.Seed( "m1.camera", $"bulk{i}.log", ageDays: 0, sizeBytes: fileSizeBytes, incompressible: true );

			return scenario;
		}

		static async Task<(Guid Instance, Task<DownloadZipped.TResult?> Completion)> StartDownload(
				TestBed.TestBed bed, string nodeId )
		{
			var node = await bed.Operator.GetVfsNodeAsync( nodeId );
			var resolved = await bed.Operator.ResolveAsync( node );

			return await bed.Operator.StartScriptAsync<DownloadZipped.TArgs, DownloadZipped.TResult>(
				DownloadZipped._Name, new DownloadZipped.TArgs() { VfsNode = resolved } );
		}

		[TestMethod()]
		public async Task ADownloadReportsItsProgressAndEndsAtOne()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = BulkyWorld() } );

			var (instance, completion) = await StartDownload( bed, "logs" );

			// everything the operation said about itself along the way
			var seen = new List<double>();

			await bed.WaitUntilAsync(
				async () =>
				{
					var state = await bed.Operator.GetScriptStateAsync( instance );
					if( state?.Progress is double p && ( seen.Count == 0 || seen[^1] != p ) ) seen.Add( p );
					return completion.IsCompleted;
				},
				Timeout, "the download finishes" );

			await completion;

			var final = await bed.Operator.GetScriptStateAsync( instance );
			Assert.AreEqual( EScriptStatus.Finished, final?.Status );
			Assert.AreEqual( 1.0, final?.Progress, "a finished operation is a whole one" );

			Assert.IsTrue( seen.Count > 0, "the download should have said something about its progress" );
			Assert.IsTrue( seen.All( p => p >= 0.0 && p <= 1.0 ), $"progress out of range: {Describe( seen )}" );
			CollectionAssert.AreEqual( seen.OrderBy( p => p ).ToList(), seen,
				$"progress should never go backwards: {Describe( seen )}" );
		}

		[TestMethod()]
		public async Task EachMachineReportsHowMuchItHasCollected()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = BulkyWorld() } );

			var (_, completion) = await StartDownload( bed, "logs" );

			// The per-machine numbers only exist while the machine works - once it is done, its state
			// carries the result instead. This is what lets the master weigh the machines against each
			// other rather than treat a 60 GB one like a 2 MB one, so it is worth catching in flight.
			var reports = new List<DownloadZippedSlave.TProgress>();

			await bed.WaitUntilAsync(
				async () =>
				{
					foreach( var (_, state) in await bed.Operator.GetAllScriptsStateAsync() )
					{
						var progress = Tools.Deserialize<DownloadZippedSlave.TProgress>( state.Data );
						if( progress is not null && progress.BytesTotal > 0 ) reports.Add( progress );
					}
					return completion.IsCompleted;
				},
				Timeout, "the download finishes" );

			await completion;

			Assert.IsTrue( reports.Count > 0, "the machine holding the files should have reported its progress" );

			var announced = reports[0].BytesTotal;
			Assert.IsTrue( announced >= 4 * 32 * 1024 * 1024L,
				$"the announced total should cover the seeded files, was {announced}" );
			Assert.IsTrue( reports.All( r => r.BytesTotal == announced ),
				"the total should not move about while the collection runs" );
			Assert.IsTrue( reports.All( r => r.BytesDone <= r.BytesTotal ),
				"a machine should never claim to have collected more than it has" );
		}

		[TestMethod()]
		public async Task CancellingADownloadStopsItAndLeavesNothingBehind()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = BulkyWorld() } );

			var (instance, completion) = await StartDownload( bed, "logs" );

			// wait until the machines are really collecting, so the cancel has something to stop
			await bed.WaitUntilAsync(
				async () =>
				{
					var state = await bed.Operator.GetScriptStateAsync( instance );
					return ( state?.Progress ?? 0 ) > 0 || completion.IsCompleted;
				},
				Timeout, "the collection has started" );

			Assert.IsFalse( completion.IsCompleted,
				"the world is too small to interrupt - seed more, or larger, files" );

			await bed.Operator.KillScriptAsync( instance );

			await bed.WaitUntilAsync(
				async () => !( ( await bed.Operator.GetScriptStateAsync( instance ) )?.IsAlive ?? false ),
				Timeout, "the download stops" );

			var final = await bed.Operator.GetScriptStateAsync( instance );
			Assert.AreEqual( EScriptStatus.Cancelled, final?.Status,
				"a cancelled download ends as cancelled, not as a failure and not as a success" );

			// Nothing half made may be left where the user would look, and nothing may turn up after
			// the fact either - a machine that kept compressing to the end would deliver its archive a
			// moment later. That is a negative property, so it takes an observation window rather than
			// a condition to wait for: a second is longer than this collection needs from here, and
			// the check can only fail if something really does appear.
			var watch = Stopwatch.StartNew();
			while( watch.Elapsed < TimeSpan.FromSeconds( 1 ) )
			{
				var leftovers = Directory.GetFileSystemEntries( bed.DownloadFolder );
				Assert.AreEqual( 0, leftovers.Length,
					"a cancelled download leaves no archive, no staging folder and no partial file; found: "
					+ string.Join( ", ", leftovers.Select( Path.GetFileName ) ) );

				await Task.Delay( 100 );
			}
		}

		static string Describe( List<double> progress )
			=> string.Join( ", ", progress.Select( p => p.ToString( "0.###" ) ) );
	}
}
