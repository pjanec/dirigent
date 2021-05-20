using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent.Gui.WinForms.AppMessages
{
	public class Tick
	{
	}

	public class OnClose
	{
	}

	// recipient fills in the IsConnected
	public class QueryIsConnected
	{
		public bool IsConnected;
	}
}
