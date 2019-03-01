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
			var version = Assembly.GetExecutingAssembly().GetName().Version;

			MessageBox.Show(
                "Dirigent app launcher\nby pjanec\nMIT license\n\nver."+ version,
                "About Dirigent",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

		private void ShowNoPlanSelectedError()
		{
            MessageBox.Show(
                "No plan selected. Select a plan first.",
                "Dirigent",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
		}

		private void startPlanMenuItem_Click(object sender, EventArgs e)
        {
			if( ctrl.GetCurrentPlan() == null )
			{
				ShowNoPlanSelectedError();	
				return;
			}
			guardedOp( ()=> ctrl.StartPlan(ctrl.GetCurrentPlan().Name) );
        }

        private void stopPlanMenuItem_Click(object sender, EventArgs e)
        {
			if( ctrl.GetCurrentPlan() == null )
			{
				ShowNoPlanSelectedError();	
				return;
			}
            guardedOp(() => ctrl.StopPlan(ctrl.GetCurrentPlan().Name));
        }

        private void killPlanMenuItem_Click(object sender, EventArgs e)
        {
			if( ctrl.GetCurrentPlan() == null )
			{
				ShowNoPlanSelectedError();	
				return;
			}
            guardedOp(() => ctrl.KillPlan(ctrl.GetCurrentPlan().Name) );
        }

        private void restartPlanMenuItem_Click(object sender, EventArgs e)
        {
			if( ctrl.GetCurrentPlan() == null )
			{
				ShowNoPlanSelectedError();	
				return;
			}
            guardedOp(() => ctrl.RestartPlan(ctrl.GetCurrentPlan().Name));
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
                var planName = plan.Name; // independent variable to be remebered by the lambda below
                EventHandler clickHandler = (sender, args) => guardedOp( ()=> { ctrl.SelectPlan(planName); } );

                var menuItem = new System.Windows.Forms.ToolStripMenuItem(plan.Name, null, clickHandler);
                selectPlanToolStripMenuItem.DropDownItems.Add(menuItem);

                var menuItem2 = new System.Windows.Forms.MenuItem(plan.Name, clickHandler);
                mnuPlanList.MenuItems.Add(menuItem2);
            }
        }
        

    }
}
