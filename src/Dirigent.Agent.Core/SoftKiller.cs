using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

using System.Xml.Linq;
using X = Dirigent.XmlConfigReaderUtils;
using System.Runtime.InteropServices;

namespace Dirigent
{
    public class SoftKiller : Disposable
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

        AppDef _appDef;
        Process_? _proc;

        // If soft kill sequence is defined, we try to terminate the process using 
        // the actions from the sequence. Each action has a timeout defined; if timed out,
        // next action (presumably more severe) is executed.
        List<SoftKillAction> _softKillSeq = new List<SoftKillAction>(); // soft kill attempts (before a hard kill is executed)
        int _softKillSeqIndex = -1; // current killseq item just being processed

        public SoftKiller( AppDef appDef )
        {
            this._appDef = appDef;
            this._proc = null;

            parseXml();

        }

        public void AddClose()
        {
            _softKillSeq.Add( new KSI_Close() );
        }


        public bool IsDefined => _softKillSeq.Count > 0;
        public bool IsRunning => _softKillSeqIndex >= 0;

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
            Stop();
		}

        public void Start( Process_ proc )
        {
            this._proc = proc;

            // execute first of the key sequence
            if( _softKillSeq.Count > 0 )
            {
                _softKillSeqIndex = 0;
                _softKillSeq[0].Execute( _appDef, proc );
            }
            else // no kill seq defined
            {
                _softKillSeqIndex = -1;
            }
        }

        public void Stop()
        {
            if( _softKillSeqIndex >= 0 && _softKillSeqIndex < _softKillSeq.Count )
            {
                var ksi = _softKillSeq[_softKillSeqIndex];
                ksi.CleanUp();
            }
            _softKillSeqIndex = -1;
            _proc = null;
        }


        // returns false if no more soft kill action left and process still running
        public bool Tick()
        {
            if( _proc is null ) return true;

            // still executing the kill sequence?
            if( IsRunning )
            {
                var ksi = _softKillSeq[_softKillSeqIndex];

                // tick the kill action
                ksi.Tick();

                // process still running?
                if( !_proc.HasExited )
                {
                    // timed out with current kill action?
                    if( ksi.TimedOut )
                    {
                        log.DebugFormat("SoftKill action {0} timed out", ksi.GetType().Name);
                        // cleanup the previous one
                        ksi.CleanUp();

                        // advance to next (and presumably more severe) kill action
                        _softKillSeqIndex++;

                        if( _softKillSeqIndex < _softKillSeq.Count )
                        {
                            ksi = _softKillSeq[_softKillSeqIndex];
                            ksi.Execute( _appDef, _proc );
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
            protected XElement? xel;
            protected double timeout = -1;
            Stopwatch sw = new Stopwatch();

            public virtual void Tick()
            {
            }

            public bool TimedOut => timeout > 0 && sw.Elapsed.TotalSeconds > timeout;
            
            public SoftKillAction( XElement? elem )
            {
                xel = elem;
                timeout = X.getDoubleAttr(elem, "timeout", -1 , true);
            }

            public virtual void Execute( AppDef appDef, Process_ proc )
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
            public KSI_Close( XElement? xel=null ) : base(xel)
            {
                if( xel == null )
                {
                    // default timeout before killing hard
                    timeout = 10.0;
                }
            }

			public override void Execute(AppDef appDef, Process_ proc)
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
			public override void Execute(AppDef appDef, Process_ proc)
			{
				base.Execute(appDef, proc);

                string keys = X.getStringAttr( xel, "Keys", "", ignoreCase:true );

                #if Windows
                IntPtr h = proc.MainWindowHandle;
                SetForegroundWindow(h);
                System.Windows.Forms.SendKeys.SendWait( keys );
                #endif
			}
        }

        //class KSI_Ctrl : SoftKillAction
        //{
        //    public KSI_Ctrl( XElement xel ) : base(xel) {}
        //}

        void parseXml()
        {
			XElement? xml = null;
			if( !String.IsNullOrEmpty( _appDef.SoftKillXml ) )
			{
				var rootedXmlString = String.Format("<root>{0}</root>", _appDef.SoftKillXml);
				var xmlRoot = XElement.Parse(rootedXmlString);
				xml = xmlRoot.Element("SoftKill");
			}

			if( xml == null ) return;

            foreach( var elem in xml.Descendants() )
            {
                double timeout = X.getDoubleAttr(elem, "timeout", -1 , true);

                string? actionName = elem.Name?.ToString();
                SoftKillAction? ksi = actionName switch
                {
                    "Keys" => new KSI_Keys( elem ),
                    "Close" => new KSI_Close( elem ),
                    //"Ctrl" => new KSI_Ctrl( elem ),
                    _ => null,
                };

                if( ksi != null )
                {
                    _softKillSeq.Add( ksi );
                }
                else
                {
                    log.ErrorFormat( "Unsuported SoftKill action '{0}'", actionName );
                }
            }
        }
 
    }

}
