using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Dirigent.Common;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace Dirigent.Agent.Core
{
    public class ExitCodeInitDetector : IAppInitializedDetector
    {
        List<int> exitCodes = new List<int>(); // list of exit codes meaning that the app was succesfull
        AppState appState;
        bool shallBeRemoved = false;

        public ExitCodeInitDetector(AppDef appDef, AppState appState, int processId, string args)
        {
            this.appState = appState;

            try
            {
                // " -1, 1, 2,3, 6-8 "
                foreach(var token in args.Split(','))
                {
                    // fill the exitCodes
                    var trimmed = token.Trim();
                    Match m;
                    m = Regex.Match( trimmed, "(-?\\d+)-(-?\\d+)" ); // "1-6" or "-4-8" or "-4--1"
                    if (m.Success)
                    {
                        var lo = int.Parse(m.Groups[1].Value);
                        var hi = int.Parse(m.Groups[2].Value);
                        if( lo <= hi )
                        {
                            for( var i = lo; i <= hi; i++ )
                            {
                                exitCodes.Add(i);
                            }
                        }
                    }
                    else
                    {
                        m = Regex.Match( trimmed, "(-?\\d+)" );
                        if (m.Success)
                        {
                            var i = int.Parse(m.Groups[0].Value);
                            exitCodes.Add(i);
                        }
                    }
                }

                // no exit codes specified??
                if (exitCodes.Count == 0)
                {
                    throw new InvalidAppInitDetectorArguments(Name, args);
                }

                appState.Initialized = false; // will be set to true as soon as the exit code condition is met

            }
            catch( Exception ex )
            {
                if (ex is FormatException || ex is OverflowException)
                {
                    throw new InvalidAppInitDetectorArguments(Name, args);
                }
                throw;
            }
        }

        static public string Name { get { return "exitcode"; } }
        static public IAppInitializedDetector create(AppDef appDef, AppState appState, int processId, string args)
        {
            return new ExitCodeInitDetector(appDef, appState, processId, args);
        }


        bool IsInitialized()
        {
            if( appState.Started && !appState.Running && !appState.Killed )
            {
                if( exitCodes.Contains( appState.ExitCode ) )
                {
                    return true;
                }
            }
            return false;
        }

        bool IAppInitializedDetector.IsInitialized
        {
            get
            {
                return IsInitialized();
            }
        }

        void IAppWatcher.Tick()
        {
            if( IsInitialized() )
            {
                appState.Initialized = true;
                shallBeRemoved = true;
            }
        }

        bool IAppWatcher.ShallBeRemoved
        {
            get
            {
                return shallBeRemoved;
            }
        }
    }
}
