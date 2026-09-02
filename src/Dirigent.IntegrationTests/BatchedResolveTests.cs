using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// A machine answering about many of its nodes in a single call.
	/// </summary>
	/// <remarks>
	/// This is the far end of the batching: the master groups everything bound for one machine into
	/// one `ResolveVfsPath` call, and this is the agent actually running it, over a real connection,
	/// with real folders on disk.
	///
	/// It has to be asked of an agent explicitly. In a tier-1 bed every machine answers on loopback,
	/// so the master treats them all as itself and never sends the script anywhere - the same blind
	/// spot that hid the missing share table. What the master does with the answer is pinned at tier
	/// 0 instead, in `ResolutionBatchingTests`, where a machine can be given an address of its own.
	/// </remarks>
	[TestClass()]
	public class BatchedResolveTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 30 );

		string _root = string.Empty;

		[TestInitialize()]
		public void SetUp()
		{
			Diagnostics.ClearLog();

			_root = Path.Combine( Path.GetTempPath(), "DirigentBatchedResolve_" + Guid.NewGuid().ToString( "N" ) );
			Directory.CreateDirectory( _root );
		}

		[TestCleanup()]
		public void TearDown()
		{
			try { Directory.Delete( _root, true ); } catch {}
		}

		string MakeFolder( string name )
		{
			var path = Path.Combine( _root, name );
			Directory.CreateDirectory( path );
			File.WriteAllText( Path.Combine( path, "app.log" ), "hello" );
			return path;
		}

		static Task<TestBed.TestBed> StartBed()
			=> TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = Scenario.TwoMachines() } );

		async Task<Scripts.BuiltIn.ResolveVfsPath.TResult?> AskAgent( TestBed.TestBed bed,
				string machineName, params VfsNodeDef[] nodes )
		{
			return await bed.Operator.RunScriptAsync<
					Scripts.BuiltIn.ResolveVfsPath.TArgs, Scripts.BuiltIn.ResolveVfsPath.TResult>(
				Scripts.BuiltIn.ResolveVfsPath._Name,
				new Scripts.BuiltIn.ResolveVfsPath.TArgs()
				{
					VfsNodes = nodes.ToList(),
					IncludeContent = true, // as a download asks for it - the folders get scanned
				},
				hostId: bed.MachineIds[machineName],
				timeout: Timeout );
		}

		FolderDef Folder( TestBed.TestBed bed, string machineName, string path )
			=> new FolderDef()
			{
				Guid = Guid.NewGuid(),
				Id = Path.GetFileName( path ),
				Title = Path.GetFileName( path ),
				MachineId = bed.MachineIds[machineName],
				Path = path,
				Mask = "*.log",
			};

		[TestMethod()]
		public async Task AnAgentAnswersAboutEveryNodeItWasAskedAbout()
		{
			using var bed = await StartBed();

			var folders = new[] { "one", "two", "three" }.Select( MakeFolder ).ToArray();

			var result = await AskAgent( bed, "m1",
				folders.Select( f => (VfsNodeDef) Folder( bed, "m1", f ) ).ToArray() );

			Assert.IsNotNull( result, "the agent answered nothing at all" );
			Assert.IsNotNull( result!.Nodes, "a batched question is answered with a list, one entry per node" );
			Assert.AreEqual( folders.Length, result.Nodes!.Count );

			// in the order asked, so that the caller can put each answer back where it belongs
			for( int i = 0; i < folders.Length; i++ )
			{
				Assert.IsNull( result.Nodes[i].Error, $"node {i}: {result.Nodes[i].Error}" );
				Assert.IsNotNull( result.Nodes[i].VfsNode, $"node {i} was not resolved" );
				Assert.AreEqual( Path.GetFileName( folders[i] ), result.Nodes[i].VfsNode!.Title );

				// really resolved, not just echoed back - the folder was scanned
				Assert.AreEqual( 1, result.Nodes[i].VfsNode!.Children.Count );
				Assert.AreEqual( "app.log", result.Nodes[i].VfsNode!.Children[0].Title );
			}
		}

		[TestMethod()]
		public async Task OneBadNodeDoesNotCostTheOthersTheirAnswerTest()
		{
			// twenty nodes travelling together to save a round trip are still twenty separate things
			// the operator asked for; a folder that has never existed on this machine must not take
			// the other nineteen down with it
			using var bed = await StartBed();

			var good = MakeFolder( "good" );
			var missing = Path.Combine( _root, "neverExisted" );

			var result = await AskAgent( bed, "m1",
				Folder( bed, "m1", good ),
				Folder( bed, "m1", missing ),
				Folder( bed, "m1", good ) );

			Assert.IsNotNull( result?.Nodes );
			Assert.AreEqual( 3, result!.Nodes!.Count );

			Assert.IsNotNull( result.Nodes[0].VfsNode, "the node before the bad one was resolved" );
			Assert.IsNotNull( result.Nodes[2].VfsNode, "and so was the node after it" );

			Assert.IsNull( result.Nodes[1].VfsNode, "the bad node has no answer..." );
			Assert.IsFalse( string.IsNullOrEmpty( result.Nodes[1].Error ), "...but it says why" );
			StringAssert.Contains( result.Nodes[1].Error!, "neverExisted",
				$"and the reason names what was missing; got '{result.Nodes[1].Error}'" );
		}
	}
}
