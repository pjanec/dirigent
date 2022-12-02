using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace Dirigent
{
	[ProtoBuf.ProtoContract]
	public class DTaskScriptState : ScriptState
	{
		public DTaskScriptState() : base() {}

		public DTaskScriptState( EScriptStatus status, string? text, byte[]? data )
			: base( status, text, data )
		{
		}

	}


}
