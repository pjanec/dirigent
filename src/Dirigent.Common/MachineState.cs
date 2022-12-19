using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace Dirigent
{
	/// <summary>
	/// Machine status known to master and shared with other participats.
	/// </summary>
	[MessagePack.MessagePackObject]
	public class MachineState
	{
		[MessagePack.Key( 2 )]
		public float CPU; // % of CPU usage

		[MessagePack.Key( 3 )]
		public float MemoryAvailMB; // MBytes

		[MessagePack.Key( 4 )]
		public float MemoryTotalMB; // physical memory Private Working Set MBytes
	}


}
