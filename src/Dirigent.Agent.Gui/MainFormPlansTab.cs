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
        const int planTabColName = 0;
        const int planTabColStatus = 1;
        const int planTabNumCols = planTabColStatus+1;

        void populatePlanGrid()
        {
            gridPlans.Rows.Clear();

            // fill the Plan -> Load menu with items
            foreach (var plan in planRepo)
            {
                int rowIndex = gridPlans.Rows.Add(
                    new object[]
                    {
                        plan.Name,
						"<status>",
                        ResizeImage( new Bitmap(Dirigent.Agent.Gui.Resource1.play), new Size(20,20)),
                        ResizeImage( new Bitmap(Dirigent.Agent.Gui.Resource1.stop), new Size(20,20)),
                        ResizeImage( new Bitmap(Dirigent.Agent.Gui.Resource1.delete), new Size(20,20)),
                        ResizeImage( new Bitmap(Dirigent.Agent.Gui.Resource1.refresh), new Size(20,20))
                    }
                );

				var planState = ctrl.GetPlanState(plan.Name);

    //            // mark currently running plan with different bacground color
    //            DataGridViewRow row = gridPlans.Rows[rowIndex];
                
				//if( planState.Running )
    //            {
    //                row.DefaultCellStyle.BackColor = Color.LightGoldenrodYellow;
    //            }
            }
        }

		void updatePlansStatus()
		{
			for(int i=0; i < gridPlans.RowCount; i++)
			{
				var r = gridPlans.Rows[i];
				string planName = (string) r.Cells[planTabColName].Value;
				var planState = ctrl.GetPlanState(planName);
				r.Cells[planTabColStatus].Value = planState.OpStatus.ToString();

				// mark currently running plans with different background color
				var color = planState.Running ? Color.LightGoldenrodYellow : Color.White;
                r.DefaultCellStyle.BackColor = color;

                // put plan state into a tooltip
                {
                    var planStatusCell = r.Cells[appTabColStatus]; // as DataGridViewCell;
                    planStatusCell.ToolTipText = Tools.GetPlanStateString( planName, ctrl.GetPlanState( planName ) );
                }
			}
		}

        private void gridPlans_MouseClick(object sender, MouseEventArgs e)
        {
            var hti = gridPlans.HitTest(e.X,e.Y);
            int currentRow = hti.RowIndex;
            int currentCol = hti.ColumnIndex;

            if (currentRow >= 0) // ignore header clicks
            {
                DataGridViewRow focused = gridPlans.Rows[currentRow];
                string planName = focused.Cells[0].Value as string;
                bool connected = callbacks.isConnectedDeleg();

                if (e.Button == MouseButtons.Left)
                {
                    // find the plan (by name)
                    var plan = planRepo.FirstOrDefault( p => p.Name == planName );
                    
                    // icon clicks
                    if( currentCol == 2 ) // start
                    {
                        guardedOp(() => ctrl.SelectPlan( plan.Name ));
                        guardedOp(() => ctrl.StartPlan(plan.Name));
                    }
                    else
                    if( currentCol == 3 ) // stop
                    {
                        guardedOp(() => ctrl.SelectPlan( plan.Name ));
                        guardedOp(() => ctrl.StopPlan(plan.Name));
                    }
                    else
                    if( currentCol == 4 ) // kill
                    {
                        guardedOp(() => ctrl.SelectPlan( plan.Name ));
                        guardedOp(() => ctrl.KillPlan(plan.Name));
                    }
                    else
                    if( currentCol == 5 ) // restart
                    {
                        guardedOp(() => ctrl.SelectPlan( plan.Name ));
                        guardedOp(() => ctrl.RestartPlan(plan.Name));
                    }
                    
                
                }
            }
        }

        // starts the doubleclicked plan
        private void gridPlans_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var hti = gridPlans.HitTest(e.X,e.Y);
            int currentRow = hti.RowIndex;
            int currentCol = hti.ColumnIndex;

            if (currentRow >= 0) // ignore header clicks
            {
                DataGridViewRow focused = gridPlans.Rows[currentRow];
                string planName = focused.Cells[0].Value as string;
                bool connected = callbacks.isConnectedDeleg();

                if (e.Button == MouseButtons.Left)
                {
                    // start the selected plan
                    guardedOp(() => ctrl.SelectPlan( planName ));
                    guardedOp(() => ctrl.StartPlan(planName));
                }
            }
        }


    }
}
