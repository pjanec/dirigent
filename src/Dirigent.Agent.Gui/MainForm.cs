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
        NotifyIcon notifyIcon;
        bool allowLocalIfDisconnected = false;
        GuiAppCallbacks callbacks;
        //public delegate void OnTickDelegate();
        //public delegate bool IsConnectedDelegate();
        //public delegate void OnCloseDelegate(FormClosingEventArgs e);
        //public delegate void OnMinimizeDelegate();
        //public delegate void OnMinimizeDelegate();

        IDirigentControl ctrl;
        string machineId;
        //OnTickDelegate tickDeleg;
        //IsConnectedDelegate isConnectedDeleg;
        
        //// the following delegates are used by the trayapp to keep the form initialized (just hidden) on close
        //public OnCloseDelegate onCloseDeleg = delegate {};
        //public OnMinimizeDelegate onMinimizeDeleg = delegate {};

        ILaunchPlan plan; // current plan
        List<ILaunchPlan> planRepo; // current plan repo


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

        /// <summary>
        /// Main form constructor
        /// </summary>
        /// <param name="ctrl"></param>
        /// <param name="planRepo"></param>
        /// <param name="machineId"></param>
        /// <param name="notifyIcon"></param>
        /// <param name="allowLocalIfDisconnected">apps and plans can be operated locally even if not connected to master</param>
        /// <param name="callbacks"></param>
        public frmMain(
            IDirigentControl ctrl,
            IEnumerable<ILaunchPlan> planRepo, // planRepo to be used until a new one is received from the master; null if none
            string machineId,
            NotifyIcon notifyIcon,
            bool allowLocalIfDisconnected,
            GuiAppCallbacks callbacks
            )
        {
            this.ctrl = ctrl;
            this.machineId = machineId;
            this.callbacks = callbacks;
            this.notifyIcon = notifyIcon;
            this.allowLocalIfDisconnected = allowLocalIfDisconnected;

            InitializeComponent();

            setDoubleBuffered(lstvApps, true);

            this.plan = null;
            if (planRepo != null)
            {
                this.planRepo = new List<ILaunchPlan>(planRepo);
                PopulatePlanListMenu(this.planRepo);
            }

            // start ticking
            tmrTick.Enabled = true;
        }

        void PopulatePlanListMenu( IEnumerable<ILaunchPlan> planRepo )
        {
            loadToolStripMenuItem.DropDownItems.Clear();

            // fill the Plan -> Load menu with items
            foreach (var plan in planRepo)
            {
                EventHandler clickHandler = (sender, args) => loadPlanSubmenu_onClick(plan);
                var menuItem = new System.Windows.Forms.ToolStripMenuItem(plan.Name, null, clickHandler);

                loadToolStripMenuItem.DropDownItems.Add(menuItem);
            }
        }
        
        void setTitle()
        {
            string planName = "<no plan>";

            var currPlan = ctrl.GetCurrentPlan();
            if( currPlan != null )
            {
                planName = currPlan.Name;
            }

            this.Text = string.Format("Dirigent [{0}] - {1}", machineId, planName);
            if (this.notifyIcon != null)
            {
                this.notifyIcon.Text = string.Format("Dirigent [{0}] - {1}", machineId, planName);
            }
        }


        private void tmrTick_Tick(object sender, EventArgs e)
        {
            callbacks.onTickDeleg();
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
            var plan = ctrl.GetCurrentPlan();
            
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

        /// <summary>
        /// Update the list of apps by doing minimal changes to avoid losing focus.
        /// Adding what is not yet there and deleting what has disappeared.
        /// </summary>
        void refreshAppList_smart()
        {
            // FIXME: still flickering!!!

            ListViewItem selected = null;
            
            var plan = ctrl.GetCurrentPlan();
            
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

        void refreshStatusBar()
        {
            if (callbacks.isConnectedDeleg())
            {
                toolStripStatusLabel1.Text = "Connected.";

            }
            else
            {
                toolStripStatusLabel1.Text = "Diconnected.";
            }

        }

        void refreshMenu()
        {
            bool isConnected = callbacks.isConnectedDeleg();
            bool hasPlan = ctrl.GetCurrentPlan() != null;
            planToolStripMenuItem.Enabled = isConnected || allowLocalIfDisconnected;
            startToolStripMenuItem.Enabled = hasPlan;
            stopToolStripMenuItem.Enabled = hasPlan;
            restartToolStripMenuItem.Enabled = hasPlan;
        }

        void refreshPlans()
        {
            // check for new plans and update local copy/menu if they are different
            var newPlanRepo = ctrl.GetPlanRepo();
            if (!newPlanRepo.SequenceEqual(planRepo))
            {
                planRepo = new List<ILaunchPlan>( newPlanRepo );
                PopulatePlanListMenu(planRepo);
            }

            setTitle();
        }

        void refreshGui()
        {
            //refreshAppList();
            refreshAppList_smart();
            refreshStatusBar();
            refreshMenu();
            refreshPlans();
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

        private void loadPlanSubmenu_onClick( ILaunchPlan plan )
        {
            ctrl.LoadPlan( plan );
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

                    bool connected = callbacks.isConnectedDeleg();

                    // build popup menu
                    var popup = new System.Windows.Forms.ContextMenuStrip(this.components);
                    popup.Enabled = connected || allowLocalIfDisconnected;

                    var killItem = new System.Windows.Forms.ToolStripMenuItem("&Kill");
                    killItem.Click += (s, a) => ctrl.StopApp(appIdTuple);
                    killItem.Enabled = st.Running;
                    popup.Items.Add(killItem);

                    var launchItem = new System.Windows.Forms.ToolStripMenuItem("&Launch");
                    launchItem.Click += (s, a) => ctrl.StartApp(appIdTuple);
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

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                "Dirigent app launcher\nby pjanec\nMIT license",
                "About Dirigent",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void frmMain_Resize(object sender, EventArgs e)
        {
            if (FormWindowState.Minimized == this.WindowState)
            {
                callbacks.onMinimizeDeleg();
            }

            //else if (FormWindowState.Normal == this.WindowState)
            //{
            //}
        }

        private void frmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            callbacks.onCloseDeleg(e);
        }
    }
}
