using System;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent
{
	[ProtoBuf.ProtoContract]
	public class PlanDef
	{
		[ProtoBuf.ProtoMember( 1 )]
		public string Name = string.Empty;

		[ProtoBuf.ProtoMember( 2 )]
		public List<AppDef> AppDefs = new List<AppDef>();

		[ProtoBuf.ProtoMember( 3 )]
		public PlanScriptDef? PlanScriptDef;

		[ProtoBuf.ProtoMember( 4 )]
		public double StartTimeout = 0.0;

		// semicolon separated list of "paths" like "main/examples;"GUI might use this for showing items in a folder tree
		[ProtoBuf.ProtoMember( 5 )]
		public string Groups = string.Empty;

		// update app def of all contained apps when the plan is started
		[ProtoBuf.ProtoMember( 6 )]
		public bool ApplyOnStart;

		// update app def of all contained apps when the plan is selected on GUI
		[ProtoBuf.ProtoMember( 7 )]
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
