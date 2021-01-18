using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Management;

using Dirigent.Common;
using System.Xml.Linq;
using X = Dirigent.Common.XmlConfigReaderUtils;
using System.Runtime.InteropServices;

namespace Dirigent.Agent.Core
{
    public class SoftKiller
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        AppDef appDef;
        Process proc;

        // If soft kill sequence is defined, we try to terminate the process using 
        // the actions from the sequence. Each action has a timeout defined; if timed out,
        // next action (presumably more severe) is executed.
        List<SoftKillAction> softKillSeq = new List<SoftKillAction>(); // soft kill attempts (before a hard kill is executed)
        int softKillSeqIndex = -1; // current killseq item just being processed

        public SoftKiller( AppDef appDef )
        {
            this.appDef = appDef;
            this.proc = null;

            parseXml();

        }

        public void AddClose()
        {
            softKillSeq.Add( new KSI_Close() );
        }


        public bool IsDefined => softKillSeq.Count > 0;
        public bool IsRunning => softKillSeqIndex >= 0;

		public void Dispose()
		{
            Stop();
		}

        public void Start( Process proc )
        {
            this.proc = proc;

            // execute first of the key sequence
            if( softKillSeq.Count > 0 )
            {
                softKillSeqIndex = 0;
                softKillSeq[0].Execute( appDef, proc );
            }
            else // no kill seq defined
            {
                softKillSeqIndex = -1;
            }
        }

        public void Stop()
        {
            if( softKillSeqIndex >= 0 && softKillSeqIndex < softKillSeq.Count )
            {
                var ksi = softKillSeq[softKillSeqIndex];
                ksi.CleanUp();
            }
            softKillSeqIndex = -1;
            proc = null;
        }


        // returns false if no more soft kill action left and process still running
        public bool Tick()
        {
            // still executing the kill sequence?
            if( IsRunning )
            {
                var ksi = softKillSeq[softKillSeqIndex];

                // tick the kill action
                ksi.Tick();

                // process still running?
                if( !proc.HasExited )
                {
                    // timed out with current kill action?
                    if( ksi.TimedOut )
                    {
                        log.DebugFormat("SoftKill action {0} timed out", ksi.GetType().Name);
                        // cleanup the previous one
                        ksi.CleanUp();

                        // advance to next (and presumably more severe) kill action
                        softKillSeqIndex++;

                        if( softKillSeqIndex < softKillSeq.Count )
                        {
                            ksi = softKillSeq[softKillSeqIndex];
                            ksi.Execute( appDef, proc );
                        }
                        else  // no more kill action left
                        {
                            Stop();

                            // report we have failed to kill the process using the sequence...
                            return false;
                        }
                    }
                }
                else // process no longer running - we succeeded!
                {
                    Stop();
                }
            }
            return true;
        }

        class SoftKillAction
        {
            protected XElement xel;
            protected double timeout = -1;
            Stopwatch sw = new Stopwatch();

            public virtual void Tick()
            {
            }

            public bool TimedOut => timeout > 0 && sw.Elapsed.TotalSeconds > timeout;
            
            public SoftKillAction( XElement elem )
            {
                xel = elem;
                timeout = X.getDoubleAttr(elem, "timeout", -1 , true);
            }

            public virtual void Execute( AppDef appDef, Process proc )
            {
                // starts counting
                sw.Restart();
            }

            // called when kill action no longer needed (after it has been executed)
            public virtual void CleanUp()
            {
            }
        }

        class KSI_Close : SoftKillAction
        {
            public KSI_Close( XElement xel=null ) : base(xel)
            {
                if( xel == null )
                {
                    // default timeout before killing hard
                    timeout = 10.0;
                }
            }

			public override void Execute(AppDef appDef, Process proc)
			{
				base.Execute(appDef, proc);

                proc.CloseMainWindow();
			}
		}

        class KSI_Keys : SoftKillAction
        {
            [DllImport ("User32.dll")]
            static extern int SetForegroundWindow(IntPtr point);

            public KSI_Keys( XElement xel ) : base(xel) {}
			public override void Execute(AppDef appDef, Process proc)
			{
				base.Execute(appDef, proc);

                string keys = X.getStringAttr( xel, "Keys", "", ignoreCase:true );

                IntPtr h = proc.MainWindowHandle;
                SetForegroundWindow(h);
                System.Windows.Forms.SendKeys.Send( keys );
			}
        }

        //class KSI_Ctrl : SoftKillAction
        //{
        //    public KSI_Ctrl( XElement xel ) : base(xel) {}
        //}

        void parseXml()
        {
			XElement xml = null;
			if( !String.IsNullOrEmpty( appDef.SoftKillXml ) )
			{
				var rootedXmlString = String.Format("<root>{0}</root>", appDef.SoftKillXml);
				var xmlRoot = XElement.Parse(rootedXmlString);
				xml = xmlRoot.Element("SoftKill");
			}

			if( xml == null ) return;

            foreach( var elem in xml.Descendants() )
            {
                double timeout = X.getDoubleAttr(elem, "timeout", -1 , true);

                SoftKillAction ksi = null;
                string actionName = elem.Name.ToString();
                switch( actionName )
                {
                    case "Keys":
                    {
                        ksi = new KSI_Keys( elem );
                        break;
                    }
                    case "Close":
                    {
                        ksi = new KSI_Close( elem );
                        break;
                    }
                    //case "Ctrl":
                    //{
                    //    ksi = new KSI_Ctrl( elem );
                    //    break;
                    //}
                }
                if( ksi != null )
                {
                    softKillSeq.Add( ksi );
                }
                else
                {
                    log.ErrorFormat( "Unsuported SoftKill action '{0}'", actionName );
                }
            }
        }
 
    }

}
