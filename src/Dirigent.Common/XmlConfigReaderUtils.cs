using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Dirigent.Common
{
    public static class XmlConfigReaderUtils
    {
        public static string getStringAttr( XElement e, string attrName, string def="", bool ignoreCase=false )
        {
            var s = (string) Attribute( e, attrName, ignoreCase );
            return ( s == null )? def : s;
        }

        public static int getIntAttr( XElement e, string attrName, int def=0, bool ignoreCase=false )
        {
            var s = (string) Attribute( e, attrName, ignoreCase );
            return (s == null)? def : int.Parse(s);
        }

        public static double getDoubleAttr( XElement e, string attrName, double def=0.0, bool ignoreCase=false )
        {
            var s = (string) Attribute( e, attrName, ignoreCase );
            return (s == null)? def : double.Parse(s, CultureInfo.InvariantCulture);
        }

        public static bool getBoolAttr( XElement e, string attrName, bool def=false, bool ignoreCase=false )
        {
            var s = (string) Attribute( e, attrName, ignoreCase );
            return ( s == null )? def : (s == "1") || (s.ToLower() == "true") || (s.ToLower() == "yes");
        }

        // ignore case if set
        public static XElement Element( this XElement element, XName name, bool ignoreCase )
        {
            var el = element.Element( name );
            if (el != null)
                return el;
 
            if (!ignoreCase)
                return null;
 
            var elements = element.Elements().Where( e => e.Name.LocalName.ToString().ToLowerInvariant() == name.ToString().ToLowerInvariant() );
            return elements.Count() == 0 ? null : elements.First();
        }
        // ignore case if set
        public static XAttribute Attribute( this XElement element, XName name, bool ignoreCase )
        {
            var at = element.Attribute( name );
            if (at != null)
                return at;
 
            if (!ignoreCase)
                return null;
 
            var ats = element.Attributes().Where( e => e.Name.LocalName.ToString().ToLowerInvariant() == name.ToString().ToLowerInvariant() );
            return ats.Count() == 0 ? null : ats.First();
        }

    }
}
