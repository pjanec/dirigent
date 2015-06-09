using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;

using Dirigent.Common;

using X = Dirigent.Common.XmlConfigReaderUtils;

namespace Dirigent.Agent.Core
{
    public class AppInitializedDetectorFactory : IAppInitializedDetectorFactory
    {
        delegate IAppInitializedDetector CreateDeleg(AppDef appDef, AppState appState, int processId, XElement xml);
        Dictionary<string, CreateDeleg> creators = new Dictionary<string, CreateDeleg>();

        public AppInitializedDetectorFactory()
        {
            // register creators
            creators[TimeOutInitDetector.Name] = TimeOutInitDetector.create;
            creators[ExitCodeInitDetector.Name] = ExitCodeInitDetector.create;
            creators[WindowPoppedUpInitDetector.Name] = WindowPoppedUpInitDetector.create;
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

        public IAppInitializedDetector create(AppDef appDef, AppState appState, int processId, XElement xml)
        {
            //if( string.IsNullOrEmpty(definitionString) )
            //{
            //    throw new UnknownAppInitDetectorType( appDef.AppIdTuple + " <Init condition not defined>" );
            //}
            
            //string name="";
            //string args="";
            //parseDefinitionString(definitionString, ref name, ref args);

            //if( !creators.ContainsKey(name) )
            //{
            //    throw new UnknownAppInitDetectorType( definitionString );
            //}

            //CreateDeleg cd = creators[name];
            //return cd(appDef, appState, processId, args);

            string name = xml.Name.LocalName;

            CreateDeleg cd = Find(name);
            if( cd == null )
            {
                throw new UnknownAppInitDetectorType( name );
            }

            return cd(appDef, appState, processId, xml);
        }

        private CreateDeleg Find( string name )
        {
            // case insensitive search
            foreach( KeyValuePair<string, CreateDeleg> c in creators )
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
