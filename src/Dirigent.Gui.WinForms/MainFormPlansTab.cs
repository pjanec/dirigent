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

namespace Dirigent.Gui.WinForms
{
	public partial class frmMain : Form
	{
		const int planTabColName = 0;
		const int planTabColStatus = 1;
		const int planTabNumCols = planTabColStatus + 1;

		void populatePlanGrid()
		{
			gridPlans.Rows.Clear();

			// fill the Plan -> Load menu with items
			foreach( var plan in _planRepo )
			{
				int rowIndex = gridPlans.Rows.Add(
								   new object[]
				{
					plan.Name,
					"<status>",
					ResizeImage( new Bitmap( Resource1.play ), new Size( 20, 20 ) ),
					ResizeImage( new Bitmap( Resource1.stop ), new Size( 20, 20 ) ),
					ResizeImage( new Bitmap( Resource1.delete ), new Size( 20, 20 ) ),
					ResizeImage( new Bitmap( Resource1.refresh ), new Size( 20, 20 ) )
				}
							   );

				var planState = _ctrl.GetPlanState( plan.Name );

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
			for( int i = 0; i < gridPlans.RowCount; i++ )
			{
				var r = gridPlans.Rows[i];
				string planName = ( string ) r.Cells[planTabColName].Value;
				var planState = _ctrl.GetPlanState( planName );
				if( planState != null )
				{
					r.Cells[planTabColStatus].Value = planState.OpStatus.ToString();

					// mark currently running plans with different background color
					var color = planState.Running ? Color.LightGoldenrodYellow : Color.White;
					r.DefaultCellStyle.BackColor = color;

					// put plan state into a tooltip
					{
						var planStatusCell = r.Cells[appTabColStatus]; // as DataGridViewCell;
						planStatusCell.ToolTipText = Tools.GetPlanStateString( planName, _ctrl.GetPlanState( planName ) );
					}
				}
				else
				{
					r.Cells[planTabColStatus].Value = "null";
					r.DefaultCellStyle.BackColor = Color.White;
				}
			}
		}

		private void gridPlans_CellFormatting( object sender, DataGridViewCellFormattingEventArgs e )
		{
			var cell = gridPlans.Rows[e.RowIndex].Cells[e.ColumnIndex];
			var defst = gridPlans.Rows[e.RowIndex].Cells[planTabColName].Style;
			if( e.ColumnIndex == planTabColStatus )
			{
				var txt = gridPlans.Rows[e.RowIndex].Cells[e.ColumnIndex].Value as string;
				if( txt.StartsWith( "Success" ) )
				{
					cell.Style = new DataGridViewCellStyle { ForeColor = Color.DarkGreen, SelectionForeColor = Color.LightGreen, BackColor = defst.BackColor };
				}
				else if( txt.StartsWith( "Failure" ) )
				{
					cell.Style = new DataGridViewCellStyle { ForeColor = Color.Red, SelectionForeColor = Color.Red, BackColor = defst.BackColor };
				}
				else if( txt.StartsWith( "InProgress" ) || txt.StartsWith( "Killing" ) )
				{
					cell.Style = new DataGridViewCellStyle { ForeColor = Color.Blue, SelectionForeColor = Color.Blue, BackColor = defst.BackColor };
				}
				else
				{
					cell.Style = defst;
				}
			}
		}

		private void gridPlans_MouseClick( object sender, MouseEventArgs e )
		{
			var hti = gridPlans.HitTest( e.X, e.Y );
			int currentRow = hti.RowIndex;
			int currentCol = hti.ColumnIndex;

			if( currentRow >= 0 ) // ignore header clicks
			{
				DataGridViewRow focused = gridPlans.Rows[currentRow];
				string planName = focused.Cells[0].Value as string;
				bool connected = IsConnected;

				if( e.Button == MouseButtons.Left )
				{
					// find the plan (by name)
					var plan = _planRepo.FirstOrDefault( p => p.Name == planName );

					// icon clicks
					if( currentCol == 2 ) // start
					{
						guardedOp( () => _ctrl.SelectPlan( plan.Name ) );
						guardedOp( () => _ctrl.StartPlan( plan.Name ) );
					}
					else if( currentCol == 3 ) // stop
					{
						guardedOp( () => _ctrl.SelectPlan( plan.Name ) );
						guardedOp( () => _ctrl.StopPlan( plan.Name ) );
					}
					else if( currentCol == 4 ) // kill
					{
						guardedOp( () => _ctrl.SelectPlan( plan.Name ) );
						guardedOp( () => _ctrl.KillPlan( plan.Name ) );
					}
					else if( currentCol == 5 ) // restart
					{
						guardedOp( () => _ctrl.SelectPlan( plan.Name ) );
						guardedOp( () => _ctrl.RestartPlan( plan.Name ) );
					}


				}
			}
		}

		// starts the doubleclicked plan
		private void gridPlans_MouseDoubleClick( object sender, MouseEventArgs e )
		{
			var hti = gridPlans.HitTest( e.X, e.Y );
			int currentRow = hti.RowIndex;
			int currentCol = hti.ColumnIndex;

			if( currentRow >= 0 ) // ignore header clicks
			{
				DataGridViewRow focused = gridPlans.Rows[currentRow];
				string planName = focused.Cells[0].Value as string;
				bool connected = IsConnected;

				if( e.Button == MouseButtons.Left )
				{
					// start the selected plan
					guardedOp( () => _ctrl.SelectPlan( planName ) );
					guardedOp( () => _ctrl.StartPlan( planName ) );
				}
			}
		}


	}
}
