using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

// origin: https://www.codeproject.com/Articles/442285/Global-Shortcuts-in-WinForms-and-WPF
//         by Akinmade Bond

namespace Dirigent.Gui.WinForms.HotKeys
{
	/// <summary>Defines the key to use as Modifier.
	/// </summary>
	[Flags]
	public enum Modifiers
	{
		/// <summary>Specifies that the key should be treated as is, without any modifier.
		/// </summary>
		None = 0x0000,
		/// <summary>Specifies that the Accelerator key (ALT) is pressed with the key.
		/// </summary>
		Alt = 0x0001,
		/// <summary>Specifies that the Control key is pressed with the key.
		/// </summary>
		Control = 0x0002,
		/// <summary>Specifies that the Shift key is pressed with the associated key.
		/// </summary>
		Shift = 0x0004,
		/// <summary>Specifies that the Window key is pressed with the associated key.
		/// </summary>
		Win = 0x0008
	}

	[Flags]
	//[TypeConverter(typeof(ModifierKeysConverter))]
	//[ValueSerializer(typeof(ModifierKeysValueSerializer))]
	public enum ModifierKeys
	{
		None = 0,
		Alt = 1,
		Control = 2,
		Shift = 4,
		Windows = 8
	}

	/// <summary>Specifies key codes and modifiers.
	/// </summary>
	[Flags]
	//[ComVisible(true)]
	[TypeConverter( typeof( KeysConverter ) )]
	public enum Keys
	{
		/// <summary>The bitmask to extract modifiers from a key value.
		/// </summary>
		Modifiers = -65536,
		/// <summary>No key pressed.
		/// </summary>
		None = 0,
		/// <summary>The left mouse button.
		/// </summary>
		LButton = 1,
		/// <summary>The right mouse button.
		/// </summary>
		RButton = 2,
		/// <summary>The CANCEL key.
		/// </summary>
		Cancel = 3,
		/// <summary>The middle mouse button (three-button mouse).
		/// </summary>
		MButton = 4,
		/// <summary>The first x mouse button (five-button mouse).
		/// </summary>
		XButton1 = 5,
		/// <summary>The second x mouse button (five-button mouse).
		/// </summary>
		XButton2 = 6,
		/// <summary>The BACKSPACE key.
		/// </summary>
		Back = 8,
		/// <summary>The TAB key.
		/// </summary>
		Tab = 9,
		/// <summary>The LINEFEED key.
		/// </summary>
		LineFeed = 10,
		/// <summary>The CLEAR key.
		/// </summary>
		Clear = 12,
		/// <summary>The ENTER key.
		/// </summary>
		Enter = 13,
		/// <summary>The RETURN key.
		/// </summary>
		Return = 13,
		/// <summary>The SHIFT key.
		/// </summary>
		ShiftKey = 16,
		/// <summary>The CTRL key.
		/// </summary>
		ControlKey = 17,
		/// <summary>The ALT key.
		/// </summary>
		Menu = 18,
		/// <summary>The PAUSE key.
		/// </summary>
		Pause = 19,
		/// <summary>The CAPS LOCK key.
		/// </summary>
		CapsLock = 20,
		/// <summary>The CAPS LOCK key.
		/// </summary>
		Capital = 20,
		/// <summary>The IME Kana mode key.
		/// </summary>
		KanaMode = 21,
		/// <summary>The IME Hanguel mode key. (maintained for compatibility; use HangulMode)
		/// </summary>
		HanguelMode = 21,
		/// <summary>The IME Hangul mode key.
		/// </summary>
		HangulMode = 21,
		/// <summary>The IME Junja mode key.
		/// </summary>
		JunjaMode = 23,
		/// <summary>The IME final mode key.
		/// </summary>
		FinalMode = 24,
		/// <summary>The IME Kanji mode key.
		/// </summary>
		KanjiMode = 25,
		/// <summary>The IME Hanja mode key.
		/// </summary>
		HanjaMode = 25,
		/// <summary>The ESC key.
		/// </summary>
		Escape = 27,
		/// <summary>The IME convert key.
		/// </summary>
		IMEConvert = 28,
		/// <summary>The IME nonconvert key.
		/// </summary>
		IMENonconvert = 29,
		/// <summary>The IME accept key. Obsolete, use System.Windows.Forms.Keys.IMEAccept instead.
		/// </summary>
		IMEAceept = 30,
		/// <summary>The IME accept key, replaces System.Windows.Forms.Keys.IMEAceept.
		/// </summary>
		IMEAccept = 30,
		/// <summary>The IME mode change key.
		/// </summary>
		IMEModeChange = 31,
		/// <summary>The SPACEBAR key.
		/// </summary>
		Space = 32,
		/// <summary>The PAGE UP key.
		/// </summary>
		Prior = 33,
		/// <summary>The PAGE UP key.
		/// </summary>
		PageUp = 33,
		/// <summary>The PAGE DOWN key.
		/// </summary>
		Next = 34,
		/// <summary>The PAGE DOWN key.
		/// </summary>
		PageDown = 34,
		/// <summary>The END key.
		/// </summary>
		End = 35,
		/// <summary>The HOME key.
		/// </summary>
		Home = 36,
		/// <summary>The LEFT ARROW key.
		/// </summary>
		Left = 37,
		/// <summary>The UP ARROW key.
		/// </summary>
		Up = 38,
		/// <summary>The RIGHT ARROW key.
		/// </summary>
		Right = 39,
		/// <summary>The DOWN ARROW key.
		/// </summary>
		Down = 40,
		/// <summary>The SELECT key.
		/// </summary>
		Select = 41,
		/// <summary>The PRINT key.
		/// </summary>
		Print = 42,
		/// <summary>The EXECUTE key.
		/// </summary>
		Execute = 43,
		/// <summary>The PRINT SCREEN key.
		/// </summary>
		PrintScreen = 44,
		/// <summary>The PRINT SCREEN key.
		/// </summary>
		Snapshot = 44,
		/// <summary>The INS key.
		/// </summary>
		Insert = 45,
		/// <summary>The DEL key.
		/// </summary>
		Delete = 46,
		/// <summary>The HELP key.
		/// </summary>
		Help = 47,
		/// <summary>The 0 key.
		/// </summary>
		D0 = 48,
		/// <summary>The 1 key.
		/// </summary>
		D1 = 49,
		/// <summary>The 2 key.
		/// </summary>
		D2 = 50,
		/// <summary>The 3 key.
		/// </summary>
		D3 = 51,
		/// <summary>The 4 key.
		/// </summary>
		D4 = 52,
		/// <summary>The 5 key.
		/// </summary>
		D5 = 53,
		/// <summary>The 6 key.
		/// </summary>
		D6 = 54,
		/// <summary>The 7 key.
		/// </summary>
		D7 = 55,
		/// <summary>The 8 key.
		/// </summary>
		D8 = 56,
		/// <summary>The 9 key.
		/// </summary>
		D9 = 57,
		/// <summary>The A key.
		/// </summary>
		A = 65,
		/// <summary>The B key.
		/// </summary>
		B = 66,
		/// <summary>The C key.
		/// </summary>
		C = 67,
		/// <summary>The D key.
		/// </summary>
		D = 68,
		/// <summary>The E key.
		/// </summary>
		E = 69,
		/// <summary>The F key.
		/// </summary>
		F = 70,
		/// <summary>The G key.
		/// </summary>
		G = 71,
		/// <summary>The H key.
		/// </summary>
		H = 72,
		/// <summary>The I key.
		/// </summary>
		I = 73,
		/// <summary>The J key.
		///
		/// </summary>
		J = 74,
		/// <summary>The K key.
		/// </summary>
		K = 75,
		/// <summary>The L key.
		/// </summary>
		L = 76,
		/// <summary>The M key.
		/// </summary>
		M = 77,
		/// <summary>The N key.
		/// </summary>
		N = 78,
		/// <summary>The O key.
		/// </summary>
		O = 79,
		/// <summary>The P key.
		/// </summary>
		P = 80,
		/// <summary>The Q key.
		/// </summary>
		Q = 81,
		/// <summary>The R key.
		/// </summary>
		R = 82,
		/// <summary>The S key.
		/// </summary>
		S = 83,
		/// <summary>The T key.
		/// </summary>
		T = 84,
		/// <summary>The U key.
		/// </summary>
		U = 85,
		/// <summary>The V key.
		/// </summary>
		V = 86,
		/// <summary>The W key.
		/// </summary>
		W = 87,
		/// <summary>The X key.
		/// </summary>
		X = 88,
		/// <summary>The Y key.
		/// </summary>
		Y = 89,
		/// <summary>The Z key.
		/// </summary>
		Z = 90,
		/// <summary>The left Windows logo key (Microsoft Natural Keyboard).
		/// </summary>
		LWin = 91,
		/// <summary>The right Windows logo key (Microsoft Natural Keyboard).
		/// </summary>
		RWin = 92,
		/// <summary>The application key (Microsoft Natural Keyboard).
		/// </summary>
		Apps = 93,
		/// <summary>The computer sleep key.
		/// </summary>
		Sleep = 95,
		/// <summary>The 0 key on the numeric keypad.
		/// </summary>
		NumPad0 = 96,
		/// <summary>The 1 key on the numeric keypad.
		/// </summary>
		NumPad1 = 97,
		/// <summary>The 2 key on the numeric keypad.
		/// </summary>
		NumPad2 = 98,
		/// <summary>The 3 key on the numeric keypad.
		/// </summary>
		NumPad3 = 99,
		/// <summary>The 4 key on the numeric keypad.
		/// </summary>
		NumPad4 = 100,
		/// <summary>The 5 key on the numeric keypad.
		/// </summary>
		NumPad5 = 101,
		/// <summary>The 6 key on the numeric keypad.
		/// </summary>
		NumPad6 = 102,
		/// <summary>The 7 key on the numeric keypad.
		/// </summary>
		NumPad7 = 103,
		/// <summary>The 8 key on the numeric keypad.
		/// </summary>
		NumPad8 = 104,
		/// <summary>The 9 key on the numeric keypad.
		/// </summary>
		NumPad9 = 105,
		/// <summary>The multiply key.
		/// </summary>
		Multiply = 106,
		/// <summary>The add key.
		/// </summary>
		Add = 107,
		/// <summary>The separator key.
		/// </summary>
		Separator = 108,
		/// <summary>The subtract key.
		/// </summary>
		Subtract = 109,
		/// <summary>The decimal key.
		/// </summary>
		Decimal = 110,
		/// <summary>The divide key.
		/// </summary>
		Divide = 111,
		/// <summary>The F1 key.
		/// </summary>
		F1 = 112,
		/// <summary>The F2 key.
		/// </summary>
		F2 = 113,
		/// <summary>The F3 key.
		/// </summary>
		F3 = 114,
		/// <summary>The F4 key.
		/// </summary>
		F4 = 115,
		/// <summary>The F5 key.
		/// </summary>
		F5 = 116,
		/// <summary>The F6 key.
		/// </summary>
		F6 = 117,
		/// <summary>The F7 key.
		/// </summary>
		F7 = 118,
		/// <summary>The F8 key.
		/// </summary>
		F8 = 119,
		/// <summary>The F9 key.
		/// </summary>
		F9 = 120,
		/// <summary>The F10 key.
		/// </summary>
		F10 = 121,
		/// <summary>The F11 key.
		/// </summary>
		F11 = 122,
		/// <summary>The F12 key.
		/// </summary>
		F12 = 123,
		/// <summary>The F13 key.
		/// </summary>
		F13 = 124,
		/// <summary>The F14 key.
		/// </summary>
		F14 = 125,
		/// <summary>The F15 key.
		/// </summary>
		F15 = 126,
		/// <summary>The F16 key.
		/// </summary>
		F16 = 127,
		/// <summary>The F17 key.
		/// </summary>
		F17 = 128,
		/// <summary>The F18 key.
		/// </summary>
		F18 = 129,
		/// <summary>The F19 key.
		/// </summary>
		F19 = 130,
		/// <summary>The F20 key.
		/// </summary>
		F20 = 131,
		/// <summary>The F21 key.
		/// </summary>
		F21 = 132,
		/// <summary>The F22 key.
		/// </summary>
		F22 = 133,
		/// <summary>The F23 key.
		/// </summary>
		F23 = 134,
		/// <summary>The F24 key.
		/// </summary>
		F24 = 135,
		/// <summary>The NUM LOCK key.
		/// </summary>
		NumLock = 144,
		/// <summary>The SCROLL LOCK key.
		/// </summary>
		Scroll = 145,
		/// <summary>The left SHIFT key.
		/// </summary>
		LShiftKey = 160,
		/// <summary>The right SHIFT key.
		/// </summary>
		RShiftKey = 161,
		/// <summary>The left CTRL key.
		/// </summary>
		LControlKey = 162,
		/// <summary>The right CTRL key.
		/// </summary>
		RControlKey = 163,
		/// <summary>The left ALT key.
		/// </summary>
		LMenu = 164,
		/// <summary>The right ALT key.
		/// </summary>
		RMenu = 165,
		/// <summary>The browser back key (Windows 2000 or later).
		/// </summary>
		BrowserBack = 166,
		/// <summary>The browser forward key (Windows 2000 or later).
		/// </summary>
		BrowserForward = 167,
		/// <summary>The browser refresh key (Windows 2000 or later).
		/// </summary>
		BrowserRefresh = 168,
		/// <summary>The browser stop key (Windows 2000 or later).
		/// </summary>
		BrowserStop = 169,
		/// <summary>The browser search key (Windows 2000 or later).
		/// </summary>
		BrowserSearch = 170,
		/// <summary>The browser favourites key (Windows 2000 or later).
		/// </summary>
		BrowserFavorites = 171,
		/// <summary>The browser home key (Windows 2000 or later).
		/// </summary>
		BrowserHome = 172,
		/// <summary>The volume mute key (Windows 2000 or later).
		/// </summary>
		VolumeMute = 173,
		/// <summary>The volume down key (Windows 2000 or later).
		/// </summary>
		VolumeDown = 174,
		/// <summary>The volume up key (Windows 2000 or later).
		/// </summary>
		VolumeUp = 175,
		/// <summary>The media next track key (Windows 2000 or later).
		/// </summary>
		MediaNextTrack = 176,
		/// <summary>The media previous track key (Windows 2000 or later).
		/// </summary>
		MediaPreviousTrack = 177,
		/// <summary>The media Stop key (Windows 2000 or later).
		/// </summary>
		MediaStop = 178,
		/// <summary>The media play pause key (Windows 2000 or later).
		/// </summary>
		MediaPlayPause = 179,
		/// <summary>The launch mail key (Windows 2000 or later).
		/// </summary>
		LaunchMail = 180,
		/// <summary>The select media key (Windows 2000 or later).
		/// </summary>
		SelectMedia = 181,
		/// <summary>The start application one key (Windows 2000 or later).
		/// </summary>
		LaunchApplication1 = 182,
		/// <summary>The start application two key (Windows 2000 or later).
		/// </summary>
		LaunchApplication2 = 183,
		/// <summary>The OEM 1 key.
		/// </summary>
		Oem1 = 186,
		/// <summary>The OEM Semicolon key on a US standard keyboard (Windows 2000 or later).
		/// </summary>
		OemSemicolon = 186,
		/// <summary>The OEM plus key on any country/region keyboard (Windows 2000 or later).
		/// </summary>
		Oemplus = 187,
		/// <summary>The OEM comma key on any country/region keyboard (Windows 2000 or later).
		/// </summary>
		Oemcomma = 188,
		/// <summary>The OEM minus key on any country/region keyboard (Windows 2000 or later).
		/// </summary>
		OemMinus = 189,
		/// <summary>The OEM period key on any country/region keyboard (Windows 2000 or later).
		/// </summary>
		OemPeriod = 190,
		/// <summary>The OEM question mark key on a US standard keyboard (Windows 2000 or later).
		/// </summary>
		OemQuestion = 191,
		/// <summary>The OEM 2 key.
		/// </summary>
		Oem2 = 191,
		/// <summary>The OEM tilde key on a US standard keyboard (Windows 2000 or later).
		/// </summary>
		Oemtilde = 192,
		/// <summary>The OEM 3 key.
		/// </summary>
		Oem3 = 192,
		/// <summary>The OEM 4 key.
		/// </summary>
		Oem4 = 219,
		/// <summary>The OEM open bracket key on a US standard keyboard (Windows 2000 or later).
		/// </summary>
		OemOpenBrackets = 219,
		/// <summary>The OEM pipe key on a US standard keyboard (Windows 2000 or later).
		/// </summary>
		OemPipe = 220,
		/// <summary>The OEM 5 key.
		/// </summary>
		Oem5 = 220,
		/// <summary>The OEM 6 key.
		/// </summary>
		Oem6 = 221,
		/// <summary>The OEM close bracket key on a US standard keyboard (Windows 2000 or later).
		/// </summary>
		OemCloseBrackets = 221,
		/// <summary>The OEM 7 key.
		/// </summary>
		Oem7 = 222,
		/// <summary>The OEM singled/double quote key on a US standard keyboard (Windows 2000 or later).
		/// </summary>
		OemQuotes = 222,
		/// <summary>The OEM 8 key.
		/// </summary>
		Oem8 = 223,
		/// <summary>The OEM 102 key.
		/// </summary>
		Oem102 = 226,
		/// <summary>The OEM angle bracket or backslash key on the RT 102 key keyboard (Windows 2000 or later).
		/// </summary>
		OemBackslash = 226,
		/// <summary>The PROCESS KEY key.
		/// </summary>
		ProcessKey = 229,
		/// <summary>Used to pass Unicode characters as if they were keystrokes.
		///     The Packet key value is the low word of a 32-bit virtual-key value used for non-keyboard
		///     input methods.
		/// </summary>
		Packet = 231,
		/// <summary>The ATTN key.
		/// </summary>
		Attn = 246,
		/// <summary>The CRSEL key.
		/// </summary>
		Crsel = 247,
		/// <summary>The EXSEL key.
		/// </summary>
		Exsel = 248,
		/// <summary>The ERASE EOF key.
		/// </summary>
		EraseEof = 249,
		/// <summary>The PLAY key.
		/// </summary>
		Play = 250,
		/// <summary>The ZOOM key.
		/// </summary>
		Zoom = 251,
		/// <summary>A constant reserved for future use.
		/// </summary>
		NoName = 252,
		/// <summary>The PA1 key.
		/// </summary>
		Pa1 = 253,
		/// <summary>The CLEAR key.
		/// </summary>
		OemClear = 254,
		/// <summary>The bitmask to extract a key code from a key value.
		/// </summary>
		KeyCode = 65535,
		/// <summary>The SHIFT modifier key.
		/// </summary>
		Shift = 65536,
		/// <summary>The CTRL modifier key.
		/// </summary>
		Control = 131072,
		/// <summary>The ALT modifier key.
		/// </summary>
		Alt = 262144,
	}


