using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;

using Dirigent.Common;

using X = Dirigent.Common.XmlConfigReaderUtils;

namespace Dirigent.Agent
{
    public class AppInitializedDetectorFactory
    {
        delegate IAppInitializedDetector CreateDeleg( LocalApp app, XElement xml );
        Dictionary<string, CreateDeleg> creators = new Dictionary<string, CreateDeleg>();

        public AppInitializedDetectorFactory()
        {
            // register creators
            creators[TimeOutInitDetector.Name] = TimeOutInitDetector.create;
            creators[ExitCodeInitDetector.Name] = ExitCodeInitDetector.create;
            #if Windows
            //creators[WindowPoppedUpInitDetector.Name] = WindowPoppedUpInitDetector.create;
            #endif
        }


        public static void ParseDefinitionString(string definitionString, out string name, out string args)
        {
            definitionString = definitionString.Trim();

            // jmeno je string az do prvni mezery
            name = "";
            args = "";
            var space = definitionString.IndexOf(' ');
            if(space < 0)
            {
                name = definitionString;
            }
            else
            {
                name = definitionString.Substring(0, space);

                if( definitionString.Length > space )
                {
                    args = definitionString.Substring(space+1);
                }
            }

        }

        public IAppInitializedDetector create( LocalApp app, XElement xml)
        {

            string name = xml.Name.LocalName;

            CreateDeleg? cd = Find(name);
            if( cd == null )
            {
                throw new UnknownAppInitDetectorType( name );
            }

            return cd( app, xml);
        }

        private CreateDeleg? Find( string name )
        {
            foreach( var c in creators )
            {
                if( String.Equals(c.Key, name, StringComparison.OrdinalIgnoreCase) )
                {
                    return c.Value;
                }
            }
            return null;
        }
    }

  }
