using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Dirigent.Common
{
    public class XmlConfigReaderUtils
    {
        public static string getStringAttr( XElement e, string attrName, string def="" )
        {
            var s = (string) e.Attribute(attrName);
            return ( s == null )? def : s;
        }

        public static int getIntAttr( XElement e, string attrName, int def=0 )
        {
            var s = (string) e.Attribute(attrName);
            return (s == null)? def : int.Parse(s);
        }

        public static double getDoubleAttr( XElement e, string attrName, double def=0.0 )
        {
            var s = (string) e.Attribute(attrName);
            return (s == null)? def : double.Parse(s);
        }

    }
}