	//A class to keep share procedures
	public static class HotKeyShared
	{
		/// <summary>Checks if a string is a valid Hotkey name.
		/// </summary>
		/// <param name="text">The string to check</param>
		/// <returns>true if the name is valid.</returns>
		public static bool IsValidHotkeyName( string text )
		{
			//If the name starts with a number, contains space or is null, return false.
			if( string.IsNullOrEmpty( text ) ) return false;

			if( text.Contains( " " ) || char.IsDigit( ( char )text.ToCharArray().GetValue( 0 ) ) )
				return false;

			return true;
		}
		/// <summary>Parses a shortcut string like 'Control + Alt + Shift + V' and returns the key and modifiers.
		/// </summary>
		/// <param name="text">The shortcut string to parse.</param>
		/// <returns>The Modifier in the lower bound and the key in the upper bound.</returns>
		public static object[] ParseShortcut( string text )
		{
			bool HasAlt = false;
			bool HasControl = false;
			bool HasShift = false;
			bool HasWin = false;

			Modifiers Modifier = Modifiers.None;        //Variable to contain modifier.
			Keys key = 0;           //The key to register.
			int current = 0;

			string[] result;
			string[] separators = new string[] { " + " };
			result = text.Split( separators, StringSplitOptions.RemoveEmptyEntries );

