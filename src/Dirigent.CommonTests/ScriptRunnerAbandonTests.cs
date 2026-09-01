using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Dirigent.Tests
{
	/// <summary>
	/// A script run that has been replaced must stop talking about itself.
	/// </summary>
	/// <remarks>
	/// Starting a singleton script under an id that is already running replaces the run: the old one
	/// is cancelled and a new one takes the id. The cancellation completes on the old run's own
	/// thread, whenever the script next comes up for air - which can be after the replacement has
	/// already published that it is running. Every observer is keyed by the instance id, so the dead
	/// run's `Cancelled` would land on the live one, and a GUI would show a script as cancelled while
	/// it is working.
	///
	/// Pinned here rather than at tier 1 because the window is a timing accident: the same tier-1
	/// test passes on an idle machine and fails inside a full suite run, which is where this was
	/// first seen.
	/// </remarks>
	[TestClass()]
	public class ScriptRunnerAbandonTests
	{
		class CapturingDirig : IDirig
		{
			public readonly List<Net.ScriptStateMessage> Sent = new();

			public string Name => "master";

			public void Send( Net.Message msg )
			{
				if( msg is Net.ScriptStateMessage m ) Sent.Add( m );
			}

			public Task<TResult?> RunScriptAsync<TArgs, TResult>( string clientId, string scriptName,
					string? sourceCode, TArgs? args, string title, out Guid scriptInstance )
				=> throw new NotImplementedException();

			public Task<VfsNodeDef?> ResolveAsync( VfsNodeDef nodeDef, bool forceUNC, bool includeContent )
				=> throw new NotImplementedException();
		}

		static ScriptRunner MakeRunner( CapturingDirig ctrl, Guid instance )
			=> new ScriptRunner( ctrl, instance, new ScriptFactory( AppContext.BaseDirectory ),
								new SynchronousOpProcessor(), AppContext.BaseDirectory );

		[TestMethod()]
		public void AnAbandonedRunSaysNothingMoreTest()
		{
			var ctrl = new CapturingDirig();
			var instance = Guid.NewGuid();
			using var runner = MakeRunner( ctrl, instance );

			runner.SendStatus( new ScriptState( EScriptStatus.Running, "still mine" ) );
			Assert.AreEqual( 1, ctrl.Sent.Count, "a live run reports as it always did" );

			runner.Abandon();

			// what the old run would say when its cancellation finally completes
			runner.SendStatus( new ScriptState( EScriptStatus.Cancelled ) );

			Assert.AreEqual( 1, ctrl.Sent.Count,
				"the instance id belongs to the replacement now; this verdict is not about it" );
		}

		[TestMethod()]
		public void ReplacingAnInstanceAbandonsTheRunItReplacesTest()
		{
			// the wiring: the registry abandons a run before handing its id to a new one
			var ctrl = new CapturingDirig();
			var instance = Guid.NewGuid();

			using var registry = new LocalScriptRegistry( ctrl, new ScriptFactory( AppContext.BaseDirectory ),
					new SynchronousOpProcessor(), AppContext.BaseDirectory );

			registry.Start( instance, "BuiltIns/DemoScript1.cs", null, null, "first", "test" );

			var runner = registry.Scripts[instance].Runner;
			Assert.IsTrue( ctrl.Sent.Count > 0, "the run announced itself" );

			Assert.IsTrue( registry.Remove( instance ), "the instance was there to be replaced" );

			var before = ctrl.Sent.Count;
			runner.SendStatus( new ScriptState( EScriptStatus.Cancelled ) );

			Assert.AreEqual( before, ctrl.Sent.Count,
				"a removed run must not report afterwards - its id is free for a replacement" );
		}
	}
}
