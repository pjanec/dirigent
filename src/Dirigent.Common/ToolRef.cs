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
	/// Definition of a tool for an app/machine in shared config
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class ToolRef
	{
		/// <summary>
		/// Reference to the list of tools defined for a local machine
		/// </summary>
		[ProtoBuf.ProtoMember(1)]
		public string Id = string.Empty;

		/// <summary>
		/// Display name for UI
		/// </summary>
		[ProtoBuf.ProtoMember(2)]
		public string Title = string.Empty;

		/// <summary>
		/// Arguments that can be passed to the tool
		/// </summary>
		[ProtoBuf.ProtoMember(3)]
		public string CmdLineArgs = string.Empty;

		public bool Equals( ToolRef? other )
		{
			if( other is null )
				return false;

			if(
				this.Id == other.Id &&
				this.Title == other.Title &&
				this.CmdLineArgs == other.CmdLineArgs &&
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

			var typed = obj as ToolRef;
			if( typed is null )
				return false;
			else
				return Equals( typed );
		}

		public override int GetHashCode()
		{
			return this.Id.GetHashCode();
		}

		public static bool operator ==( ToolRef? t1, ToolRef? t2 )
		{
			if( t1 is null || t2 is null )
				return Object.Equals( t1, t2 );

			return t1.Equals( t2 );
		}

		public static bool operator !=( ToolRef? t1, ToolRef? t2 )
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
