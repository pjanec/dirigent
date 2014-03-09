using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Common;

namespace Dirigent.Agent.Core
{

    public class AppInitializedDetectorFactory : IAppInitializedDetectorFactory
    {
        delegate IAppInitializedDetector CreateDeleg(AppDef appDef, AppState appState, string args);
        Dictionary<string, CreateDeleg> creators = new Dictionary<string, CreateDeleg>();

        public AppInitializedDetectorFactory()
        {
            // register creators
            creators[TimeOutInitDetector.Name] = TimeOutInitDetector.create;
        }


        private void parseInitCondString(string initConditionString, ref string name, ref string args)
        {
            initConditionString = initConditionString.Trim();

            // jmeno je string az do prvni mezery
            name = "";
            args = "";
            var space = initConditionString.IndexOf(' ');
            if(space < 0)
            {
                name = initConditionString;
            }
            else
            {
                name = initConditionString.Substring(0, space);

                if( initConditionString.Length > space )
                {
                    args = initConditionString.Substring(space+1);
                }
            }

        }

        public IAppInitializedDetector create(AppDef appDef, AppState appState, string initConditionString)
        {
            if( initConditionString == null )
            {
                return new DummyInitDetector();
            }

            string name="";
            string args="";
            parseInitCondString(initConditionString, ref name, ref args);

            if( !creators.ContainsKey(name) )
            {
                throw new UnknownAppInitDetectorType( initConditionString );
            }

            CreateDeleg cd = creators[name];
            return cd(appDef, appState, args);
        }
    }
}
