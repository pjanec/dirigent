using System.Collections.Generic;

namespace Dirigent
{
	public class GatewayDef
	{
		public string Label = "";
		public string ExternalIP = "";
		public string InternalIP = "";
		public int Port;
		public string UserName = "";
		public string Password = "";

		/// <summary>
		/// List of machines that are accessible through this gateway.
		/// Cached in GatewayConfig, updated from SharedConfig as soon as we connect to the gateway,
		/// reach Dirigent and get the list of machines from it.
		/// </summary>
		public List<MachineDef> Machines = new List<MachineDef>();
	}
}
