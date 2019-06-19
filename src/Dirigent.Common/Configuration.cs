using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace Dirigent.Common
{
    public class SharedConfig
    {
        //public Dictionary<string, MachineDef> Machines = new Dictionary<string, MachineDef>();
        public List<ILaunchPlan> Plans = new List<ILaunchPlan>();
    }

    public class LocalConfig
    {
		/// <summary>
		/// The XML document with local configuration
		/// </summary>
		public System.Xml.Linq.XDocument xmlDoc;
		public List<System.Xml.Linq.XElement> folderWatcherXmls = new List<System.Xml.Linq.XElement>();
    }
}
