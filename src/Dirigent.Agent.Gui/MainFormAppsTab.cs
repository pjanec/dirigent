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
        void refreshAppList()
        {
            var plan = ctrl.GetCurrentPlan();
            
            gridApps.Rows.Clear();

            if( plan != null )
            {
                foreach( AppDef a in plan.getAppDefs() )
                {
                    gridApps.Rows.Add(
                        new object[]
                        {
                            a.AppIdTuple.ToString(),
                            getAppStatusCode( a.AppIdTuple, ctrl.GetAppState(a.AppIdTuple), true )
                        }
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
            DataGridViewRow selected = null;
            
            var plan = ctrl.GetCurrentPlan();
            var planApps = (plan != null) ? (from ad in plan.getAppDefs() select ad.AppIdTuple).ToList() : new List<AppIdTuple>();
            var appStates = ctrl.GetAllAppsState();
            
            // remember apps from plan
            Dictionary<string, AppIdTuple> newApps = new Dictionary<string, AppIdTuple>();

            foreach (AppIdTuple a in appStates.Keys)
            {
                newApps[a.ToString()] = a;
            }

            // remember apps from list
            Dictionary<string, DataGridViewRow> oldApps = new Dictionary<string, DataGridViewRow>();

            foreach (DataGridViewRow item in gridApps.Rows)
            {
                string id = item.Cells[0].Value as string;
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
            List<DataGridViewRow> toRemove = new List<DataGridViewRow>();
            List<object[]> toAdd = new List<object[]>();

            foreach (DataGridViewRow item in gridApps.Rows)
            {
                string id = item.Cells[0].Value as string;
                if (!newApps.ContainsKey(id) )
                {
                    toRemove.Add( item );
                }
            }

            foreach (var x in appStates)
            {
                var id = x.Key.ToString();
                if (!oldApps.ContainsKey(id))
                {
                    toAdd.Add(
                        new object[]
                        {
                            id,
                            getAppStatusCode( x.Key, x.Value, planApps.Contains(x.Key) ),
                            ResizeImage( new Bitmap(Dirigent.Agent.Gui.Resource1.play), new Size(20,20)),
                            ResizeImage( new Bitmap(Dirigent.Agent.Gui.Resource1.delete), new Size(20,20)),
                            ResizeImage( new Bitmap(Dirigent.Agent.Gui.Resource1.refresh), new Size(20,20))
                        }
                    );
                }
            }
            
            foreach( var i in toRemove )
            {
                gridApps.Rows.Remove( i );
            }                

            foreach( var i in toAdd )
            {
                gridApps.Rows.Add( i );
            }                
            
            Dictionary<DataGridViewRow, string> toUpdate = new Dictionary<DataGridViewRow, string>();
            foreach( var o in oldApps )
            {
                if( !toRemove.Contains(o.Value) )
                {
                    toUpdate[o.Value] = getAppStatusCode( newApps[o.Key], ctrl.GetAppState(newApps[o.Key]), planApps.Contains(newApps[o.Key]) );
                }
            }

            foreach (var tu in toUpdate)
            {
                var row = tu.Key;
                row.Cells[1].Value = tu.Value;
            }

            // colorize the background of items from current plan
            List<string> planAppIds = (from ad in planApps select ad.ToString()).ToList();
            foreach (DataGridViewRow item in gridApps.Rows)
            {
                string id = item.Cells[0].Value as string;
                if (planAppIds.Contains(id))
                {
                    item.DefaultCellStyle.BackColor = Color.LightGoldenrodYellow;
                }
                else
                {
                    item.DefaultCellStyle.BackColor = SystemColors.Control;
                }
            }
        }

        private void gridApps_MouseClick(object sender, MouseEventArgs e)
        {
            var hti = gridApps.HitTest(e.X,e.Y);
            int currentRow = hti.RowIndex;
            int currentCol = hti.ColumnIndex;

            if (currentRow >= 0) // ignore header clicks
            {
                DataGridViewRow focused = gridApps.Rows[currentRow];
                var appIdTuple = new AppIdTuple(focused.Cells[0].Value as string);
                var st = ctrl.GetAppState(appIdTuple);
                bool connected = callbacks.isConnectedDeleg();
                bool isLocalApp = appIdTuple.MachineId == this.machineId;
                bool isAccessible = isLocalApp || connected; // can we change its state?

                if (e.Button == MouseButtons.Right)
                {
                    // build popup menu
                    var popup = new System.Windows.Forms.ContextMenuStrip(this.components);
                    popup.Enabled = connected || allowLocalIfDisconnected;

                    var launchItem = new System.Windows.Forms.ToolStripMenuItem("&Launch");
                    launchItem.Click += (s, a) => guardedOp(() => ctrl.LaunchApp(appIdTuple));
                    launchItem.Enabled = isAccessible && !st.Running;
                    popup.Items.Add(launchItem);

                    var killItem = new System.Windows.Forms.ToolStripMenuItem("&Kill");
                    killItem.Click += (s, a) => guardedOp( () => ctrl.KillApp(appIdTuple) );
                    killItem.Enabled = isAccessible && st.Running;
                    popup.Items.Add(killItem);

                    var restartItem = new System.Windows.Forms.ToolStripMenuItem("&Restart");
                    restartItem.Click += (s, a) => guardedOp( () => ctrl.RestartApp(appIdTuple) );
                    restartItem.Enabled = isAccessible && st.Running;
                    popup.Items.Add(restartItem);

                    popup.Show(Cursor.Position);

                }
                else
                if (e.Button == MouseButtons.Left)
                {
                    // icon clicks
                    if( currentCol == 2 )
                    {
                        if( isAccessible && !st.Running )
                        {
                            guardedOp(() => ctrl.LaunchApp(appIdTuple));
                        }
                    }

                    if( currentCol == 3 )
                    {
                        if( isAccessible && st.Running )
                        {
                            guardedOp(() => ctrl.KillApp(appIdTuple));
                        }
                    }

                    if( currentCol == 4 )
                    {
                        if( isAccessible && st.Running )
                        {
                            guardedOp(() => ctrl.RestartApp(appIdTuple));
                        }
                    }
                
                }
            }
        }

        private void gridApps_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            // launch the app
            if (e.Button == MouseButtons.Left)
            {
                int currentMouseOverRow = gridApps.HitTest(e.X,e.Y).RowIndex;

                if (currentMouseOverRow >= 0)
                {
                    DataGridViewRow focused = gridApps.Rows[currentMouseOverRow];
                    var appIdTuple = new AppIdTuple(focused.Cells[0].Value as string);
                    var st = ctrl.GetAppState(appIdTuple);
                    
                    guardedOp(() => ctrl.LaunchApp(appIdTuple));
                }
            }
        }

        string getAppStatusCode( AppIdTuple appIdTuple, AppState st, bool isPartOfPlan )
        {
            string stCode = "Not running";

			var currPlan = ctrl.GetCurrentPlan();
            bool planRunning = (currPlan != null) && currPlan.Running && isPartOfPlan;
            bool connected = callbacks.isConnectedDeleg();
            var currTime = DateTime.UtcNow;
            bool isRemoteApp = appIdTuple.MachineId != this.machineId;

            if( isRemoteApp && !connected )
            {
                stCode = "??? (discon.)";
                return stCode;
            }

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

            var statusInfoAge = currTime - st.LastChange;
            if( isRemoteApp && statusInfoAge > TimeSpan.FromSeconds(3) )
            {
                stCode += string.Format(" (no info for {0:0} sec)", statusInfoAge.TotalSeconds);
            }

            
            return stCode;
        }


    }
}
