using System;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent
{
	[MessagePack.MessagePackObject]
	public class PlanDef
	{
		[MessagePack.Key( 1 )]
		public string Name = string.Empty;

		[MessagePack.Key( 2 )]
		public List<AppDef> AppDefs = new List<AppDef>();

		[MessagePack.Key( 3 )]
		public PlanScriptDef? PlanScriptDef;

		[MessagePack.Key( 4 )]
		public double StartTimeout = 0.0;

		// semicolon separated list of "paths" like "main/examples;"GUI might use this for showing items in a folder tree
		[MessagePack.Key( 5 )]
		public string Groups = string.Empty;

		// update app def of all contained apps when the plan is started
		[MessagePack.Key( 6 )]
		public bool ApplyOnStart;

		// update app def of all contained apps when the plan is selected on GUI
		[MessagePack.Key( 7 )]
		public bool ApplyOnSelect;

		/// <summary>
		/// File definition ids (wildcards allowed) whose clearable files are marked when the plan starts, before the
		/// first app is launched. Empty = nothing is marked, and the plan behaves exactly as it always did.
		/// </summary>
		[MessagePack.Key( 8 )]
		public List<string> MarkFiles = new List<string>();

		/// <summary>The same, clearing: emptied where possible, marked where not.</summary>
		[MessagePack.Key( 9 )]
		public List<string> ClearFiles = new List<string>();

		/// <summary>Seconds the marking may take before the plan carries on without waiting for it.</summary>
		[MessagePack.Key( 10 )]
		public double MarkTimeout = DefaultMarkTimeout;

		public const double DefaultMarkTimeout = 60.0;

		/// <summary>
		/// Seconds between the checkpoints the plan takes of its MarkFiles while it runs - the lengths of the files recorded
		/// in their history, never moving the mark - so that a collection "since a time" can start near that time. 0 (the
		/// default): none.
		/// </summary>
		[MessagePack.Key( 11 )]
		public double MarkInterval;

		/// <summary>The shortest MarkInterval accepted: a checkpoint is a look at every file of the selection on every machine.</summary>
		public const double MinMarkInterval = 10.0;

		[MessagePack.IgnoreMember]
		public bool HasMarking => MarkFiles.Count > 0 || ClearFiles.Count > 0;


		public bool Equals( PlanDef other )
		{
			if( other == null )
				return false;

			if( this.Name == other.Name
					&&
					this.AppDefs.SequenceEqual( other.AppDefs )
					&&
					this.MarkFiles.SequenceEqual( other.MarkFiles )
					&&
					this.ClearFiles.SequenceEqual( other.ClearFiles )
					&&
					this.MarkTimeout == other.MarkTimeout
					&&
					this.MarkInterval == other.MarkInterval
			  )
				return true;
			else
				return false;
		}

		public override bool Equals( Object? obj )
		{
			if( obj == null )
				return false;

			var typed = obj as PlanDef;
			if( typed == null )
				return false;
			else
				return Equals( typed );
		}

		public override int GetHashCode()
		{
			return this.Name.GetHashCode();
		}
	}
}
