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
using System.Management;

using X = Dirigent.Common.XmlConfigReaderUtils;

namespace Dirigent.Agent.Core
{

    /// <summary>
    /// As soon as the process has its own main window, apply the style to it and stop operating.
	/// This is for processes that create their main window later in their life or
	///	not respecting the WindowStyle passed to CreateProcess.
    /// </summary>
    public class MainWindowStyler : IAppWatcher
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        bool shallBeRemoved = false;
        int processId;
		AppDef appDef;

        public MainWindowStyler(AppDef appDef, int processId)
        {
			this.appDef = appDef;
			this.processId = processId;
        }


        void IAppWatcher.Tick()
        {
            // is process still existing?
            Process proc;
            try
            {
                proc = Process.GetProcessById( processId ); // throws if process noed not exist
                if( proc == null || proc.HasExited )
				{
					shallBeRemoved = true;
					return; // do nothing if process has terminated
				}
            }
            catch
            {
                shallBeRemoved = true;
                return; // do nothing if process has terminated
            }

			try
			{
				IntPtr mainHwnd = proc.MainWindowHandle;
				if( mainHwnd != IntPtr.Zero ) // window has been created!
				{
					int showCmd = WinApi.SW_SHOWNORMAL;
					switch( appDef.WindowStyle )
					{
						case EWindowStyle.Hidden: showCmd=WinApi.SW_HIDE; break;
						case EWindowStyle.Minimized: showCmd=WinApi.SW_MINIMIZE; break;
						case EWindowStyle.Maximized: showCmd=WinApi.SW_MAXIMIZE; break;
						case EWindowStyle.Normal: showCmd=WinApi.SW_SHOWNORMAL; break;
					}
					WinApi.ShowWindow( mainHwnd, showCmd );
					log.DebugFormat("Applied style={0} to main widow 0x{1:X} of proc pid={2}", appDef.WindowStyle, mainHwnd.ToInt64(), proc.Id );
					shallBeRemoved = true;
				}
			}
			catch( Exception )
			{
				// ignore any error (MainWindowHandle might not be available if process exits immediately etc.)
				// but if failed, don't try again
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