using System;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent
{
	[MessagePack.MessagePackObject]
	public class PlanScriptDef
	{
		/// <summary>
		/// Path to the script file; Either absolute or relative to the SharedConfig file location
		/// </summary>
		[MessagePack.Key( 1 )]
		public string Name = string.Empty;
	}

}
