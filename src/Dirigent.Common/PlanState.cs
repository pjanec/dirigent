using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace Dirigent
{

	/// <summary>
	/// State of an app within a plan; Master's part of app status.
	/// </summary>
	[MessagePack.MessagePackObject]
	public class PlanAppState
	{
		[Flags]
		enum FL
		{
			PlanApplied     = 1 << 0,
		}

		FL flags;

		bool Is( FL value )
		{
			return ( flags & value ) == value;
		}

		void Set( FL value, bool toSet )
		{
			if( toSet ) flags |= value;
			else flags &= ~value;

			changed();
		}

		void changed()
		{
		}

		[MessagePack.Key( 1 )]
		public bool PlanApplied
		{
			get { return Is( FL.PlanApplied ); }
			set { Set( FL.PlanApplied, value ); changed(); }
		}
	}


	/// <summary>
	/// Plan execution status
	/// </summary>
	[MessagePack.MessagePackObject]
	public class PlanState : IEquatable<PlanState>
	{
		[MessagePack.Key( 1 )]
		public bool Running;  // currently taking care of apps (launching, keeping alive...); mutually exclusive with Running

		[MessagePack.Key( 2 )]
		public bool Killing; //	currently killing apps; mutually exclusive with Running

		public enum EOpStatus
		{
			None,	 // plan not running, not controlling apps
			InProgress,	 // still launching
			Success,  // all apps started and initialized and running
			Failure,  // launching some of the apps failed (start failure, init failure, crash...)
			Killing	// we are killing a plan and some apps are still dying
		}

		[MessagePack.Key( 3 )]
		public EOpStatus OpStatus; // status to report to the user; determined fromthe state of contained apps


		[MessagePack.Key( 4 )]
		public DateTime TimeStarted; // to calculate app-start timeout causing plan failure


		[MessagePack.Key( 5 )]
		[MaybeNull] // when constructed without arguments by protobuf
		public Dictionary<AppIdTuple, PlanAppState> PlanAppStates;

		/// <summary>
		/// What the plan is doing before it launches anything, in words - "Marking files (pkg.logs)" - and empty
		/// otherwise. Only plans that mark files on start ever have one.
		/// </summary>
		[MessagePack.Key( 6 )]
		public string Phase = string.Empty;

		/// <summary>When the marking of the current start began (UTC).</summary>
		[MessagePack.Key( 7 )]
		public DateTime? MarkStarted;

		/// <summary>What the last marking came to. Null until a plan with marking has marked.</summary>
		[MessagePack.Key( 8 )]
		public PlanMarkResult? MarkResult;

		/// <summary>When the plan last took a checkpoint of its MarkFiles (its MarkInterval), UTC; null if it never has.</summary>
		[MessagePack.Key( 9 )]
		public DateTime? LastCheckpoint;

		/// <summary>A copy that shares nothing with this one, to remember what was sent while the original goes on changing.</summary>
		public PlanState CloneDeep()
		{
			var c = (PlanState)this.MemberwiseClone();
			if( PlanAppStates is not null )
			{
				c.PlanAppStates = new Dictionary<AppIdTuple, PlanAppState>( PlanAppStates.Count );
				foreach( var (id, st) in PlanAppStates ) c.PlanAppStates[id] = new PlanAppState() { PlanApplied = st.PlanApplied };
			}
			if( MarkResult is not null )
			{
				var r = MarkResult;
				c.MarkResult = new PlanMarkResult() { Marked = r.Marked, Cleared = r.Cleared, Skipped = r.Skipped, Failed = r.Failed,
					TimedOut = r.TimedOut, Cancelled = r.Cancelled, Errors = new List<string>( r.Errors ) };
			}
			return c;
		}

		/// <summary>
		/// Whether anything a GUI shows differs from the state <paramref name="sent"/> last time. Unlike <see cref="Equals(PlanState?)"/>
		/// this looks at everything: the times, and the state of every app of the plan.
		/// </summary>
		public bool DiffersForFeed( PlanState? sent )
		{
			if( sent is null ) return true;
			if( !Equals( sent ) ) return true;
			if( TimeStarted != sent.TimeStarted || MarkStarted != sent.MarkStarted || LastCheckpoint != sent.LastCheckpoint ) return true;

			var a = PlanAppStates; var b = sent.PlanAppStates;
			if( a is null || b is null ) return a is null != b is null;
			if( a.Count != b.Count ) return true;
			foreach( var (id, st) in a )
				if( !b.TryGetValue( id, out var other ) || other.PlanApplied != st.PlanApplied ) return true;
			return false;
		}

		// Add this Clone method
		public PlanState Clone()
		{
			var newObj = (PlanState)this.MemberwiseClone();
			// The dictionary PlanAppStates is not compared, so a shallow copy is sufficient.
			return newObj;
		}

		// Add these new methods for equality comparison
		public bool Equals(PlanState? other)
		{
			if (other is null) return false;

			return this.Running == other.Running &&
				   this.Killing == other.Killing &&
				   this.OpStatus == other.OpStatus &&
				   this.Phase == other.Phase &&
				   ( this.MarkResult is null ? other.MarkResult is null : this.MarkResult.Equals( other.MarkResult ) );
		}

		public override bool Equals(object? obj)
		{
			return Equals(obj as PlanState);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Running, Killing, OpStatus, Phase);
		}
	}

	/// <summary>What the marking a plan does when it starts came to.</summary>
	[MessagePack.MessagePackObject]
	public class PlanMarkResult : IEquatable<PlanMarkResult>
	{
		[MessagePack.Key( 1 )]
		public int Marked;
		[MessagePack.Key( 2 )]
		public int Cleared;
		[MessagePack.Key( 3 )]
		public int Skipped;
		[MessagePack.Key( 4 )]
		public int Failed;

		/// <summary>The marking took longer than the plan's MarkTimeout; it was stopped and the apps launched anyway.</summary>
		[MessagePack.Key( 5 )]
		public bool TimedOut;

		/// <summary>The marking was stopped before it was done: its script was killed - by a second Kill All, by KillScript.</summary>
		[MessagePack.Key( 6 )]
		public bool Cancelled;

		/// <summary>What went wrong, in words: a machine that is not connected, a file that could not be touched.</summary>
		[MessagePack.Key( 7 )]
		public List<string> Errors = new List<string>();

		public bool Equals( PlanMarkResult? o )
			=> o is not null && Marked == o.Marked && Cleared == o.Cleared && Skipped == o.Skipped && Failed == o.Failed
			&& TimedOut == o.TimedOut && Cancelled == o.Cancelled && Errors.SequenceEqual( o.Errors );

		public override bool Equals( object? obj ) => Equals( obj as PlanMarkResult );
		public override int GetHashCode() => HashCode.Combine( Marked, Cleared, Skipped, Failed, TimedOut );
	}

}
