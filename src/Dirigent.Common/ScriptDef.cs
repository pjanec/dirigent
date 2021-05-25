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
	/// Definition of a script in shared config
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class ScriptDef : IEquatable<ScriptDef>
	{
		/// <summary>
		/// Unique script name
		/// </summary>
		[ProtoBuf.ProtoMember( 1 )]
		public string Id = string.Empty;

		[ProtoBuf.ProtoMember(2)]
		public string FileName = string.Empty;

		[ProtoBuf.ProtoMember( 3 )]
		public string Args = string.Empty;

		// semocilon separated list of "paths" like "main/examples;"GUI might use this for showing scripts differently
		[ProtoBuf.ProtoMember( 4 )]
		public string Groups = string.Empty;

		/// <summary>
		/// list of script-local vars to set (can be used in expansions for example in process exe path or command line)
		/// </summary>
		[ProtoBuf.ProtoMember( 5 )]
		[DataMember]
		public Dictionary<string, string> LocalVarsToSet = new Dictionary<string, string>();

		public bool Equals( ScriptDef? other )
		{
			if( other is null )
				return false;

			if(
				this.Id == other.Id &&
				this.FileName == other.FileName &&
				this.Args == other.Args &&
				this.Groups == other.Groups &&
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

			var typed = obj as AppDef;
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
