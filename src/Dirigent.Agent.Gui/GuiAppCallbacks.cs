using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using Dirigent.Common;

namespace Dirigent.Agent.Gui
{
    public class GuiAppCallbacks
    {
        public delegate void OnTickDelegate(); // gui app ticking
        public delegate bool IsConnectedDelegate(); // are we connected to master?
        public delegate void OnCloseDelegate(FormClosingEventArgs e); // gui app window closing
        public delegate void OnMinimizeDelegate(); // gui app window minimizing
        public delegate void OnWantExitDelegate(); // gui app want to be terminated
        public delegate void OnTrayIconTextChangedDelegate(string text); // gui app wants to change the tray icon text


        public OnTickDelegate onTickDeleg = delegate { };
        public IsConnectedDelegate isConnectedDeleg = delegate { return false; };
        public OnCloseDelegate onCloseDeleg = delegate { };
        public OnMinimizeDelegate onMinimizeDeleg = delegate { };
        public OnWantExitDelegate onWantExitDelegate = delegate { };
        public OnTrayIconTextChangedDelegate onTrayIconTextChangedDelegate = delegate { };
    }
}
