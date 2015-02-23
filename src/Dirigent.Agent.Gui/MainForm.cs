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
            this.clientId = clientId;
            this.callbacks = callbacks;
            this.notifyIcon = notifyIcon;
            this.allowLocalIfDisconnected = allowLocalIfDisconnected;

            InitializeComponent();

            //setDoubleBuffered(gridApps, true); // not needed anymore, DataViewGrid does not flicker

            this.plan = null;
            if (planRepo != null)
            {
                this.planRepo = new List<ILaunchPlan>(planRepo);
                populatePlanLists();
            }

            // start ticking
            tmrTick.Enabled = true;
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

        void refreshStatusBar()
        {
            if (callbacks.isConnectedDeleg())
            {
                toolStripStatusLabel1.Text = "Connected.";

            }
            else
            {
                toolStripStatusLabel1.Text = "Disconnected.";
            }

        }

        void refreshMenu()
        {
            bool isConnected = callbacks.isConnectedDeleg();
            bool hasPlan = ctrl.GetCurrentPlan() != null;
            planToolStripMenuItem.Enabled = isConnected || allowLocalIfDisconnected;
            startPlanToolStripMenuItem.Enabled = hasPlan;
            stopPlanToolStripMenuItem.Enabled = hasPlan;
            killPlanToolStripMenuItem.Enabled = hasPlan;
            restartPlanToolStripMenuItem.Enabled = hasPlan;
        }

        void refreshPlans()
        {
            // check for new plans and update local copy/menu if they are different
            var newPlanRepo = ctrl.GetPlanRepo();
            if (!newPlanRepo.SequenceEqual(planRepo))
            {
                planRepo = new List<ILaunchPlan>( newPlanRepo );
                populatePlanLists();
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

        void populatePlanLists()
        {
            populatePlanSelectionMenu();
            populatePlanGrid();
        }

    }
}
