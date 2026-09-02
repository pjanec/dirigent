using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.Scripts.BuiltIn;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Dirigent.Tests
{
	/// <summary>
	/// What resolving a package costs in remote round trips, and how long the operator waits for them.
	/// </summary>
	/// <remarks>
	/// Every node belonging to another machine is resolved by that machine, over the network. The
	/// cost of a collection is therefore counted in round trips, and a site of forty machines with a
	/// few nodes each has a lot of nodes: asked one at a time, one after another, the lookup is
	/// longer than the download it precedes.
	///
	/// The test bed cannot show this - every machine in it answers on loopback, so nothing is ever
	/// treated as remote and no round trip is ever made. Here the registry is built by hand with a
	/// machine-IP delegate that gives each machine an address of its own, which is what makes the
	/// remote branch run, and the stub standing in for the rest of the site counts what it is asked.
	/// </remarks>
	[TestClass()]
	public class ResolutionBatchingTests
	{
		const string _master = "master";
		static readonly string[] _machines = { "m1", "m2", "m3", "m4", "m5" };
		const int _nodesPerMachine = 8;

		/// <summary>How long a machine takes to answer, whatever it is asked for.</summary>
		static readonly TimeSpan _roundTrip = TimeSpan.FromMilliseconds( 150 );

		#region the rest of the site

		/// <summary>
		/// Every other machine of the site, answering the resolve script and remembering what it was
		/// asked, by whom, and how much of it happened at the same time.
		/// </summary>
		class TheOtherMachines : IDirig
		{
			public string Name => _master;

			readonly object _lock = new object();
			int _inFlight;

			public List<Ask> Asks = new List<Ask>();

			/// <summary>The most calls that were ever in flight together.</summary>
			public int MostAtOnce;

			/// <summary>Ids of nodes that cannot be resolved on the machine they belong to.</summary>
			public HashSet<string> Broken = new HashSet<string>();

			/// <summary>Machines that do not answer at all.</summary>
			public HashSet<string> Unreachable = new HashSet<string>();

			public class Ask
			{
				public string MachineId = string.Empty;
				public List<string> Nodes = new List<string>();
				public bool Batched;
				public bool IncludeContent;
			}

			// the registry does its own resolving; it never asks the control for it
			public Task<VfsNodeDef?> ResolveAsync( VfsNodeDef nodeDef, bool forceUNC, bool includeContent )
				=> throw new NotImplementedException();

			public Task<TResult?> RunScriptAsync<TArgs, TResult>( string clientId, string scriptName,
					string? sourceCode, TArgs? args, string title, out Guid scriptInstance )
			{
				scriptInstance = Guid.NewGuid();

				Assert.AreEqual( ResolveVfsPath._Name, scriptName, "the registry runs the resolve script" );

				var ask = args as ResolveVfsPath.TArgs ?? throw new Exception( $"unexpected args: {args}" );
				var asked = ask.VfsNodes ?? new List<VfsNodeDef>() { ask.VfsNode! };

				lock( _lock )
				{
					Asks.Add( new Ask()
					{
						MachineId = clientId,
						Nodes = asked.Select( n => n.Id ?? string.Empty ).ToList(),
						Batched = ask.VfsNodes is not null,
						IncludeContent = ask.IncludeContent,
					} );

					_inFlight++;
					MostAtOnce = Math.Max( MostAtOnce, _inFlight );
				}

				return Answer<TResult>( clientId, ask, asked );
			}

			async Task<TResult?> Answer<TResult>( string machineId, ResolveVfsPath.TArgs ask,
					List<VfsNodeDef> asked )
			{
				try
				{
					await Task.Delay( _roundTrip );

					if( Unreachable.Contains( machineId ) )
						throw new Exception( $"Machine {machineId} not connected." );

					var result = new ResolveVfsPath.TResult();

					if( ask.VfsNodes is null )
					{
						// one node, and a failure is the whole call failing
						if( Broken.Contains( asked[0].Id ?? string.Empty ) )
							throw new Exception( Why( asked[0] ) );

						result.VfsNode = Resolved( asked[0] );
					}
					else
					{
						result.Nodes = asked.Select( n =>
							Broken.Contains( n.Id ?? string.Empty )
								? new ResolveVfsPath.TNodeResult() { Error = Why( n ) }
								: new ResolveVfsPath.TNodeResult() { VfsNode = Resolved( n ) } ).ToList();
					}

					return (TResult?) (object) result;
				}
				finally
				{
					lock( _lock ) _inFlight--;
				}
			}

			static string Why( VfsNodeDef node ) => $"There is no folder {node.Path} here";

			static VfsNodeDef Resolved( VfsNodeDef node ) => new ResolvedVfsNodeDef()
			{
				Id = node.Id,
				Title = node.Title,
				MachineId = node.MachineId,
				AppId = node.AppId,
				Path = $@"\\{node.MachineId}\share\{node.Id}.log",
			};
		}

		#endregion

		TheOtherMachines _site = null!;
		FileRegistry _reg = null!;
		IDirigAsync _dirig = null!;

		[TestInitialize()]
		public void SetUp()
		{
			_site = new TheOtherMachines();

			_reg = new FileRegistry( _site, _master, @"C:\", MachineIP );
			_reg.SetMachines( AllMachines() );

			// no dispatching to a Tick - the calls are made straight from this thread's task
			_dirig = new SynchronousIDirig( _site, null! ) { ShouldWaitForSync = false };
		}

		static string? MachineIP( string machineId )
		{
			if( machineId == _master ) return "10.0.0.1";

			var idx = Array.IndexOf( _machines, machineId );
			return idx < 0 ? null : $"10.0.0.{idx + 2}";
		}

		static List<MachineDef> AllMachines()
		{
			var defs = new List<MachineDef>() { new MachineDef() { Id = _master, IP = MachineIP( _master ) } };
			foreach( var machine in _machines )
				defs.Add( new MachineDef() { Id = machine, IP = MachineIP( machine ) } );
			return defs;
		}

		static string NodeId( string machine, int i ) => $"{machine}-log{i:00}";

		static FolderDef Node( string machine, int i ) => new FolderDef()
		{
			Guid = Guid.NewGuid(),
			Id = NodeId( machine, i ),
			Title = $"Log {i:00} of {machine}",
			MachineId = machine,
			Path = $@"C:\logs\{i:00}",
		};

		/// <summary>
		/// A package the shape a real one has: the same handful of logs on every machine, listed the
		/// way a configuration lists them - by log, not by machine, so that grouping by machine has
		/// to actually group rather than just notice a run of neighbours.
		/// </summary>
		static FilePackageDef Package()
		{
			var pkg = new FilePackageDef()
			{
				Guid = Guid.NewGuid(),
				Id = "logs",
				Title = "All the logs",
				IsContainer = true,
			};

			for( int i = 0; i < _nodesPerMachine; i++ )
				foreach( var machine in _machines )
					pkg.Children.Add( Node( machine, i ) );

			return pkg;
		}

		static int TotalNodes => _machines.Length * _nodesPerMachine;

		async Task<(VfsNodeDef Tree, TimeSpan Took)> Resolve( VfsNodeDef def )
		{
			var clock = Stopwatch.StartNew();
			var tree = await _reg.ResolveAsync( _dirig, def, false, true, null );
			clock.Stop();

			Assert.IsNotNull( tree );
			return (tree!, clock.Elapsed);
		}

		static List<string> IdsOf( VfsNodeDef tree ) => tree.Children.Select( c => c.Id ?? "" ).ToList();

		string Report() => string.Join( "\n",
			_site.Asks.Select( a => $"  {a.MachineId}: {a.Nodes.Count} node(s) - {string.Join( ", ", a.Nodes )}" ) );

		[TestMethod()]
		public async Task OneRoundTripPerMachineTest()
		{
			var (tree, _) = await Resolve( Package() );

			Assert.AreEqual( TotalNodes, tree.Children.Count, "every node was resolved" );

			Assert.AreEqual( _machines.Length, _site.Asks.Count,
				$"a machine is asked once, for everything of its own that the package wants, rather "
				+ $"than once per node ({TotalNodes} nodes over {_machines.Length} machines):\n{Report()}" );

			CollectionAssert.AreEquivalent( _machines, _site.Asks.Select( a => a.MachineId ).ToList(),
				$"and every machine is asked:\n{Report()}" );

			// nothing was dropped or asked of the wrong machine on the way
			foreach( var ask in _site.Asks )
			{
				CollectionAssert.AreEquivalent(
					Enumerable.Range( 0, _nodesPerMachine ).Select( i => NodeId( ask.MachineId, i ) ).ToList(),
					ask.Nodes,
					$"{ask.MachineId} was asked for exactly its own nodes:\n{Report()}" );
			}
		}

		[TestMethod()]
		public async Task TheMachinesAreAskedAllAtOnceTest()
		{
			var (_, took) = await Resolve( Package() );

			Assert.AreEqual( _machines.Length, _site.MostAtOnce,
				$"every machine is asked at the same time - the site answers in parallel, and waiting "
				+ $"for one machine before asking the next is the whole cost:\n{Report()}" );

			// what the operator actually feels: one machine's answer, not the sum of forty
			Assert.IsTrue( took < TimeSpan.FromMilliseconds( _roundTrip.TotalMilliseconds * 3 ),
				$"looking up {TotalNodes} nodes on {_machines.Length} machines took {took.TotalMilliseconds:0} ms, "
				+ $"which is more than a couple of round trips ({_roundTrip.TotalMilliseconds:0} ms each)" );
		}

		[TestMethod()]
		public async Task AContainerIsAlwaysAskedWhatIsInsideItsChildrenTest()
		{
			// "do not look inside" is about the node asked for, not about its members: a package
			// resolved without content is still a list of what its members hold. Batching puts many
			// nodes behind one call and the flag belongs to the call, so it is the one thing that
			// could have been quietly lost on the way.
			var tree = await _reg.ResolveAsync( _dirig, Package(), false, includeContent: false, null );

			Assert.IsNotNull( tree );
			Assert.AreEqual( TotalNodes, tree!.Children.Count );

			foreach( var ask in _site.Asks )
				Assert.IsTrue( ask.IncludeContent, $"{ask.MachineId} was asked not to look inside" );
		}

		[TestMethod()]
		public async Task TheTreeIsTheSameWhoeverAnsweredFirstTest()
		{
			var package = Package();
			var (tree, _) = await Resolve( package );

			// asking the machines together must not shuffle the package: the archive is laid out in
			// this order and an operator reads it expecting the order of the configuration
			CollectionAssert.AreEqual( IdsOf( package ), IdsOf( tree ),
				"the resolved tree keeps the order the package was declared in" );

			foreach( var child in tree.Children )
			{
				Assert.AreEqual( $@"\\{child.MachineId}\share\{child.Id}.log", child.Path,
					"and every node got its own machine's answer" );
			}
		}

		[TestMethod()]
		public async Task OneUnresolvableNodeCostsOnlyItselfTest()
		{
			// the missing-folder guard, now that a machine answers about twenty nodes at a time: one
			// bad node in the batch must not cost the batch
			_site.Broken.Add( NodeId( "m3", 4 ) );

			var (tree, _) = await Resolve( Package() );

			Assert.AreEqual( TotalNodes - 1, tree.Children.Count, "only the one node is missing" );
			Assert.IsFalse( IdsOf( tree ).Contains( NodeId( "m3", 4 ) ) );

			Assert.IsNotNull( tree.Notes, "and the package says what it is missing" );
			Assert.AreEqual( 1, tree.Notes!.Count );
			StringAssert.Contains( tree.Notes[0], "Log 04 of m3" );
			StringAssert.Contains( tree.Notes[0], "on m3" );
			StringAssert.Contains( tree.Notes[0], "There is no folder" );

			// the rest of m3 came anyway
			Assert.AreEqual( _nodesPerMachine - 1, IdsOf( tree ).Count( id => id.StartsWith( "m3-" ) ) );
		}

		[TestMethod()]
		public async Task AMachineThatDoesNotAnswerCostsOnlyItsOwnNodesTest()
		{
			// batching puts many nodes behind one call, so the call failing must read as each of
			// those nodes failing - not as the package failing
			_site.Unreachable.Add( "m2" );

			var (tree, _) = await Resolve( Package() );

			Assert.AreEqual( TotalNodes - _nodesPerMachine, tree.Children.Count );
			Assert.IsFalse( IdsOf( tree ).Any( id => id.StartsWith( "m2-" ) ) );

			Assert.IsNotNull( tree.Notes );
			Assert.AreEqual( _nodesPerMachine, tree.Notes!.Count,
				"one note per thing the operator asked for and did not get:\n"
				+ string.Join( "\n", tree.Notes! ) );

			foreach( var note in tree.Notes! )
				StringAssert.Contains( note, "not connected" );
		}

		[TestMethod()]
		public async Task ANodeAskedForOnItsOwnStillFailsOutLoudTest()
		{
			// there is nothing else to deliver, so the caller hears about it
			_site.Broken.Add( NodeId( "m1", 0 ) );

			var ex = await Assert.ThrowsExceptionAsync<Exception>(
				() => _reg.ResolveAsync( _dirig, Node( "m1", 0 ), false, true, null ) );

			StringAssert.Contains( ex.Message, "There is no folder" );
		}

		[TestMethod()]
		public async Task AReferenceReachingEveryMachineIsStillOneTripPerMachineTest()
		{
			// how a real package is written: a <FileRef> per log, matching a node on every machine of
			// the site. Resolved a reference at a time, that is a whole sweep of the site per line of
			// the configuration.
			var nodes = new List<VfsNodeDef>();
			for( int i = 0; i < _nodesPerMachine; i++ )
				foreach( var machine in _machines )
					nodes.Add( Node( machine, i ) );

			_reg.SetVfsNodes( nodes );

			var pkg = new FilePackageDef()
			{
				Guid = Guid.NewGuid(),
				Id = "logs",
				Title = "All the logs",
				IsContainer = true,
			};

			for( int i = 0; i < _nodesPerMachine; i++ )
			{
				foreach( var machine in _machines )
				{
					pkg.Children.Add( new FileRef()
					{
						Guid = Guid.NewGuid(),
						Id = NodeId( machine, i ),
						MachineId = "*",
						AppId = "*",
					} );
				}
			}

			var (tree, took) = await Resolve( pkg );

			Assert.AreEqual( TotalNodes, tree.Children.Count, "every reference found its node" );

			Assert.AreEqual( _machines.Length, _site.Asks.Count,
				$"a reference is looked up here, in the registry; only the nodes it finds go over the "
				+ $"network, and they travel with everything else bound for the same machine:\n{Report()}" );

			Assert.IsTrue( took < TimeSpan.FromMilliseconds( _roundTrip.TotalMilliseconds * 3 ),
				$"{TotalNodes} references took {took.TotalMilliseconds:0} ms" );
		}
	}
}