			//Iterate through the keys and find the modifier.
			foreach( string entry in result )
			{
				//Find the Control Key.
				if( entry.Trim() == Keys.Control.ToString() )
				{
					HasControl = true;
				}
				//Find the Alt key.
				if( entry.Trim() == Keys.Alt.ToString() )
				{
					HasAlt = true;
				}
				//Find the Shift key.
				if( entry.Trim() == Keys.Shift.ToString() )
				{
					HasShift = true;
				}

				//Find the Window key.
				if( entry.Trim() == Keys.LWin.ToString() && current != result.Length - 1 )
				{
					HasWin = true;
				}

				current++;
			}

			if( HasControl ) { Modifier |= Modifiers.Control; }
			if( HasAlt ) { Modifier |= Modifiers.Alt; }
			if( HasShift ) { Modifier |= Modifiers.Shift; }
			if( HasWin ) { Modifier |= Modifiers.Win; }

			KeysConverter keyconverter = new KeysConverter();
			key = ( Keys )keyconverter.ConvertFrom( result.GetValue( result.Length - 1 ) );

			return new object[] { Modifier, key };
		}
		/// <summary>Parses a shortcut string like 'Control + Alt + Shift + V' and returns the key and modifiers.
		/// </summary>
		/// <param name="text">The shortcut string to parse.</param>
		/// <param name="separator">The delimiter for the shortcut.</param>
		/// <returns>The Modifier in the lower bound and the key in the upper bound.</returns>
		public static object[] ParseShortcut( string text, string separator )
		{
			bool HasAlt = false;
			bool HasControl = false;
			bool HasShift = false;
			bool HasWin = false;

