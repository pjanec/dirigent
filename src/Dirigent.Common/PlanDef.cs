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
		

		public bool Equals( PlanDef other )
		{
			if( other == null )
				return false;

			if( this.Name == other.Name
					&&
					this.AppDefs.SequenceEqual( other.AppDefs )
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
