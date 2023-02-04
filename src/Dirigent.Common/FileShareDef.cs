using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace Dirigent
{

	/// <summary>
	/// Definition of a file share for a machine
	/// </summary>
	//[MessagePack.MessagePackObject]
	public class FileShareDef : IEquatable<FileShareDef>
	{
		/// <summary>
		/// Unique name of the share
		/// </summary>
		//[MessagePack.Key( 2 )]
		[XmlAttribute]
		public string Name = String.Empty;

		/// <summary>
		/// Local folder path (full one, from root, including drive letter)
		/// </summary>
		//[MessagePack.Key( 3 )]
		[XmlAttribute]
		public string Path = String.Empty;

		public bool ThisEquals( FileShareDef other ) =>
				this.Name == other.Name &&
				this.Path == other.Path &&
				true;

		// boilerplate
		public override bool Equals(object? obj) => this.Equals(obj, ThisEquals);
		public bool Equals(FileShareDef? o) => object.Equals(this, o);
		public static bool operator ==(FileShareDef o1, FileShareDef o2) => object.Equals(o1, o2);
		public static bool operator !=(FileShareDef o1, FileShareDef o2) => !object.Equals(o1, o2);
		public override int GetHashCode() => Name.GetHashCode() ^ Path.GetHashCode();

		public override string ToString()
		{
			return $"{Name} -> {Path}";
		}
	}

}
