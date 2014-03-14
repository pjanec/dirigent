using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using System.IO;

using Dirigent.Common;

namespace Dirigent.Agent.Gui
{
    public partial class frmMain : Form
    {
        public delegate void TickDelegate();

        IDirigentControl ctrl;
        TickDelegate tickDeleg;

        //void terminateFromConstructor()
        //{
        //    Load += (s, e) => Close();
        //}

        public frmMain( IDirigentControl ctrl, TickDelegate tickDeleg )
        {
            this.ctrl = ctrl;
            this.tickDeleg = tickDeleg;

            InitializeComponent();
            
            // start ticking
            tmrTick.Enabled = true;
        }


        private void tmrTick_Tick(object sender, EventArgs e)
        {
            tickDeleg();
            refreshGui();
        }

        string getStatusCode( AppState st )
        {
            string stCode = "Not running";

            if( st.Running && !st.Initialized )
            {
                stCode = "Initializing";
            }
            if( st.Running && st.Initialized )
            {
                stCode = "Running";
            }
            if( st.WasLaunched && !st.Running )
            {
                stCode = "Terminated";
            }

            
            return stCode;
        }

        void refreshAppList()
        {
            var plan = ctrl.GetPlan();
            
            lstvApps.Items.Clear();

            if( plan != null )
            {
                foreach( AppDef a in plan.getAppDefs() )
                {
                    lstvApps.Items.Add(
                        new ListViewItem(
                            new string[]
                            {
                                a.AppIdTuple.ToString(),
                                getStatusCode( ctrl.GetAppState(a.AppIdTuple) )
                            }
                        )
                    );
                }
            }
        }

        void refreshGui()
        {
            refreshAppList();
        }

        private void startToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ctrl.StartPlan();
        }

        private void stopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ctrl.StopPlan();
        }

        private void restartToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ctrl.RestartPlan();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }
    }
}