			Modifiers Modifier = Modifiers.None;        //Variable to contain modifier.
			Keys key = 0;           //The key to register.
			int current = 0;

			string[] result;
			string[] separators = new string[] { separator };
			result = text.Split( separators, StringSplitOptions.RemoveEmptyEntries );

			//Iterate through the keys and find the modifier.
			foreach( string entry in result )
			{
				//Find the Control Key.
				if( entry.Trim() == Keys.Control.ToString() )
				{
					HasControl = true;
				}
				//Find the Alt key.
				if( entry.Trim() == Keys.Alt.ToString() )
				{
					HasAlt = true;
				}
				//Find the Shift key.
				if( entry.Trim() == Keys.Shift.ToString() )
				{
					HasShift = true;
				}
				//Find the Window key.
				if( entry.Trim() == Keys.LWin.ToString() && current != result.Length - 1 )
				{
					HasWin = true;
				}

				current++;
			}

			if( HasControl ) { Modifier |= Modifiers.Control; }
			if( HasAlt ) { Modifier |= Modifiers.Alt; }
			if( HasShift ) { Modifier |= Modifiers.Shift; }
			if( HasWin ) { Modifier |= Modifiers.Win; }

			KeysConverter keyconverter = new KeysConverter();
			key = ( Keys )keyconverter.ConvertFrom( result.GetValue( result.Length - 1 ) );

