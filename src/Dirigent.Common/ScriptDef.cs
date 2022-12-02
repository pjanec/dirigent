using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Dirigent
{

	/// <summary>
	/// Definition of a single-instance script in shared config
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class ScriptDef : IEquatable<ScriptDef>
	{
		/// <summary>
		/// Unique script name
		/// </summary>
		[ProtoBuf.ProtoMember( 1 )]
		public Guid Id;

		[ProtoBuf.ProtoMember( 2 )]
		public string Title = String.Empty;

		/// <summary>
		/// Name in the script library
		/// </summary>
		[ProtoBuf.ProtoMember(3)]
		public string Name = string.Empty;

		[ProtoBuf.ProtoMember( 4 )]
		public string Args = string.Empty;

		// semicilon separated list of "paths" like "main/examples;"GUI might use this for showing scripts in a folder tree
		[ProtoBuf.ProtoMember( 5 )]
		public string Groups = string.Empty;

		/// <summary>
		/// On which node (client/agent/master) to run this script. empty=master.
		/// </summary>
		[ProtoBuf.ProtoMember( 6 )]
		public string MachineId = string.Empty;

		public bool Equals( ScriptDef? other )
		{
			if( other is null )
				return false;

			if(
				this.Id == other.Id &&
				this.Title == other.Title &&
				this.Name == other.Name &&
				this.Args == other.Args &&
				this.Groups == other.Groups &&
				this.MachineId == other.MachineId &&
				true
			)
				return true;
			else
				return false;
		}

		public override bool Equals( Object? obj )
		{
			if( obj == null )
				return false;

			var typed = obj as ScriptDef;
			if( typed is null )
				return false;
			else
				return Equals( typed );
		}

		public override int GetHashCode()
		{
			return this.Id.GetHashCode();
		}

		public static bool operator ==( ScriptDef t1, ScriptDef t2 )
		{
			if( ( object )t1 == null || ( ( object )t2 ) == null )
				return Object.Equals( t1, t2 );

			return t1.Equals( t2 );
		}

		public static bool operator !=( ScriptDef t1, ScriptDef t2 )
		{
			if( t1 is null || t2 is null )
				return !Object.Equals( t1, t2 );

			return !( t1.Equals( t2 ) );
		}


		public override string ToString()
		{
			return $"{Id}";
		}
	}

}
