using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Xml.Linq;
using System.Text.RegularExpressions;

using Dirigent.Common;

using X = Dirigent.Common.XmlConfigReaderUtils;
using System.Diagnostics;

namespace Dirigent.Common
{

	public class LocalXmlConfigReader
	{

		public LocalConfig cfg;
		public XDocument doc;

		public LocalXmlConfigReader( System.IO.TextReader textReader )
		{
			doc = XDocument.Load( textReader );
			cfg = new LocalConfig( doc );

			LoadFolderWatchers();

			//loadPlans();
			//loadMachines();
			//loadMaster();
		}

		void LoadFolderWatchers()
		{
			var fwNodes = from e in doc.Element( "Local" )?.Descendants( "FolderWatcher" )
						  select e;

			foreach( var fwNode in fwNodes )
			{
				cfg.folderWatcherXmls.Add( fwNode );
			}
		}

	}
}
