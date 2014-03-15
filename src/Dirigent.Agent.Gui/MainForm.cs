using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Reflection;

using System.IO;

using Dirigent.Common;

namespace Dirigent.Agent.Gui
{
    public partial class frmMain : Form
    {
        public delegate void TickDelegate();

        IDirigentControl ctrl;
        string machineId;
        TickDelegate tickDeleg;


        //void terminateFromConstructor()
        //{
        //    Load += (s, e) => Close();
        //}

        public static void setDoubleBuffered(Control control, bool enable)
        {
            var doubleBufferPropertyInfo = control.GetType().GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            if (doubleBufferPropertyInfo != null)
            {
                doubleBufferPropertyInfo.SetValue(control, enable, null);
            }
        }
        
        public frmMain( IDirigentControl ctrl, TickDelegate tickDeleg, SharedConfig scfg, string machineId )
        {
            this.ctrl = ctrl;
            this.tickDeleg = tickDeleg;
            this.machineId = machineId;

            InitializeComponent();

            setDoubleBuffered(lstvApps, true);
            
            // fill the Plan -> Load menu with items
            foreach (var plan in scfg.Plans)
            {
                EventHandler clickHandler = (sender, args) => loadPlanSubmenu_onClick(plan.Value);
                var menuItem = new System.Windows.Forms.ToolStripMenuItem(plan.Key, null, clickHandler);

                loadToolStripMenuItem.DropDownItems.Add( menuItem );
            }
            
            // start ticking
            tmrTick.Enabled = true;
        }

        void setTitle(string planName)
        {
            this.Text = string.Format("Dirigent [{0}] - {1}", machineId, planName);
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

        void refreshAppList_smart()
        {
            // FIXME: still flickering!!!

            ListViewItem selected = null;
            
            var plan = ctrl.GetPlan();
            
            // remmber apps from plan
            Dictionary<string, AppDef> newApps = new Dictionary<string, AppDef>();

            if (plan != null)
            {
                foreach( AppDef a in plan.getAppDefs() )
                {
                    newApps[a.AppIdTuple.ToString()] = a;
                }
            }

            // remember apps from list
            Dictionary<string, ListViewItem> oldApps = new Dictionary<string, ListViewItem>();

            foreach (ListViewItem item in lstvApps.Items)
            {
                var id = item.SubItems[0].Text;
                oldApps[id] = item;

                if( item.Selected )
                {
                    if( selected == null )
                    {
                        selected = item;
                    }
                }
            }

            // determine what to add and what to remove
            List<ListViewItem> toRemove = new List<ListViewItem>();
            List<ListViewItem> toAdd = new List<ListViewItem>();

            foreach (ListViewItem item in lstvApps.Items)
            {
                var id = item.SubItems[0].Text;
                if (!newApps.ContainsKey(id) )
                {
                    toRemove.Add( item );
                }
            }

            if (plan != null)
            {
                foreach (AppDef a in plan.getAppDefs())
                {
                    var id = a.AppIdTuple.ToString();
                    if (!oldApps.ContainsKey(id))
                    {
                        toAdd.Add(
                            new ListViewItem(
                                new string[]
                                {
                                    id,
                                    getStatusCode( ctrl.GetAppState(a.AppIdTuple) )

                                }
                            )
                        );
                    }
                }
            }
            
            foreach( var i in toRemove )
            {
                lstvApps.Items.Remove( i );
            }                

            foreach( var i in toAdd )
            {
                lstvApps.Items.Add( i );
            }                
            
            Dictionary<ListViewItem, string> toUpdate = new Dictionary<ListViewItem, string>();
            foreach( var o in oldApps )
            {
                if( !toRemove.Contains(o.Value) )
                {
                    toUpdate[o.Value] = getStatusCode( ctrl.GetAppState(newApps[o.Key].AppIdTuple) );
                }
            }

            foreach (var tu in toUpdate)
            {
                var item = tu.Key;
                item.SubItems[1].Text = tu.Value;
            }


        }

        void refreshGui()
        {
            //refreshAppList();
            refreshAppList_smart();
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

        private void loadPlanSubmenu_onClick( ILaunchPlan plan )
        {
            ctrl.LoadPlan( plan );
            setTitle( plan.Name );
        }

        private void lstvApps_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (lstvApps.FocusedItem.Bounds.Contains(e.Location) == true)
                {
                    var focused = lstvApps.FocusedItem;
                    var appIdTuple = new AppIdTuple(focused.Text);
                    var st = ctrl.GetAppState(appIdTuple);

                    // build popup menu
                    var popup = new System.Windows.Forms.ContextMenuStrip(this.components);

                    var killItem = new System.Windows.Forms.ToolStripMenuItem("&Kill");
                    killItem.Click += (s, a) => ctrl.KillApp(appIdTuple);
                    killItem.Enabled = st.Running;
                    popup.Items.Add(killItem);

                    var launchItem = new System.Windows.Forms.ToolStripMenuItem("&Launch");
                    launchItem.Click += (s, a) => ctrl.RunApp(appIdTuple);
                    launchItem.Enabled = !st.Running;
                    popup.Items.Add(launchItem);

                    var restartItem = new System.Windows.Forms.ToolStripMenuItem("&Restart");
                    restartItem.Click += (s, a) => ctrl.RestartApp(appIdTuple);
                    restartItem.Enabled = st.Running;
                    popup.Items.Add(restartItem);

                    popup.Show(Cursor.Position);
                    
                }
            }
        }
    }
}
