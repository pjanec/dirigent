using System.Collections.Generic;
using System.Xml.Serialization;

namespace Dirigent
{
	public class GatewayDef
	{
		[XmlAttribute]
		public string Id = "";

		[XmlAttribute]
		public string ExternalIP = "";

		[XmlAttribute]
		public string InternalIP = "";

		[XmlAttribute]
		public int Port;

		[XmlAttribute]
		public string UserName = "";

		[XmlAttribute]
		public string Password = "";
		
		[XmlAttribute]
		public string MasterIP = "";

		[XmlAttribute]
		public int MasterPort = 0;

		/// <summary>
		/// List of machines that are accessible through this gateway.
		/// Cached in GatewayConfig, updated from SharedConfig as soon as we connect to the gateway,
		/// reach Dirigent and get the list of machines from it.
		/// </summary>
		public List<MachineDef> Machines = new List<MachineDef>();
	}
}
