using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Common;

namespace Dirigent.Agent.Core
{
    public class AppInitializedDetectorFactory : IAppInitializedDetectorFactory
    {
        delegate IAppInitializedDetector CreateDeleg(AppDef appDef, AppState appState, int processId, string args);
        Dictionary<string, CreateDeleg> creators = new Dictionary<string, CreateDeleg>();

        public AppInitializedDetectorFactory()
        {
            // register creators
            creators[TimeOutInitDetector.Name] = TimeOutInitDetector.create;
            creators[ExitCodeInitDetector.Name] = ExitCodeInitDetector.create;
        }


        private void parseDefinitionString(string definitionString, ref string name, ref string args)
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

        public IAppInitializedDetector create(AppDef appDef, AppState appState, int processId, string definitionString)
        {
            if( string.IsNullOrEmpty(definitionString) )
            {
                throw new UnknownAppInitDetectorType( appDef.AppIdTuple + " <Init condition not defined>" );
            }
            
            string name="";
            string args="";
            parseDefinitionString(definitionString, ref name, ref args);

            if( !creators.ContainsKey(name) )
            {
                throw new UnknownAppInitDetectorType( definitionString );
            }

            CreateDeleg cd = creators[name];
            return cd(appDef, appState, processId, args);
        }
    }
  }
