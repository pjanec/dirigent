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

		public List<MachineDef> Machines = new List<MachineDef>();
	}
}
