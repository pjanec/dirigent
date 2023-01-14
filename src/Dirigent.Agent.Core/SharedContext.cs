using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dirigent
{
	/// <summary>
	/// Stuff shared across multiple components of an agent instance
	/// </summary>
	public record SharedContext
	(
		///<summary>Where to watch for exe etc. Usually the location of SharedConfig.xml</summary>
		string RootForRelativePaths,

		///<summary>Variables not published to started process environment but still usable for expansion within exe file name, cmd line args, startup dirs etc.</summary>
		Dictionary<string, string> ExpansionVars,

		///<summary>Factory for one type af app watchers</summary>
		AppInitializedDetectorFactory AppInitializedDetectorFactory,
		
		///<summary>Network communicator used by the agent. Can be used for sending messages.</summary>
		Net.Client Client,

		string MachineId
	);
}
