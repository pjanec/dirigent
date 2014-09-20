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
                        ResizeImage( new Bitmap(Dirigent.Agent.Gui.Resource1.play), new Size(20,20)),
                        ResizeImage( new Bitmap(Dirigent.Agent.Gui.Resource1.stop), new Size(20,20)),
                        ResizeImage( new Bitmap(Dirigent.Agent.Gui.Resource1.delete), new Size(20,20)),
                        ResizeImage( new Bitmap(Dirigent.Agent.Gui.Resource1.refresh), new Size(20,20))
                    }
                );

                // mark currently running plan with different bacground color
                DataGridViewRow row = gridPlans.Rows[rowIndex];
                if( plan.Running )
                {
                    row.DefaultCellStyle.BackColor = Color.LightGoldenrodYellow;
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
                    if( currentCol == 1 ) // start
                    {
                        guardedOp(() => ctrl.SelectPlan( plan ));
                        guardedOp(() => ctrl.StartPlan());
                    }
                    else
                    if( currentCol == 2 ) // stop
                    {
                        guardedOp(() => ctrl.SelectPlan( plan ));
                        guardedOp(() => ctrl.StopPlan());
                    }
                    else
                    if( currentCol == 3 ) // kill
                    {
                        guardedOp(() => ctrl.SelectPlan( plan ));
                        guardedOp(() => ctrl.KillPlan());
                    }
                    else
                    if( currentCol == 4 ) // restart
                    {
                        guardedOp(() => ctrl.SelectPlan( plan ));
                        guardedOp(() => ctrl.RestartPlan());
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
                    // find the plan (by name)
                    var plan = planRepo.FirstOrDefault( p => p.Name == planName );
                    
                    // start the selected plan
                    guardedOp(() => ctrl.SelectPlan( plan ));
                    guardedOp(() => ctrl.StartPlan());
                }
            }
        }


    }
}
