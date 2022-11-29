using System.Collections.Generic;
using System.Text;
using System.Net;

namespace Dirigent
{

	public class LocalConfig
	{
		/// <summary>
		/// The XML document with local configuration
		/// </summary>
		public System.Xml.Linq.XDocument xmlDoc;
		public List<System.Xml.Linq.XElement> folderWatcherXmls = new List<System.Xml.Linq.XElement>();
		public List<AppDef> Tools = new List<AppDef>();

		public LocalConfig( System.Xml.Linq.XDocument xmlDoc )
		{
			this.xmlDoc = xmlDoc;
		}
	}
}
