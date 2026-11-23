using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.Net;
using System;
using System.Collections.Generic;

namespace Dirigent.Tests
{
	/// <summary>
	/// What counts as a change worth sending to the feed. The states go to the GUIs, and from the agents to the master, only when they
	/// differ from what was last sent; these tests are what decides "differ".
	/// </summary>
	[TestClass()]
	public class FeedComparerTests
	{
		static AppState Running()
		{
			var s = new AppState() { PlanName = "plan", PID = 100, CPU = 10f, GPU = 5f, Memory = 200f, RestartsRemaining = 3 };
			s.Started = true;
			s.Running = true;
			s.Initialized = true;
			s.StartedAt = new DateTime( 2026, 11, 23, 10, 0, 0, DateTimeKind.Utc );
			return s;
		}

		static bool Differs( Action<AppState> change )
		{
			var sent = Running();
			var now = sent.Clone();
			change( now );
			return now.DiffersForFeed( sent );
		}

		[TestMethod]
		public void AnUnchangedAppDoesNotDiffer()
		{
			var sent = Running();
			Assert.IsFalse( sent.Clone().DiffersForFeed( sent ) );
		}

		[TestMethod]
		public void NothingSentYetDiffers()
		{
			Assert.IsTrue( Running().DiffersForFeed( null ) );
		}

		[TestMethod]
		public void TheTimeOfTheLastChangeIsNotAChange()
		{
			// it is renewed by every tick of the agent, and by the master for every heartbeat
			Assert.IsFalse( Differs( s => s.LastChange = s.LastChange.AddMinutes( 5 ) ) );
		}

		[TestMethod]
		public void EveryStatusFieldIsAChange()
		{
			Assert.IsTrue( Differs( s => s.Running = false ), "Running" );
			Assert.IsTrue( Differs( s => s.Started = false ), "Started" );
			Assert.IsTrue( Differs( s => s.Initialized = false ), "Initialized" );
			Assert.IsTrue( Differs( s => s.Killed = true ), "Killed" );
			Assert.IsTrue( Differs( s => s.PlanApplied = true ), "PlanApplied" );
			Assert.IsTrue( Differs( s => s.ExitCode = 3 ), "ExitCode" );
			Assert.IsTrue( Differs( s => s.PID = 101 ), "PID" );
			Assert.IsTrue( Differs( s => s.PlanName = "other" ), "PlanName" );
			Assert.IsTrue( Differs( s => s.RestartsRemaining = 2 ), "RestartsRemaining" );
			Assert.IsTrue( Differs( s => s.StartedAt = s.StartedAt!.Value.AddSeconds( 1 ) ), "StartedAt" );
			Assert.IsTrue( Differs( s => s.EndedAt = DateTime.UtcNow ), "EndedAt" );
			Assert.IsTrue( Differs( s => s.Offline = true ), "Offline" );
		}

		[TestMethod]
		public void CpuAndGpuCountWhenTheyMoveByAPercentagePoint()
		{
			Assert.IsFalse( Differs( s => s.CPU += 0.9f ), "CPU below the threshold" );
			Assert.IsTrue( Differs( s => s.CPU += 1.0f ), "CPU at the threshold" );
			Assert.IsTrue( Differs( s => s.CPU -= 4f ), "CPU down" );
			Assert.IsFalse( Differs( s => s.GPU -= 0.5f ), "GPU below the threshold" );
			Assert.IsTrue( Differs( s => s.GPU += 2f ), "GPU" );
		}

		[TestMethod]
		public void MemoryCountsWhenItMovesByTenMegabytes()
		{
			Assert.IsFalse( Differs( s => s.Memory += 9.5f ), "below the threshold" );
			Assert.IsTrue( Differs( s => s.Memory += 10f ), "at the threshold" );
			Assert.IsTrue( Differs( s => s.Memory -= 50f ), "down" );
		}

		[TestMethod]
		public void ASlowDriftAddsUpAgainstWhatWasSent()
		{
			// the baseline is what was sent, not the previous reading: five steps of 0.3 are 1.5
			var sent = Running();
			var now = sent.Clone();
			for( int i = 0; i < 3; i++ )
			{
				now.CPU += 0.3f;
				Assert.IsFalse( now.DiffersForFeed( sent ), $"step {i}" );
			}
			now.CPU += 0.3f;
			now.CPU += 0.3f;
			Assert.IsTrue( now.DiffersForFeed( sent ) );
		}

		// ---- plans