			return new object[] { Modifier, key };
		}
		/// <summary>Combines the modifier and key to a shortcut.
		/// Changes Control;Shift;Alt;T to Control + Shift + Alt + T
		/// </summary>
		/// <param name="mod">The modifier.</param>
		/// <param name="key">The key.</param>
		/// <returns>A string representation of the modifier and key.</returns>
		public static string CombineShortcut( Modifiers mod, Keys key )
		{
			string hotkey = "";
			foreach( Modifiers a in new HotKeyShared.ParseModifier( ( int )mod ) )
			{
				hotkey += a.ToString() + " + ";
			}

			if( hotkey.Contains( Modifiers.None.ToString() ) ) hotkey = "";
			hotkey += key.ToString();
			return hotkey;
		}
		/// <summary>Combines the modifier and key to a shortcut.
		/// Changes Control;Shift;Alt; to Control + Shift + Alt
		/// </summary>
		/// <param name="mod">The modifier.</param>
		/// <returns>A string representation of the modifier</returns>
		public static string CombineShortcut( Modifiers mod )
		{
			string hotkey = "";
			foreach( Modifiers a in new HotKeyShared.ParseModifier( ( int )mod ) )
			{
				hotkey += a.ToString() + " + ";
			}

			if( hotkey.Contains( Modifiers.None.ToString() ) ) hotkey = "";
			if( hotkey.Trim().EndsWith( "+" ) ) hotkey = hotkey.Trim().Substring( 0, hotkey.Length - 1 );

			return hotkey;
		}
		/// <summary>Allows the conversion of an integer to its modifier representation.
		/// </summary>
		public struct ParseModifier : IEnumerable
		{
			private List<Modifiers> Enumeration;
			public bool HasAlt;
			public bool HasControl;
			public bool HasShift;
			public bool HasWin;

			/// <summary>Initializes this class.
			/// </summary>
			/// <param name="Modifier">The integer representation of the modifier to parse.</param>
			public ParseModifier( int Modifier )
			{
				Enumeration = new List<Modifiers>();
				HasAlt = false;
				HasWin = false;
				HasShift = false;
				HasControl = false;
				switch( Modifier )
				{
					case 0:
						Enumeration.Add( Modifiers.None );
						break;
					case 1:
						HasAlt = true;
						Enumeration.Add( Modifiers.Alt );
						break;
					case 2:
						HasControl = true;
						Enumeration.Add( Modifiers.Control );
						break;
					case 3:
						HasAlt = true;
						HasControl = true;
						Enumeration.Add( Modifiers.Control );
						Enumeration.Add( Modifiers.Alt );
						break;
					case 4:
						HasShift = true;
						Enumeration.Add( Modifiers.Shift );
						break;
					case 5:
						HasShift = true;
						HasAlt = true;
						Enumeration.Add( Modifiers.Shift );
						Enumeration.Add( Modifiers.Alt );
						break;
					case 6:
						HasShift = true;
						HasControl = true;
						Enumeration.Add( Modifiers.Shift );
						Enumeration.Add( Modifiers.Control );
						break;
					case 7:
						HasControl = true;
						HasShift = true;
						HasAlt = true;
						Enumeration.Add( Modifiers.Shift );
						Enumeration.Add( Modifiers.Control );
						Enumeration.Add( Modifiers.Alt );
						break;
					case 8:
						HasWin = true;
						Enumeration.Add( Modifiers.Win );
						break;
					case 9:
						HasAlt = true;
						HasWin = true;
						Enumeration.Add( Modifiers.Alt );
						Enumeration.Add( Modifiers.Win );
						break;
					case 10:
						HasControl = true;
						HasWin = true;
						Enumeration.Add( Modifiers.Control );
						Enumeration.Add( Modifiers.Win );
						break;
					case 11:
						HasControl = true;
						HasAlt = true;
						HasWin = true;
						Enumeration.Add( Modifiers.Control );
						Enumeration.Add( Modifiers.Alt );
						Enumeration.Add( Modifiers.Win );
						break;
					case 12:
						HasShift = true;
						HasWin = true;
						Enumeration.Add( Modifiers.Shift );
						Enumeration.Add( Modifiers.Win );
						break;
					case 13:
						HasShift = true;
						HasAlt = true;
						HasWin = true;
						Enumeration.Add( Modifiers.Shift );
						Enumeration.Add( Modifiers.Alt );
						Enumeration.Add( Modifiers.Win );
						break;
					case 14:
						HasShift = true;
						HasControl = true;
						HasWin = true;
						Enumeration.Add( Modifiers.Shift );
						Enumeration.Add( Modifiers.Control );
						Enumeration.Add( Modifiers.Win );
						break;
					case 15:
						HasShift = true;
						HasControl = true;
						HasAlt = true;
						HasWin = true;
						Enumeration.Add( Modifiers.Shift );
						Enumeration.Add( Modifiers.Control );
						Enumeration.Add( Modifiers.Alt );
						Enumeration.Add( Modifiers.Win );
						break;
					default:
						throw new ArgumentOutOfRangeException( "The argument is parsed is more than the expected range", "Modifier" );
				}
			}
			/// <summary>Initializes this class.
			/// </summary>
			/// <param name="mod">the modifier to parse.</param>
			public ParseModifier( Modifiers mod ) : this( ( int )mod ) { }

			IEnumerator IEnumerable.GetEnumerator()
			{
				return Enumeration.GetEnumerator();
			}
		}
	}

	/// <summary>Provides a System.ComponentModel.TypeConverter to convert System.Windows.Forms.Keys
	///     objects to and from other representations.
	/// </summary>
	public class KeysConverter : TypeConverter, IComparer
	{
		private List<string> displayOrder;
		private const Keys FirstAscii = Keys.A;
		private const Keys FirstDigit = Keys.D0;
		private const Keys FirstNumpadDigit = Keys.NumPad0;
		private IDictionary keyNames;
		private const Keys LastAscii = Keys.Z;
		private const Keys LastDigit = Keys.D9;
		private const Keys LastNumpadDigit = Keys.NumPad9;
		private TypeConverter.StandardValuesCollection values;

		private void AddKey( string key, Keys value )
		{
			this.keyNames[key] = value;
			this.displayOrder.Add( key );
		}

		public override bool CanConvertFrom( ITypeDescriptorContext context, System.Type sourceType )
		{
			if( ( sourceType != typeof( string ) ) && ( sourceType != typeof( Enum[] ) ) )
			{
				return base.CanConvertFrom( context, sourceType );
			}
			return true;
		}

		public override bool CanConvertTo( ITypeDescriptorContext context, System.Type destinationType )
		{
			return ( ( destinationType == typeof( Enum[] ) ) || base.CanConvertTo( context, destinationType ) );
		}

		public int Compare( object a, object b )
		{
			return string.Compare( base.ConvertToString( a ), base.ConvertToString( b ), false, CultureInfo.InvariantCulture );
		}

