﻿#if Windows

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Globalization;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Drawing;
using System.Windows.Forms;
using System.Management;

using X = Dirigent.XmlConfigReaderUtils;

namespace Dirigent
{

    /// <summary>
    /// As soon as the process has its own main window, apply the style to it and stop operating.
	/// This is for processes that create their main window later in their life or
	///	not respecting the WindowStyle passed to CreateProcess.
    /// </summary>
    public class MainWindowStyler : IAppWatcher
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod()?.DeclaringType);

        public IAppWatcher.EFlags Flags => IAppWatcher.EFlags.ClearOnLaunch;
		public bool ShallBeRemoved { get; set; }
		public LocalApp App => _app;

		private AppDef _appDef;
        private AppState _appState;
        private LocalApp _app;

        public MainWindowStyler( LocalApp app )
        {
            _app = app;
            //_appState = _app.AppState;
            _appDef = _app.RecentAppDef;
            _appState = _app.AppState;
        }


        void IAppWatcher.Tick()
        {
            if( !_appState.Running || _app.Process is null  )
            {
				ShallBeRemoved = true;
				return; // do nothing if process has terminated
            }

			try
			{
				IntPtr mainHwnd = _app.Process.MainWindowHandle;
				if( mainHwnd != IntPtr.Zero ) // window has been created!
				{
					SetWindowStyle( mainHwnd, _appDef.WindowStyle, _app.ProcessId );
					ShallBeRemoved = true;
				}
			}
			catch( Exception )
			{
				// ignore any error (MainWindowHandle might not be available if process exits immediately etc.)
				// but if failed, don't try again
				ShallBeRemoved = true;
			}
        }

		public static void SetWindowStyle( IntPtr hWnd, EWindowStyle style, int pid, bool moveToFront=false )
		{
			int showCmd = WinApi.SW_SHOWNORMAL;
			switch( style )
			{
				case EWindowStyle.Hidden:
					showCmd=WinApi.SW_HIDE;
					moveToFront = false;
					break;

				case EWindowStyle.Minimized:
					showCmd=WinApi.SW_MINIMIZE;
					moveToFront = false;
					break;

				case EWindowStyle.Maximized:
					showCmd=WinApi.SW_MAXIMIZE;
					break;

				case EWindowStyle.Normal:
					showCmd=WinApi.SW_SHOWNORMAL;
					break;
			}
			WinApi.ShowWindowAsync( hWnd, showCmd );
			log.DebugFormat("Applied style={0} to main widow 0x{1:X} of proc pid={2}", style, hWnd.ToInt64(), pid );

			if( moveToFront )
			{
			WinApi.SetForegroundWindow( hWnd );
			}
		}

    }
}

#endif