		static PlanState Plan()
		{
			var p = new PlanState() { Running = true, TimeStarted = new DateTime( 2026, 11, 23, 10, 0, 0, DateTimeKind.Utc ), PlanAppStates = new() };
			p.PlanAppStates[new AppIdTuple( "m1", "a" )] = new PlanAppState() { PlanApplied = true };
			p.PlanAppStates[new AppIdTuple( "m1", "b" )] = new PlanAppState();
			return p;
		}

		static bool PlanDiffers( Action<PlanState> change )
		{
			var plan = Plan();
			var sent = plan.CloneDeep();
			change( plan );
			return plan.DiffersForFeed( sent );
		}

		[TestMethod]
		public void AnUnchangedPlanDoesNotDiffer()
		{
			var plan = Plan();
			Assert.IsFalse( plan.DiffersForFeed( plan.CloneDeep() ) );
			Assert.IsTrue( plan.DiffersForFeed( null ) );
		}

		[TestMethod]
		public void EverythingAboutAPlanIsAChange()
		{
			Assert.IsTrue( PlanDiffers( p => p.Running = false ), "Running" );
			Assert.IsTrue( PlanDiffers( p => p.Killing = true ), "Killing" );
			Assert.IsTrue( PlanDiffers( p => p.OpStatus = PlanState.EOpStatus.Success ), "OpStatus" );
			Assert.IsTrue( PlanDiffers( p => p.TimeStarted = p.TimeStarted.AddSeconds( 1 ) ), "TimeStarted" );
			Assert.IsTrue( PlanDiffers( p => p.Phase = "Marking files" ), "Phase" );
			Assert.IsTrue( PlanDiffers( p => p.MarkStarted = DateTime.UtcNow ), "MarkStarted" );
			Assert.IsTrue( PlanDiffers( p => p.LastCheckpoint = DateTime.UtcNow ), "LastCheckpoint" );
			Assert.IsTrue( PlanDiffers( p => p.MarkResult = new PlanMarkResult() { Marked = 1 } ), "MarkResult" );
		}

		[TestMethod]
		public void TheStateOfAnAppOfAPlanIsAChange()
		{
			// the master's PlanState.Equals leaves the apps out; a GUI shows which ones the plan has applied
			Assert.IsTrue( PlanDiffers( p => p.PlanAppStates[new AppIdTuple( "m1", "b" )].PlanApplied = true ), "an app applied" );
			Assert.IsTrue( PlanDiffers( p => p.PlanAppStates.Remove( new AppIdTuple( "m1", "a" ) ) ), "an app gone" );
			Assert.IsTrue( PlanDiffers( p => p.PlanAppStates[new AppIdTuple( "m2", "c" )] = new PlanAppState() ), "an app added" );
		}

		[TestMethod]
		public void ADeepCopyOfAPlanDoesNotFollowTheOriginal()
		{
			var plan = Plan();
			var copy = plan.CloneDeep();
			plan.PlanAppStates[new AppIdTuple( "m1", "b" )].PlanApplied = true;
			Assert.IsFalse( copy.PlanAppStates[new AppIdTuple( "m1", "b" )].PlanApplied );
		}

		// ---- clients

		static ClientState Client()
		{
			var ident = new ClientIdent( "m1", EMsgRecipCateg.Agent );
			ident.LocalAddresses.Add( "10.0.0.1" );
			var c = new ClientState() { Ident = ident, IP = "10.0.0.1" };
			c.Connected = true;
			return c;
		}

		static bool ClientDiffers( Action<ClientState> change )
		{
			var c = Client();
			var sent = c.Clone();
			change( c );
			return c.DiffersForFeed( sent );
		}

		[TestMethod]
		public void AClientDiffersWhenItsStateDoes_NotWhenItsHeartbeatDoes()
		{
			Assert.IsFalse( Client().DiffersForFeed( Client() ) );
			Assert.IsFalse( ClientDiffers( c => c.LastChange = c.LastChange.AddMinutes( 1 ) ), "the heartbeat" );

			Assert.IsTrue( ClientDiffers( c => c.Connected = false ), "disconnected" );
			Assert.IsTrue( ClientDiffers( c => c.IP = "10.0.0.2" ), "IP" );
			Assert.IsTrue( ClientDiffers( c => c.SelectedPlanName = "plan" ), "selected plan" );
			Assert.IsTrue( ClientDiffers( c => c.Ident = new ClientIdent( "m2", EMsgRecipCateg.Agent ) ), "who it is" );
			Assert.IsTrue( ClientDiffers( c => { var i = new ClientIdent( "m1", EMsgRecipCateg.Agent ); i.LocalAddresses.Add( "10.0.0.9" ); c.Ident = i; } ), "its addresses" );
		}
	}
}
