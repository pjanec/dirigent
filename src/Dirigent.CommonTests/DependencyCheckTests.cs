using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Dirigent.Tests
{
	/// <summary>
	/// What the config reader accepts and refuses about `Dependencies`.
	/// </summary>
	/// <remarks>
	/// Reported from a live site as *"a two-element `A;B` splitting and then reporting Circular
	/// dependency B found for a B depending on nothing"*, unreproduced there because reproducing it
	/// meant editing a config on a running station.
	///
	/// It reproduces here, and it is not about the splitting: `CheckDependenciesCircular` walks the
	/// dependency graph with one `depsUsed` dictionary shared by every branch of the walk, and never
	/// takes an entry out when a branch finishes. So it detects a node **visited twice**, which is
	/// not the same thing as a cycle - any diamond trips it. A plan where two applications wait for
	/// the same third one is refused, and the master will not start.
	/// </remarks>
	[TestClass()]
	public class DependencyCheckTests
	{
		/// <summary>A one-plan config whose apps are the given (id, dependencies) pairs.</summary>
		static string ConfigWith( params (string Id, string Deps)[] apps )
		{
			var xml = new StringBuilder();
			xml.AppendLine( "<Shared>" );
			xml.AppendLine( "  <Plan Name='p'>" );

			foreach( var (id, deps) in apps )
			{
				xml.Append( $"    <App AppIdTuple='{id}' ExeFullPath='c:\\windows\\notepad.exe'" );
				if( !string.IsNullOrEmpty( deps ) ) xml.Append( $" Dependencies='{deps}'" );
				xml.AppendLine( "/>" );
			}

			xml.AppendLine( "  </Plan>" );
			xml.AppendLine( "</Shared>" );
			return xml.ToString();
		}

		static SharedConfig Load( string xml )
		{
			using var reader = new StringReader( xml );
			return new SharedConfigReader( reader ).Config;
		}

		[TestMethod()]
		public void APlainChainLoadsTest()
		{
			// c -> b -> a, nothing shared, nothing circular
			var cfg = Load( ConfigWith( ("m1.a", ""), ("m1.b", "m1.a"), ("m1.c", "m1.b") ) );

			Assert.AreEqual( 1, cfg.Plans.Count );
			Assert.AreEqual( 3, cfg.Plans[0].AppDefs.Count );
		}

		[TestMethod()]
		public void TwoApplicationsMayWaitForTheSameThirdOneTest()
		{
			// The diamond: d is depended on twice by way of b and c, and there is no cycle anywhere.
			// This is what a real plan looks like - a database everything waits for, a licence
			// server, a recorder - and it must load.
			var cfg = Load( ConfigWith(
					("m1.d", ""),
					("m1.b", "m1.d"),
					("m1.c", "m1.d"),
					("m1.a", "m1.b;m1.c") ) );

			Assert.AreEqual( 4, cfg.Plans[0].AppDefs.Count );
		}

		[TestMethod()]
		public void DependingOnBothAThingAndWhatItWaitsForIsNotCircularTest()
		{
			// The exact shape reported from the site: a two-element list, and the second element is
			// also reached through the first. "Start c once both a and b are up, and b needs a" is
			// an ordinary thing to write, and a depends on nothing at all.
			var cfg = Load( ConfigWith(
					("m1.a", ""),
					("m1.b", "m1.a"),
					("m1.c", "m1.a;m1.b") ) );

			Assert.AreEqual( 3, cfg.Plans[0].AppDefs.Count );
		}

		[TestMethod()]
		public void ARealCycleIsStillRefusedTest()
		{
			// what the check is for, and what must keep working
			var ex = Assert.ThrowsException<CircularDependencyException>(
					() => Load( ConfigWith( ("m1.a", "m1.b"), ("m1.b", "m1.a") ) ) );

			StringAssert.Contains( ex.Message, "Circular dependency" );
		}

		[TestMethod()]
		public void ALongerCycleIsStillRefusedTest()
		{
			var ex = Assert.ThrowsException<CircularDependencyException>(
					() => Load( ConfigWith( ("m1.a", "m1.c"), ("m1.b", "m1.a"), ("m1.c", "m1.b") ) ) );

			StringAssert.Contains( ex.Message, "Circular dependency" );
		}

		[TestMethod()]
		public void AnAppDependingOnItselfIsRefusedTest()
		{
			Assert.ThrowsException<CircularDependencyException>(
					() => Load( ConfigWith( ("m1.a", "m1.a") ) ) );
		}

		[TestMethod()]
		public void ADependencyOnSomethingUndefinedIsRefusedTest()
		{
			var ex = Assert.ThrowsException<UnknownDependencyException>(
					() => Load( ConfigWith( ("m1.a", "m1.nosuchthing") ) ) );

			StringAssert.Contains( ex.Message, "not found" );
		}

		[TestMethod()]
		public void ABareDependencyNameMeansTheSameMachineTest()
		{
			// how AppLaunchPlanner reads it at run time, so the reader has to agree - and a cycle
			// written with bare names has to be caught as well
			var cfg = Load( ConfigWith( ("m1.a", ""), ("m1.b", "a") ) );
			Assert.AreEqual( 2, cfg.Plans[0].AppDefs.Count );

			Assert.ThrowsException<CircularDependencyException>(
					() => Load( ConfigWith( ("m1.a", "b"), ("m1.b", "a") ) ) );
		}

		/// <summary>
		/// Whether the graph really has a cycle, worked out a different way: peel off applications
		/// that depend on nothing still standing, and see whether any are left.
		/// </summary>
		/// <remarks>
		/// Kahn's algorithm, deliberately unlike the reader's depth-first walk, so that the two
		/// agreeing means something.
		/// </remarks>
		static bool HasCycle( (string Id, string Deps)[] apps )
		{
			var deps = apps.ToDictionary(
					a => a.Id,
					a => ( a.Deps ?? "" ).Split( ';', StringSplitOptions.RemoveEmptyEntries )
							.Select( d => d.Trim() )
							.Where( d => d.Length > 0 )
							.ToHashSet() );

			var standing = deps.Keys.ToHashSet();

			while( true )
			{
				// anything whose dependencies are all gone can be removed
				var free = standing.Where( id => !deps[id].Any( d => standing.Contains( d ) ) ).ToList();
				if( free.Count == 0 ) break;
				foreach( var id in free ) standing.Remove( id );
			}

			return standing.Count > 0;
		}

		[TestMethod()]
		public void TheCheckRefusesExactlyTheGraphsThatHaveACycleTest()
		{
			// The property that matters for existing configurations: a working config is one that
			// loads, so it is acyclic - and every acyclic graph has to keep loading. The other
			// direction says the check was not simply weakened into uselessness.
			//
			// Random graphs rather than hand-written ones, because the case that broke was one
			// nobody thought to write: a node reachable by two routes at once.
			var random = new Random( 20260908 ); // fixed, so a failure can be re-run
			int cyclic = 0, acyclic = 0;

			for( int round = 0; round < 400; round++ )
			{
				int n = 2 + random.Next( 6 );
				var ids = Enumerable.Range( 0, n ).Select( i => $"m1.a{i}" ).ToArray();

				var apps = new (string Id, string Deps)[n];
				for( int i = 0; i < n; i++ )
				{
					// any edge at all, including backwards ones - that is what makes cycles happen
					var picked = ids.Where( x => x != ids[i] && random.Next( 100 ) < 30 ).ToList();
					apps[i] = ( ids[i], string.Join( ";", picked ) );
				}

				var expected = HasCycle( apps );
				var xml = ConfigWith( apps );

				CircularDependencyException? thrown = null;
				try { Load( xml ); }
				catch( CircularDependencyException e ) { thrown = e; }

				if( expected ) cyclic++; else acyclic++;

				Assert.AreEqual( expected, thrown is not null,
					expected
						? $"a real cycle was accepted:\n{xml}"
						: $"an acyclic plan was refused as circular - this is the shape that breaks a "
							+ $"working config:\n{thrown?.Message}\n{xml}" );
			}

			// the run has to have covered both answers, or it proves nothing
			Assert.IsTrue( cyclic > 20, $"only {cyclic} cyclic graphs in the sample" );
			Assert.IsTrue( acyclic > 20, $"only {acyclic} acyclic graphs in the sample" );
		}

		[TestMethod()]
		public void SeveralDependenciesAreSeparatedBySemicolonsTest()
		{
			// the separator, and that a trailing one is tolerated - the sample config has written it
			// that way for years
			var cfg = Load( ConfigWith(
					("m1.a", ""), ("m1.b", ""), ("m1.c", "m1.a;m1.b;") ) );

			var c = cfg.Plans[0].AppDefs.First( x => x.Id.AppId == "c" );
			CollectionAssert.AreEquivalent( new[] { "m1.a", "m1.b" }, c.Dependencies!.ToArray() );
		}
	}
}
