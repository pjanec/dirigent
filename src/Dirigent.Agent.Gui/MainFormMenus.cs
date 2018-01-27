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

        ContextMenu mnuPlanList;  // context menu for the 'Open' toolbar button


        private void aboutMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                "Dirigent app launcher\nby pjanec\nMIT license",
                "About Dirigent",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void startPlanMenuItem_Click(object sender, EventArgs e)
        {
            guardedOp( ()=> ctrl.StartPlan(ctrl.GetCurrentPlan()) );
        }

        private void stopPlanMenuItem_Click(object sender, EventArgs e)
        {
            guardedOp(() => ctrl.StopPlan(ctrl.GetCurrentPlan()));
        }

        private void killPlanMenuItem_Click(object sender, EventArgs e)
        {
            guardedOp(() => ctrl.KillPlan(ctrl.GetCurrentPlan()) );
        }

        private void restartPlanMenuItem_Click(object sender, EventArgs e)
        {
            guardedOp(() => ctrl.RestartPlan(ctrl.GetCurrentPlan()));
        }

        private void selectPlanMenuItem_Click(object sender, EventArgs e)
        {
            //selectPlanToolStripMenuItem.ShowDropDown();
            mnuPlanList.Show(this, this.PointToClient(Cursor.Position) );
        }


        void populatePlanSelectionMenu()
        {
            mnuPlanList = new ContextMenu();

            selectPlanToolStripMenuItem.DropDownItems.Clear();

            // fill the Plan -> Load menu with items
            foreach (var plan in planRepo)
            {
                var planCopy = plan; // independent variable to be remebered by the lambda below
                EventHandler clickHandler = (sender, args) => guardedOp( ()=> { ctrl.SelectPlan(planCopy); } );

                var menuItem = new System.Windows.Forms.ToolStripMenuItem(plan.Name, null, clickHandler);
                selectPlanToolStripMenuItem.DropDownItems.Add(menuItem);

                var menuItem2 = new System.Windows.Forms.MenuItem(plan.Name, clickHandler);
                mnuPlanList.MenuItems.Add(menuItem2);
            }
        }
        

    }
}