		public override object ConvertFrom( ITypeDescriptorContext context, CultureInfo culture, object value )
		{
			if( value is string )
			{
				string str = ( ( string )value ).Trim();
				if( str.Length == 0 )
				{
					return null;
				}
				string[] strArray = str.Split( new char[] { '+' } );
				for( int i = 0; i < strArray.Length; i++ )
				{
					strArray[i] = strArray[i].Trim();
				}
				Keys none = Keys.None;
				bool flag = false;
				for( int j = 0; j < strArray.Length; j++ )
				{
					object obj2 = this.KeyNames[strArray[j]];
					if( obj2 == null )
					{
						obj2 = Enum.Parse( typeof( Keys ), strArray[j] );
					}
					if( obj2 == null )
					{
						throw new FormatException( "Invalid Key Name" );
					}
					Keys keys2 = ( Keys )obj2;
					if( ( keys2 & Keys.KeyCode ) != Keys.None )
					{
						if( flag )
						{
							throw new FormatException( "Invalid Key Combination" );
						}
						flag = true;
					}
					none |= keys2;
				}
				return none;
			}
			if( !( value is Enum[] ) )
			{
				return base.ConvertFrom( context, culture, value );
			}
			long num3 = 0L;
			foreach( Enum enum2 in ( Enum[] )value )
			{
				num3 |= Convert.ToInt64( enum2, CultureInfo.InvariantCulture );
			}
			return Enum.ToObject( typeof( Keys ), num3 );
		}

		public override object ConvertTo( ITypeDescriptorContext context, CultureInfo culture, object value, System.Type destinationType )
		{
			if( destinationType == null )
			{
				throw new ArgumentNullException( "destinationType" );
			}
			if( ( value is Keys ) || ( value is int ) )
			{
				bool flag = destinationType == typeof( string );
				bool flag2 = false;
				if( !flag )
				{
					flag2 = destinationType == typeof( Enum[] );
				}
				if( flag || flag2 )
				{
					Keys keys = ( Keys )value;
					bool flag3 = false;
					ArrayList list = new ArrayList();
					Keys keys2 = keys & ~Keys.KeyCode;
					for( int i = 0; i < this.DisplayOrder.Count; i++ )
					{
						string str = this.DisplayOrder[i];
						Keys keys3 = ( Keys )this.keyNames[str];
						if( ( keys3 & keys2 ) != Keys.None )
						{
							if( flag )
							{
								if( flag3 )
								{
									list.Add( "+" );
								}
								list.Add( str );
							}
							else
							{
								list.Add( keys3 );
							}
							flag3 = true;
						}
					}
					Keys keys4 = keys & Keys.KeyCode;
					bool flag4 = false;
					if( flag3 && flag )
					{
						list.Add( "+" );
					}
					for( int j = 0; j < this.DisplayOrder.Count; j++ )
					{
						string str2 = this.DisplayOrder[j];
						Keys keys5 = ( Keys )this.keyNames[str2];
						if( keys5.Equals( keys4 ) )
						{
							if( flag )
							{
								list.Add( str2 );
							}
							else
							{
								list.Add( keys5 );
							}
							flag3 = true;
							flag4 = true;
							break;
						}
					}
					if( !flag4 && Enum.IsDefined( typeof( Keys ), ( int )keys4 ) )
					{
						if( flag )
						{
							list.Add( keys4.ToString() );
						}
						else
						{
							list.Add( keys4 );
						}
					}
					if( !flag )
					{
						return ( Enum[] )list.ToArray( typeof( Enum ) );
					}
					StringBuilder builder = new StringBuilder( 0x20 );
					foreach( string str3 in list )
					{
						builder.Append( str3 );
					}
					return builder.ToString();
				}
			}
			return base.ConvertTo( context, culture, value, destinationType );
		}

		public override TypeConverter.StandardValuesCollection GetStandardValues( ITypeDescriptorContext context )
		{
			if( this.values == null )
			{
				ArrayList list = new ArrayList();
				foreach( object obj2 in this.KeyNames.Values )
				{
					list.Add( obj2 );
				}
				list.Sort( this );
				this.values = new TypeConverter.StandardValuesCollection( list.ToArray() );
			}
			return this.values;
		}

		public override bool GetStandardValuesExclusive( ITypeDescriptorContext context )
		{
			return false;
		}

		public override bool GetStandardValuesSupported( ITypeDescriptorContext context )
		{
			return true;
		}

		private void Initialize()
		{
			this.keyNames = new Hashtable( 0x22 );
			this.displayOrder = new List<string>( 0x22 );
			this.AddKey( Keys.Enter.ToString().ToUpper(), Keys.Enter );
			this.AddKey( "F12", Keys.F12 );
			this.AddKey( "F11", Keys.F11 );
			this.AddKey( "F10", Keys.F10 );
			this.AddKey( Keys.End.ToString().ToUpper(), Keys.End );
			this.AddKey( Keys.Control.ToString().ToUpper(), Keys.Control );
			this.AddKey( "F8", Keys.F8 );
			this.AddKey( "F9", Keys.F9 );
			this.AddKey( Keys.Alt.ToString().ToUpper(), Keys.Alt );
			this.AddKey( "F4", Keys.F4 );
			this.AddKey( "F5", Keys.F5 );
			this.AddKey( "F6", Keys.F6 );
			this.AddKey( "F7", Keys.F7 );
			this.AddKey( "F1", Keys.F1 );
			this.AddKey( "F2", Keys.F2 );
			this.AddKey( "F3", Keys.F3 );
			this.AddKey( Keys.PageDown.ToString().ToUpper(), Keys.PageDown );
			this.AddKey( Keys.Insert.ToString().ToUpper(), Keys.Insert );
			this.AddKey( Keys.Home.ToString().ToUpper(), Keys.Home );
			this.AddKey( Keys.Delete.ToString().ToUpper(), Keys.Delete );
			this.AddKey( Keys.Shift.ToString().ToUpper(), Keys.Shift );
			this.AddKey( Keys.PageUp.ToString().ToUpper(), Keys.PageUp );
			this.AddKey( Keys.Back.ToString().ToUpper(), Keys.Back );
			this.AddKey( "0", Keys.D0 );
			this.AddKey( "1", Keys.D1 );
			this.AddKey( "2", Keys.D2 );
			this.AddKey( "3", Keys.D3 );
			this.AddKey( "4", Keys.D4 );
			this.AddKey( "5", Keys.D5 );
			this.AddKey( "6", Keys.D6 );
			this.AddKey( "7", Keys.D7 );
			this.AddKey( "8", Keys.D8 );
			this.AddKey( "9", Keys.D9 );
		}

