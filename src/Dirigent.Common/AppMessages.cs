using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Dirigent.AppMessages
{
	public class ExitApp
	{
	}

	public class CheckSharedConfigAndRestartMaster
	{
	}

	public class StatusText
	{
		public string Section = "";  // what section of status bar to set (status line has multiple parts)
		public string Type = ""; // "info", "warning", "error"
		public int TimeoutMsec = -1; // when to clear the status text if no further update comes
		public string Text = ""; // text to show

		public StatusText( string section, string text, string type = "", int timeoutMsec = -1 )
		{
			Section = section;
			Text = text;
			Type = type;
			TimeoutMsec = timeoutMsec;
		}
	}
}
