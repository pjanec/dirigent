using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Dirigent
{
	public class GatewayConfig
	{
		[XmlAnyElement("MachGenComment")]
		public XmlComment MachGenComment { get { return new XmlDocument().CreateComment("This file gets overwritten by Dirigent upon connecting to the gateway!"); } set { } }

		public List<GatewayDef> Gateways = new List<GatewayDef>();
	}
}
