using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Dirigent.Common;

namespace Dirigent.Agent.Core
{
    public static class AppInitializedDetectorFactory
    {
        private static void parseInitCondString(string initConditionString, ref string name, ref string args)
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

        public static IAppInitializedDetector get(string initConditionString)
        {
            string name="";
            string args="";
            parseInitCondString(initConditionString, ref name, ref args);
            return null;
        }
    }
}
