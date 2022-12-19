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
	/// Definition of a file share for a machine
	/// </summary>
	[MessagePack.MessagePackObject]
	public class FileShareDef : IEquatable<FileShareDef>
	{
		[MessagePack.Key( 1 )]
		public string MachineId = String.Empty;

		/// <summary>
		/// Unique name of the share
		/// </summary>
		[MessagePack.Key( 2 )]
		public string Name = String.Empty;

		/// <summary>
		/// Local folder path (full one, from root, including drive letter)
		/// </summary>
		[MessagePack.Key( 3 )]
		public string Path = String.Empty;


		public bool Equals( FileShareDef? other )
		{
			if( other is null )
				return false;

			if(
				this.MachineId == other.MachineId &&
				this.Name == other.Name &&
				this.Path == other.Path &&
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

			var typed = obj as FileShareDef;
			if( typed is null )
				return false;
			else
				return Equals( typed );
		}

		public override int GetHashCode()
		{
			return this.Name.GetHashCode();
		}

		public static bool operator ==( FileShareDef t1, FileShareDef t2 )
		{
			if( ( object )t1 == null || ( ( object )t2 ) == null )
				return Object.Equals( t1, t2 );

			return t1.Equals( t2 );
		}

		public static bool operator !=( FileShareDef t1, FileShareDef t2 )
		{
			if( t1 is null || t2 is null )
				return !Object.Equals( t1, t2 );

			return !( t1.Equals( t2 ) );
		}


		public override string ToString()
		{
			return $"{MachineId}\\{Name} = {Path}";
		}
	}

}
