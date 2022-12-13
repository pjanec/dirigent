using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using log4net;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Dirigent.Gui.WinForms
{
	public static class HotKeysRegistrator
	{
		public const int HOTKEY_ID_START_CURRENT_PLAN = 1;
		public const int HOTKEY_ID_KILL_CURRENT_PLAN = 2;
		public const int HOTKEY_ID_RESTART_CURRENT_PLAN = 3;
		public const int HOTKEY_ID_SELECT_PLAN_0 = 4; 
		public const int HOTKEY_ID_SELECT_PLAN_1 = HOTKEY_ID_SELECT_PLAN_0 + 1;
		public const int HOTKEY_ID_SELECT_PLAN_2 = HOTKEY_ID_SELECT_PLAN_0 + 2;
		public const int HOTKEY_ID_SELECT_PLAN_3 = HOTKEY_ID_SELECT_PLAN_0 + 3;
		public const int HOTKEY_ID_SELECT_PLAN_4 = HOTKEY_ID_SELECT_PLAN_0 + 4;
		public const int HOTKEY_ID_SELECT_PLAN_5 = HOTKEY_ID_SELECT_PLAN_0 + 5;
		public const int HOTKEY_ID_SELECT_PLAN_6 = HOTKEY_ID_SELECT_PLAN_0 + 6;
		public const int HOTKEY_ID_SELECT_PLAN_7 = HOTKEY_ID_SELECT_PLAN_0 + 7;
		public const int HOTKEY_ID_SELECT_PLAN_8 = HOTKEY_ID_SELECT_PLAN_0 + 8;
		public const int HOTKEY_ID_SELECT_PLAN_9 = HOTKEY_ID_SELECT_PLAN_0 + 9;

		// DLL libraries used to manage hotkeys
		[DllImport( "user32.dll" )]
		public static extern bool RegisterHotKey( IntPtr hWnd, int id, int fsModifiers, int vlc );
		[DllImport( "user32.dll" )]
		public static extern bool UnregisterHotKey( IntPtr hWnd, int id );

		public static void RegisterHotKeys( IntPtr hWnd )
		{
			var exeConfigFileName = System.Reflection.Assembly.GetEntryAssembly().Location + ".config";
			XDocument document = XDocument.Load( exeConfigFileName );
			var templ = "/configuration/userSettings/Dirigent.Common.Properties.Settings/setting[@name='{0}']/value";
			{
				var x = document.XPathSelectElement( String.Format( templ, "StartPlanHotKey" ) );
				string hotKeyStr = ( x != null ) ? x.Value : "Control + Shift + Alt + S";
				if( !String.IsNullOrEmpty( hotKeyStr ) )
				{
					var key = ( HotKeys.Keys )HotKeys.HotKeyShared.ParseShortcut( hotKeyStr ).GetValue( 1 );
					var modifier = ( HotKeys.Modifiers )HotKeys.HotKeyShared.ParseShortcut( hotKeyStr ).GetValue( 0 );
					RegisterHotKey( hWnd, HOTKEY_ID_START_CURRENT_PLAN, ( int )modifier, ( int )key );
				}
			}
			{
				var x = document.XPathSelectElement( String.Format( templ, "KillPlanPlanHotKey" ) );
				string hotKeyStr = ( x != null ) ? x.Value : "Control + Shift + Alt + K";
				if( !String.IsNullOrEmpty( hotKeyStr ) )
				{
					var key = ( HotKeys.Keys )HotKeys.HotKeyShared.ParseShortcut( hotKeyStr ).GetValue( 1 );
					var modifier = ( HotKeys.Modifiers )HotKeys.HotKeyShared.ParseShortcut( hotKeyStr ).GetValue( 0 );
					RegisterHotKey( hWnd, HOTKEY_ID_KILL_CURRENT_PLAN, ( int )modifier, ( int )key );
				}
			}

			{
				var x = document.XPathSelectElement( String.Format( templ, "RestartPlanPlanHotKey" ) );
				string hotKeyStr = ( x != null ) ? x.Value : "Control + Shift + Alt + R";
				if( !String.IsNullOrEmpty( hotKeyStr ) )
				{
					var key = ( HotKeys.Keys )HotKeys.HotKeyShared.ParseShortcut( hotKeyStr ).GetValue( 1 );
					var modifier = ( HotKeys.Modifiers )HotKeys.HotKeyShared.ParseShortcut( hotKeyStr ).GetValue( 0 );
					RegisterHotKey( hWnd, HOTKEY_ID_RESTART_CURRENT_PLAN, ( int )modifier, ( int )key );
				}
			}

			for( int i = 0; i <= 9; i++ )
			{
				var x = document.XPathSelectElement( String.Format( templ, String.Format( "SelectPlan{0}HotKey", i ) ) );
				string hotKeyStr = ( x != null ) ? x.Value : String.Format( "Control + Shift + Alt + {0}", i );
				if( !String.IsNullOrEmpty( hotKeyStr ) )
				{
					var key = ( HotKeys.Keys )HotKeys.HotKeyShared.ParseShortcut( hotKeyStr ).GetValue( 1 );
					var modifier = ( HotKeys.Modifiers )HotKeys.HotKeyShared.ParseShortcut( hotKeyStr ).GetValue( 0 );
					RegisterHotKey( hWnd, HOTKEY_ID_SELECT_PLAN_0 + i, ( int )modifier, ( int )key );
				}
			}

			//var hk = HotKeys.HotKeyShared.CombineShortcut(HotKeys.Modifiers.Control | HotKeys.Modifiers.Alt | HotKeys.Modifiers.Shift, HotKeys.Keys.B);

			//string shortcut = "Shift + Alt + H";
			//Keys Key = (Keys)HotKeys.HotKeyShared.ParseShortcut(shortcut).GetValue(1);
			//HotKeys.Modifiers Modifier = (HotKeys.Modifiers)HotKeys.HotKeyShared.ParseShortcut(shortcut).GetValue(0);


			//if (hotKeysEnabled)
			//{

			//	// Modifier keys codes: Alt = 1, Ctrl = 2, Shift = 4, Win = 8
			//	// Compute the addition of each combination of the keys you want to be pressed
			//	// ALT+CTRL = 1 + 2 = 3 , CTRL+SHIFT = 2 + 4 = 6...
			//	RegisterHotKey(hWnd, HOTKEY_ID_START_CURRENT_PLAN, 1+2+4, (int)Keys.R); // CTRL+SHIFT+ALT+R
			//	RegisterHotKey(hWnd, HOTKEY_ID_KILL_CURRENT_PLAN, 1 + 2 + 4, (int)Keys.K); // CTRL+SHIFT+ALT+K
			//}
		}
	}
}
