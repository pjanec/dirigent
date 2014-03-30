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
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger
            (System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        NotifyIcon notifyIcon;
        bool allowLocalIfDisconnected = false;
        GuiAppCallbacks callbacks;

        IDirigentControl ctrl;
        string machineId;
        string clientId; // name of the network client; messages are marked with that
        
        
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
        /// Dirigent Agent GUI main form constructor
        /// </summary>
        /// <param name="ctrl">instance of object providing dirigent operations</param>
        /// <param name="planRepo">planRepo to be used until a new one is received from the master; null if none</param>
        /// <param name="machineId">machine id (part of application id in launch plans); informative only; used to be presented to the user</param>
        /// <param name="clientId">name of network client used to mark the network messages; informative only; used to recognize incoming errors caused by request from this agent</param>
        /// <param name="notifyIcon">instance of notify icon</param>
        /// <param name="allowLocalIfDisconnected">if true, apps and plans can be operated locally even if not connected to master</param>
        /// <param name="callbacks">bunch of callbacks</param>
        public frmMain(
            IDirigentControl ctrl,
            IEnumerable<ILaunchPlan> planRepo,
            string machineId,
            string clientId,
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


        private void handleOperationError(Exception ex)
        {
            this.notifyIcon.ShowBalloonTip(5000, "Dirigent Operation Error", ex.Message, ToolTipIcon.Error);
            log.Error(ex.Message);
        }

        private void tmrTick_Tick(object sender, EventArgs e)
        {
            try
            {
                callbacks.onTickDeleg();
            }
            catch (RemoteOperationErrorException ex) // operation exception (not necesarily remote, could be also local
                                                     // as all operational requests always go through the network if
                                                     // connected to master
            {
                // if this GUI was the requestor of the operation that failed
                if (ex.Requestor == clientId)
                {
                    handleOperationError(ex);
                }
            }
            catch (Exception ex) // local operation exception
            {
                handleOperationError(ex);
            }

            refreshGui();
        }

        string getStatusCode( AppState st )
        {
            string stCode = "Not running";

            var currPlan = ctrl.GetCurrentPlan();
            bool planRunning = (currPlan != null) && currPlan.Running;

            if( planRunning && !st.PlanApplied )
            {
                stCode = "Planned";
            }

            if (st.Started)
            {
                if (st.Running && !st.Initialized)
                {
                    stCode = "Initializing";
                }
                if (st.Running && st.Initialized)
                {
                    stCode = "Running";
                }
                if (!st.Running)
                {
                    if (st.Killed)
                    {
                        stCode = "Killed";
                    }
                    else
                    {
                        stCode = string.Format("Terminated ({0})", st.ExitCode);
                    }
                }
            }
            else
            if (st.StartFailed)
            {
                stCode = "Failed to start";
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

        /// <summary>
        /// Executes a delegate and show exception window on exception
        /// </summary>
        private static void guardedOp(MethodInvoker mi)
        {
            try
            {
                mi.Invoke();
            }
            catch (Exception ex)
            {
                log.Error(ex);
                ExceptionDialog.showException(ex, "Dirigent Exception", "");
            }
        }

        private void startToolStripMenuItem_Click(object sender, EventArgs e)
        {
            guardedOp( ()=> ctrl.StartPlan() );
        }

        private void stopToolStripMenuItem_Click(object sender, EventArgs e)
        {
            guardedOp(() => ctrl.StopPlan() );
        }

        private void restartToolStripMenuItem_Click(object sender, EventArgs e)
        {
            guardedOp(() => ctrl.RestartPlan());
        }

        private void loadPlanSubmenu_onClick( ILaunchPlan plan )
        {
            guardedOp( ()=> ctrl.LoadPlan( plan ) );
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

                    var launchItem = new System.Windows.Forms.ToolStripMenuItem("&Launch");
                    launchItem.Click += (s, a) => guardedOp(() => ctrl.StartApp(appIdTuple));
                    launchItem.Enabled = !st.Running;
                    popup.Items.Add(launchItem);

                    var killItem = new System.Windows.Forms.ToolStripMenuItem("&Kill");
                    killItem.Click += (s, a) => guardedOp( () => ctrl.StopApp(appIdTuple) );
                    killItem.Enabled = st.Running;
                    popup.Items.Add(killItem);

                    var restartItem = new System.Windows.Forms.ToolStripMenuItem("&Restart");
                    restartItem.Click += (s, a) => guardedOp( () => ctrl.RestartApp(appIdTuple) );
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