		private List<string> DisplayOrder
		{
			get
			{
				if( this.displayOrder == null )
				{
					this.Initialize();
				}
				return this.displayOrder;
			}
		}

		private IDictionary KeyNames
		{
			get
			{
				if( this.keyNames == null )
				{
					this.Initialize();
				}
				return this.keyNames;
			}
		}
	}

	internal static class Consts
	{
		internal const int KeyboardHook = 13;
		internal const int KEYEVENTF_EXTENDEDKEY = 0x1;
		internal const int KEYEVENTF_KEYUP = 0x2;
	}

	internal static class HelperMethods
	{
		/// <summary>
		/// This delegate matches the type of parameter "lpfn" for the NativeMethods method "SetWindowsHookEx".
		/// For more information: http://msdn.microsoft.com/en-us/library/ms644986(VS.85).aspx
		/// </summary>
		/// <param name="nCode">
		/// Specifies whether the hook procedure must process the message.
		/// If nCode is HC_ACTION, the hook procedure must process the message.
		/// If nCode is less than zero, the hook procedure must pass the message to the
		/// CallNextHookEx function without further processing and must return the
		/// value returned by CallNextHookEx.
		/// </param>
		/// <param name="wParam">
		/// Specifies whether the message was sent by the current thread.
		/// If the message was sent by the current thread, it is nonzero; otherwise, it is zero.
		/// </param>
		/// <param name="lParam">Pointer to a CWPSTRUCT structure that contains details about the message.
		/// </param>
		/// <returns>
		/// If nCode is less than zero, the hook procedure must return the value returned by CallNextHookEx.
		/// If nCode is greater than or equal to zero, it is highly recommended that you call CallNextHookEx
		/// and return the value it returns; otherwise, other applications that have installed WH_CALLWNDPROC
		/// hooks will not receive hook notifications and may behave incorrectly as a result. If the hook
		/// procedure does not call CallNextHookEx, the return value should be zero.
		/// </returns>
		internal delegate IntPtr HookProc( int nCode, IntPtr wParam, IntPtr lParam );

		/// <summary>Registers a shortcut on a global level.
		/// </summary>
		/// <param name="hwnd">
		/// Handle to the window that will receive WM_HOTKEY messages generated by the hot key.
		/// If this parameter is NULL, WM_HOTKEY messages are posted to the message queue of the calling thread and must be processed in the message loop.
		/// </param>
		/// <param name="id">Specifies the identifier of the hot key.
		/// If the hWnd parameter is NULL, then the hot key is associated with the current thread rather than with a particular window.
		/// If a hot key already exists with the same hWnd and id parameters
		/// </param>
		/// <param name="modifiers">
		/// Specifies keys that must be pressed in combination with the key specified by the Key parameter in order to generate the WM_HOTKEY message.
		/// The fsModifiers parameter can be a combination of the following values.
		///MOD_ALT
		///Either ALT key must be held down.
		///MOD_CONTROL
		///Either CTRL key must be held down.
		///MOD_SHIFT
		///Either SHIFT key must be held down.
		///MOD_WIN
		///Either WINDOWS key was held down. These keys are labelled with the Windows logo.
		///Keyboard shortcuts that involve the WINDOWS key are reserved for use by the operating system.
		///</param>
		/// <param name="key">Specifies the virtual-key code of the hot key.
		///</param>
		/// <returns>If the function succeeds, the return value is nonzero.
		///If the function fails, the return value is zero. To get extended error information, call GetLastError.
		///</returns>
		[DllImport( "user32", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true )]
		internal static extern int RegisterHotKey( IntPtr hwnd, int id, int modifiers, int key );

		/// <summary>
		/// </summary>
		/// <param name="hwnd">Handle to the window associated with the hot key to be freed.
		/// This parameter should be NULL if the hot key is not associated with a window.
		///</param>
		/// <param name="id">Specifies the identifier of the hot key to be freed.
		///</param>
		/// <returns>If the function succeeds, the return value is nonzero.
		///If the function fails, the return value is zero. To get extended error information, call GetLastError.
		///</returns>
		[DllImport( "user32", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true )]
		internal static extern int UnregisterHotKey( IntPtr hwnd, int id );

