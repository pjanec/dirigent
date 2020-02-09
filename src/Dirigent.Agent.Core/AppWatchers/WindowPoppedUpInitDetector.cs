using System;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Linq;
using System.Text;
using Dirigent.Common;
using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Management;

using X = Dirigent.Common.XmlConfigReaderUtils;

namespace Dirigent.Agent.Core
{
    /// <summary>
    /// Fires when a window belonging to the current process pops up, having a title matching given regular expression
    /// </summary>
    public class WindowPoppedUpInitDetector : IAppInitializedDetector
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        //double TimeOut = 0.0;
        //long InitialTicks;
        AppState appState;
        int processId;
        AppDef appDef;
        bool shallBeRemoved = false;
        
        string titleRegExpString;
        Regex titleRegExp;

        // args example: titleregexp=".*?\s-\sNotepad"
        void parseArgs( XElement xml )
        {
            if( xml != null )
            {
                titleRegExpString = X.getStringAttr(xml, "TitleRegExp", null, ignoreCase:true);

                titleRegExp = new Regex( titleRegExpString );
            }
        }
        
        public WindowPoppedUpInitDetector(AppDef appDef, AppState appState, int processId, XElement xml)
        {
            this.appState = appState;
            this.processId = processId;
            this.appDef = appDef;

            try
            {
                parseArgs( xml );
            }
            catch
            {
                throw new InvalidAppInitDetectorArguments(Name, xml.ToString());
            }

            appState.Initialized = false; // will be set to true as soon as the exit code condition is met

            log.DebugFormat("WindowPoppedUpInitDetector: Waiting for window with titleRegExp {0}, appid {1}, pid {2}", titleRegExpString, appDef.AppIdTuple, processId );
        }

        bool IsInitialized()
        {
            Process pr = null;
            try
            {
                pr = Process.GetProcessById(processId);
            }
            catch( ArgumentException )
            {
            }

            if( pr != null )
            {
                bool found = false;
                var windows = GetProcessWindows( processId );
                foreach( var w in windows )
                {
                    // apply pos for matching titles
                    var m = titleRegExp.Match( w.Title );
                    if( m != null && m.Success )
                    {
                        log.DebugFormat("WindowPoppedUpInitDetector: Found matching window handle 0x{0:X8}, title \"{1}\", pid {2}", w.Handle, w.Title, processId );
                    
                        found = true;
                        shallBeRemoved = true;
                    }
                }
                return found;
            }
            else
            {
                shallBeRemoved = true;
                return false;
            }
        }

        bool IAppInitializedDetector.IsInitialized
        {
            get
            {
                return IsInitialized();
            }
        }

        static public string Name { get { return "WindowPoppedUp"; } }
        static public IAppInitializedDetector create(AppDef appDef, AppState appState, int processId, XElement xml)
        {
            return new WindowPoppedUpInitDetector(appDef, appState, processId, xml);
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



        // from http://stackoverflow.com/questions/2531828/how-to-enumerate-all-windows-belonging-to-a-particular-process-using-net/2584672#2584672

        delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);

        static IEnumerable<IntPtr> EnumerateProcessWindowHandles(int processId)
        {
            var handles = new List<IntPtr>();

            Process pr = null;
            try
            {
                pr = Process.GetProcessById(processId);
            }
            catch( ArgumentException )
            {
            }

            if( pr != null )
            {
				//var str = pr.MainWindowTitle;
				var mainWndH = pr.MainWindowHandle;
				if( mainWndH != IntPtr.Zero ) handles.Add( mainWndH );

                foreach (ProcessThread thread in Process.GetProcessById(processId).Threads)
                    EnumThreadWindows(
						thread.Id, 
                        (hWnd, lParam) =>
						{
							if( handles.IndexOf(hWnd) < 0 )	// add just unique handles
							{
								handles.Add(hWnd);
							}
							return true;
						},
						IntPtr.Zero);
            }
            return handles;
        }

        struct WinInfo
        {
            public System.IntPtr Handle;
            public string Title;
        }

        private List<int> GetChildProcesses(int pid)
        {
            List<int> childrenPID = new List<int>();
            ManagementObjectSearcher searcher = new ManagementObjectSearcher("Select * From Win32_Process Where ParentProcessID=" + pid);
            ManagementObjectCollection moc = searcher.Get();
            foreach (ManagementObject mo in moc)
            {
                childrenPID.Add( Convert.ToInt32(mo["ProcessID"]) );
            }

            return childrenPID;
        }
        
        List<WinInfo> GetProcessWindows( int processId )
        {
            var list = new List<WinInfo>();

            foreach (var handle in EnumerateProcessWindowHandles( processId ) )
            {
                StringBuilder message = new StringBuilder(1000);
                
				UIntPtr result;
				if( SendMessageTimeout(
					handle,
					WM_GETTEXT,
					message.Capacity,
					message,
					SendMessageTimeoutFlags.SMTO_ABORTIFHUNG,
					10,	// timeout
					out result
				).ToInt32() > 0	)
				{
					list.Add( new WinInfo() { Handle = handle, Title = message.ToString() } );
				}
            }

            return list;
        }

		string GetWindowTitle( IntPtr hwnd )
		{
			return "";
		}


        #region WINAPI

        [DllImport("user32.dll")]
        static extern bool EnumThreadWindows(int dwThreadId, EnumThreadDelegate lpfn,
            IntPtr lParam);

        private const uint WM_GETTEXT = 0x000D;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, int wParam, StringBuilder lParam);

		[Flags]
		public enum SendMessageTimeoutFlags : uint
		{
			SMTO_NORMAL = 0x0,
			SMTO_BLOCK = 0x1,
			SMTO_ABORTIFHUNG = 0x2,
			SMTO_NOTIMEOUTIFNOTHUNG = 0x8
		}

		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		public static extern IntPtr SendMessageTimeout(
		IntPtr hWnd,
		uint Msg,
		int wParam,
		StringBuilder lParam,
		SendMessageTimeoutFlags fuFlags,
		uint uTimeout,
		out UIntPtr lpdwResult);

        #endregion

    }
}
