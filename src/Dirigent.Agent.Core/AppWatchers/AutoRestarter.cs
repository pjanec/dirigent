using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using Dirigent.Common;
using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Windows.Forms;

using X = Dirigent.Common.XmlConfigReaderUtils;

namespace Dirigent.Agent.Core
{

    /// <summary>
    /// When the application terminates, waits a few seconds and set the flags to "not yet started" 
    /// which makes the Dirigent to start the app again if the plan is running.
    /// </summary>
    public class AutoRestarter : IAppWatcher
    {
        AppState appState;
        bool shallBeRemoved = false;
        DateTime waitingStartTime;

        enum eState 
        {
            WaitingForCrash,
            WaitingBeforeRestart,
            Restarting
        };

        eState state;

        readonly double RESTART_DELAY = 5.0; // howlong to wait before restarting the app

        public AutoRestarter(AppDef appDef, AppState appState, int processId, XElement xml)
        {
            this.appState = appState;

            parseXml( xml );

            state =  eState.WaitingForCrash;
        }

        void parseXml( XElement xml )
        {
            //pos = new WindowPos();
            
            //if( xml != null )
            //{
            //    var xrect = xml.Attribute("rect");
            //    if( xrect != null )
            //    {
            //        var myRegex = new Regex(@"\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*");
            //        var m = myRegex.Match( (string) xrect );
            //        if( m != null && m.Success)
            //        {
            //            pos.Rect = new System.Drawing.Rectangle(
            //                int.Parse(m.Groups[1].Value),
            //                int.Parse(m.Groups[2].Value),
            //                int.Parse(m.Groups[3].Value),
            //                int.Parse(m.Groups[4].Value)
            //            );
            //        }
            //    }

            //    pos.Screen = X.getIntAttr(xml, "screen", 0);
            //    pos.TitleRegExp = X.getStringAttr(xml, "titleregexp", null);
            //    pos.Keep = X.getBoolAttr(xml, "keep", false);
            //    pos.Topmost = X.getBoolAttr(xml, "topmost", false);
            //}
        }



        void IAppWatcher.Tick()
        {
            switch( state )
            {
                case eState.WaitingForCrash:
                {
                    // has the application terminated?
                    if( appState.Started && !appState.Running && !appState.Killed )
                    {
                        state = eState.WaitingBeforeRestart;
                        waitingStartTime = DateTime.Now;
                    }
                    break;
                }

                case eState.WaitingBeforeRestart:
                {
                    var waitTime = (DateTime.Now - waitingStartTime).TotalSeconds;
                    if( waitTime > RESTART_DELAY )
                    {
                        state = eState.Restarting;
                        
                        // make the dirigent to start the application again
                        appState.Started = false;
                        appState.PlanApplied = false;
                        appState.Running = false;
                        appState.StartFailed = false;
                        appState.Initialized = false;
                        appState.Killed = false;
                    }
                    break;
                }

                case eState.Restarting:
                {
                    // do nothing, wait until watcher get destroyed when the app is started again
                    break;
                }
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