		/// <summary>
		/// The SetWindowsHookEx function installs an application-defined hook procedure into a hook chain.
		/// You would install a hook procedure to monitor the system for certain types of events. These events
		/// are associated either with a specific thread or with all threads in the same desktop as the calling thread.
		/// </summary>
		/// <param name="idHook">
		/// [in] Specifies the type of hook procedure to be installed. This parameter can be one of the following values.
		/// </param>
		/// <param name="lpfn">
		/// [in] Pointer to the hook procedure. If the dwThreadId parameter is zero or specifies the identifier of a
		/// thread created by a different process, the lpfn parameter must point to a hook procedure in a dynamic-link
		/// library (DLL). Otherwise, lpfn can point to a hook procedure in the code associated with the current process.
		/// </param>
		/// <param name="hMod">
		/// [in] Handle to the DLL containing the hook procedure pointed to by the lpfn parameter.
		/// The hMod parameter must be set to NULL if the dwThreadId parameter specifies a thread created by
		/// the current process and if the hook procedure is within the code associated with the current process.
		/// </param>
		/// <param name="dwThreadId">
		/// [in] Specifies the identifier of the thread with which the hook procedure is to be associated.
		/// If this parameter is zero, the hook procedure is associated with all existing threads running in the
		/// same desktop as the calling thread.
		/// </param>
		/// <returns>
		/// If the function succeeds, the return value is the handle to the hook procedure.
		/// If the function fails, the return value is NULL. To get extended error information, call GetLastError.
		/// </returns>
		/// <remarks>
		/// http://msdn.microsoft.com/library/default.asp?url=/library/en-us/winui/winui/windowsuserinterface/windowing/hooks/hookreference/hookfunctions/setwindowshookex.asp
		/// </remarks>
		[DllImport( "user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true )]
		internal static extern IntPtr SetWindowsHookEx( int idHook, HookProc lpfn, IntPtr hMod, int dwThreadId );

		/// <summary>Retrieves a module handle for the specified module.
		/// The module must have been loaded by the calling process.
		/// </summary>
		/// <param name="lpModuleName">
		/// The name of the loaded module (either a .dll or .exe file).
		/// If the file name extension is omitted, the default library extension .dll is appended.
		/// The file name string can include a trailing point character (.) to indicate that the module name has no extension. The string does not have to specify a path. When specifying a path, be sure to use backslashes (\), not forward slashes (/). The name is compared (case independently) to the names of modules currently mapped into the address space of the calling process.
		///If this parameter is NULL, GetModuleHandle returns a handle to the file used to create the calling process (.exe file).
		///The GetModuleHandle function does not retrieve handles for modules that were loaded using the LOAD_LIBRARY_AS_DATAFILE flag.
		///</param>
		///<returns>
		///If the function succeeds, the return value is a handle to the specified module.
		///If the function fails, the return value is NULL.
		/// </returns>
		[DllImport( "kernel32.dll", CharSet = CharSet.Auto, SetLastError = true )]
		internal static extern IntPtr GetModuleHandle( string lpModuleName );

		/// <summary>
		/// The UnhookWindowsHookEx function removes a hook procedure installed in a hook chain by the SetWindowsHookEx function.
		/// </summary>
		/// <param name="idHook">
		/// [in] Handle to the hook to be removed. This parameter is a hook handle obtained by a previous call to SetWindowsHookEx.
		/// </param>
		/// <returns>
		/// If the function succeeds, the return value is nonzero.
		/// If the function fails, the return value is zero. To get extended error information, call GetLastError.
		/// </returns>
		/// <remarks>
		/// http://msdn.microsoft.com/library/default.asp?url=/library/en-us/winui/winui/windowsuserinterface/windowing/hooks/hookreference/hookfunctions/setwindowshookex.asp
		/// </remarks>
		[DllImport( "user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true )]
		[return: MarshalAs( UnmanagedType.Bool )]
		internal static extern int UnhookWindowsHookEx( IntPtr idHook );

		/// <summary>
		/// The CallNextHookEx function passes the hook information to the next hook procedure in the current hook chain.
		/// A hook procedure can call this function either before or after processing the hook information.
		/// </summary>
		/// <param name="idHook">Ignored.</param>
		/// <param name="nCode">
		/// [in] Specifies the hook code passed to the current hook procedure.
		/// The next hook procedure uses this code to determine how to process the hook information.
		/// </param>
		/// <param name="wParam">
		/// [in] Specifies the wParam value passed to the current hook procedure.
		/// The meaning of this parameter depends on the type of hook associated with the current hook chain.
		/// </param>
		/// <param name="lParam">
		/// [in] Specifies the lParam value passed to the current hook procedure.
		/// The meaning of this parameter depends on the type of hook associated with the current hook chain.
		/// </param>
		/// <returns>
		/// This value is returned by the next hook procedure in the chain.
		/// The current hook procedure must also return this value. The meaning of the return value depends on the hook type.
		/// For more information, see the descriptions of the individual hook procedures.
		/// </returns>
		/// <remarks>
		/// http://msdn.microsoft.com/library/default.asp?url=/library/en-us/winui/winui/windowsuserinterface/windowing/hooks/hookreference/hookfunctions/setwindowshookex.asp
		/// </remarks>
		[DllImport( "user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall, SetLastError = true )]
		internal static extern IntPtr CallNextHookEx( IntPtr idHook, int nCode, IntPtr wParam, IntPtr lParam );

		/// <summary>
		///The MapVirtualKey function translates (maps) a virtual-key code into a scan code or character value, or translates a scan code into a virtual-key code.
		///</summary>
		///<param name="uCode">Specifies the virtual-key code or scan code for a key.
		///How this value is interpreted depends on the value of the uMapType parameter.
		///</param>
		///<param name="uMapType">Specifies the translation to perform.</param>
		///<returns>The return value is either a scan code, a virtual-key code, or a character value, depending on the value of uCode and uMapType.
		///If there is no translation, the return value is zero.
		///</returns>
		[DllImport( "user32.dll" )]
		internal static extern uint MapVirtualKey( uint uCode, uint uMapType );

		///<summary>
		///The keybd_event function synthesizes a keystroke.
		///The system can use such a synthesized keystroke to generate a WM_KEYUP or WM_KEYDOWN message.
		///</summary>
		///<param name="key">Specifies a virtual-key code. The code must be a value in the range 1 to 254.</param>
		///<param name="scan">Specifies a hardware scan code for the key.
		///</param>
		///<param name="flags">
		///Specifies various aspects of function operation. This parameter can be one or more of the following values.
		///KEYEVENTF_EXTENDEDKEY
		///If specified, the scan code was preceded by a prefix byte having the value 0xE0 (224).
		///KEYEVENTF_KEYUP
		///If specified, the key is being released. If not specified, the key is being depressed.
		///</param>
		///<param name="extraInfo">Specifies an additional value associated with the key stroke.
		///</param>
		[DllImport( "user32.dll" )]
		internal static extern void keybd_event( byte key, byte scan, int flags, int extraInfo );

		internal static IntPtr SetWindowsHook( int hookType, HookProc callback )
		{
			IntPtr hookId;
			using( var currentProcess = Process.GetCurrentProcess() )
				using( var currentModule = currentProcess.MainModule )
				{
					var handle = HelperMethods.GetModuleHandle( currentModule.ModuleName );
					hookId = HelperMethods.SetWindowsHookEx( hookType, callback, handle, 0 );
				}
			return hookId;
		}
	}
}
