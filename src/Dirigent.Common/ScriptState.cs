using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace Dirigent
{

	[ProtoBuf.ProtoContract]
	public class ScriptState
	{
		//[ProtoBuf.ProtoMember( 1 )]
		//public bool Running = false;

		//[ProtoBuf.ProtoMember( 2 )]
		//public bool Running = false;

		[ProtoBuf.ProtoMember( 3 )]
		public string StatusText = string.Empty;

	}


}
