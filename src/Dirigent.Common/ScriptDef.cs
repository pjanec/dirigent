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
	/// Definition of a single-instance script with all its parameters predefined in shared config
	/// </summary>
	[ProtoBuf.ProtoContract]
	public class ScriptDef : ScriptActionDef
	{
		public override string ToString()
		{
			return $"[{Guid}] \"{Title}\" {Name} {Args}";
		}
	}

}
