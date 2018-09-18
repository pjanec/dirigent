using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Reflection;

namespace Dirigent.Agent.Gui
{
    // This version of ToolStrip enables mouse click's even when the main form is NOT active.
    public class MyToolStrip : System.Windows.Forms.ToolStrip
    {
        const uint WM_LBUTTONDOWN = 0x201;
        const uint WM_LBUTTONUP   = 0x202;

        static private bool down = false;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg==WM_LBUTTONUP && !down) {
                m.Msg=(int)WM_LBUTTONDOWN; base.WndProc(ref m);
                m.Msg=(int)WM_LBUTTONUP;
                }

            if (m.Msg==WM_LBUTTONDOWN) down=true;
            if (m.Msg==WM_LBUTTONUP)   down=false;

            base.WndProc(ref m);
        }
    }
}
