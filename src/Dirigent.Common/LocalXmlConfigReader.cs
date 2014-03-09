using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Xml.Linq;

using Dirigent.Common;

using X = Dirigent.Common.XmlConfigReaderUtils;

namespace Dirigent.Common
{
    public class LocalXmlConfigReader
    {

        LocalConfig cfg;
        XDocument doc;

        public LocalConfig Load( System.IO.TextReader textReader )
        {
            cfg = new LocalConfig();
            doc = XDocument.Load(textReader);
            
            loadLocalMachineId();

            return cfg;
        }
        void loadLocalMachineId()
        {
            var master = doc.Element("Local").Element("MachineId");
            cfg.LocalMachineId = X.getStringAttr( master, "Id" );
        }

    }
}
