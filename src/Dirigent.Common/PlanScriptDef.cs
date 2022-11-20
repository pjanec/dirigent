using System;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent
{
	[ProtoBuf.ProtoContract]
	public class PlanScriptDef
	{
		/// <summary>
		/// Path to the script file; Either absolute or relative to the SharedConfig file location
		/// </summary>
		[ProtoBuf.ProtoMember( 1 )]
		public string Name = string.Empty;
	}

}
