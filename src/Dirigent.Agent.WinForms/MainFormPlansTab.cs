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

namespace Dirigent.Gui.WinForms
{
	public partial class frmMain : Form
	{
		const int planTabColName = 0;
		const int planTabColStatus = 1;
		const int planTabColIconStart = 2;
		const int planTabColIconStop = 3;
		const int planTabColIconKill = 4;
		const int planTabColIconRestart = 5;

        private BindingSource _gridPlansBindingSource = null;
		private DataTable _gridPlansDataTable = null;
        private DataSet _gridPlansDataSet = null;

		void initPlanGrid()
		{
			// when using DataTables the ADGV can properly filter rows
			_gridPlansBindingSource = new BindingSource();
			_gridPlansDataTable = new DataTable();
			_gridPlansDataSet = new DataSet();

			_gridPlansBindingSource.DataSource = _gridPlansDataSet;
			gridPlans.DataSource = _gridPlansBindingSource;

	        _gridPlansDataTable = _gridPlansDataSet.Tables.Add("PlansTable");
			_gridPlansDataTable.Columns.Add("Name", typeof(string));
			_gridPlansDataTable.Columns.Add("Status", typeof(PlanState.EOpStatus));
			_gridPlansDataTable.Columns.Add("IconStart", typeof(Bitmap));
			_gridPlansDataTable.Columns.Add("IconStop", typeof(Bitmap));
			_gridPlansDataTable.Columns.Add("IconKill", typeof(Bitmap));
			_gridPlansDataTable.Columns.Add("IconRestart", typeof(Bitmap));

			_gridPlansBindingSource.DataMember = _gridPlansDataSet.Tables[0].TableName;

			// fix columns appearance

			var _hdrPlanName = gridPlans.Columns[planTabColName];
			_hdrPlanName.HeaderText = "Plan Name";
			_hdrPlanName.MinimumWidth = 9;
			_hdrPlanName.ReadOnly = true;
			_hdrPlanName.Width = 250;

			var _Status = gridPlans.Columns[planTabColStatus];
			_Status.HeaderText = "Status";
			_Status.MinimumWidth = 9;
			_Status.ReadOnly = true;
			_Status.Width = 175;

			var _hdrPlanStart = gridPlans.Columns[planTabColIconStart];
			_hdrPlanStart.HeaderText = "";
			_hdrPlanStart.MinimumWidth = 9;
			_hdrPlanStart.ReadOnly = true;
			_hdrPlanStart.Width = 24;

			var _hdrPlanStop = gridPlans.Columns[planTabColIconStop];
			_hdrPlanStop.HeaderText = "";
			_hdrPlanStop.MinimumWidth = 9;
			_hdrPlanStop.ReadOnly = true;
			_hdrPlanStop.Width = 24;

			var _hdrPlanKill = gridPlans.Columns[planTabColIconKill];
			_hdrPlanKill.HeaderText = "";
			_hdrPlanKill.MinimumWidth = 9;
			_hdrPlanKill.ReadOnly = true;
			_hdrPlanKill.Width = 24;

			var _hdrPlanRestart = gridPlans.Columns[planTabColIconRestart];
			_hdrPlanRestart.HeaderText = "";
			_hdrPlanRestart.MinimumWidth = 9;
			_hdrPlanRestart.ReadOnly = true;
			_hdrPlanRestart.Width = 24;

			if( Common.Properties.Settings.Default.GridButtonSpacing > 0 ) 
			{
				_hdrPlanStart.Width = Common.Properties.Settings.Default.GridButtonSpacing;
				_hdrPlanStop.Width = Common.Properties.Settings.Default.GridButtonSpacing;
				_hdrPlanKill.Width = Common.Properties.Settings.Default.GridButtonSpacing;
				_hdrPlanRestart.Width = Common.Properties.Settings.Default.GridButtonSpacing;
			}
		}


		void populatePlanGrid()
		{
			if( _gridPlansBindingSource == null )
			{
				initPlanGrid();
			}
			else
			{
				_gridPlansDataTable.Rows.Clear();
			}

			foreach (var plan in _planRepo)
			{
				object[] newrow = new object[] {
					plan.Name,
					PlanState.EOpStatus.None,
					_iconStart,
					_iconStop,
					_iconKill,
					_iconRestart
				};
	            _gridPlansDataTable.Rows.Add(newrow);
			};
		}

		void updatePlansStatus()
		{
			for (int i = 0; i < gridPlans.RowCount; i++)
			{
				var r = gridPlans.Rows[i];
				var drv = r.DataBoundItem as DataRowView;
				var rowItems = drv.Row.ItemArray;

				string planName = (string)rowItems[planTabColName];

				var planState = _ctrl.GetPlanState( planName );
				if (planState != null)
				{
					drv.Row.SetField(planTabColStatus, planState.OpStatus);

					// note: origRow found in table is the same object as the drv.Row => no need to look it up
					//int inTableIndex = _gridPlansDataTable.Rows.IndexOf(drv.Row);
					//var origRow = _gridPlansDataTable.Rows[inTableIndex];
					//origRow.SetField(planTabColStatus, planState.OpStatus);

					// mark currently running plans with different background color
					var color = planState.Running ? Color.LightGoldenrodYellow : Color.White;
					r.DefaultCellStyle.BackColor = color;

					// put plan state into a tooltip
					{
						var planStatusCell = r.Cells[planTabColStatus]; // as DataGridViewCell;
						planStatusCell.ToolTipText = Tools.GetPlanStateString( planName, _ctrl.GetPlanState( planName ) );
					}
				}
				else
				{
					r.Cells[planTabColStatus].Value = PlanState.EOpStatus.None;
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
				var txt = (PlanState.EOpStatus) gridPlans.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
				if( txt == PlanState.EOpStatus.Success )
				{
					cell.Style = new DataGridViewCellStyle { ForeColor = Color.DarkGreen, SelectionForeColor = Color.LightGreen, BackColor = defst.BackColor };
				}
				else if( txt == PlanState.EOpStatus.Failure )
				{
					cell.Style = new DataGridViewCellStyle { ForeColor = Color.Red, SelectionForeColor = Color.Red, BackColor = defst.BackColor };
				}
				else if( txt == PlanState.EOpStatus.InProgress || txt == PlanState.EOpStatus.Killing )
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
					if( currentCol == planTabColIconStart ) // start
					{
						guardedOp( () => _currentPlan = _ctrl.GetPlanDef( plan.Name ) );
						guardedOp( () => _ctrl.Send( new Net.StartPlanMessage( _ctrl.Name, plan.Name )  ) );
					}
					else if( currentCol == planTabColIconStop ) // stop
					{
						guardedOp( () => _currentPlan = _ctrl.GetPlanDef( plan.Name ) );
						guardedOp( () => _ctrl.Send( new Net.StopPlanMessage( _ctrl.Name, plan.Name )  ) );
					}
					else if( currentCol == planTabColIconKill ) // kill
					{
						guardedOp( () => _currentPlan = _ctrl.GetPlanDef( plan.Name ) );
						guardedOp( () => _ctrl.Send( new Net.KillPlanMessage( _ctrl.Name, plan.Name )  ) );
					}
					else if( currentCol == planTabColIconRestart ) // restart
					{
						guardedOp( () => _currentPlan = _ctrl.GetPlanDef( plan.Name ) );
						guardedOp( () => _ctrl.Send( new Net.RestartPlanMessage( _ctrl.Name, plan.Name )  ) );
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
					guardedOp( () => _currentPlan = _ctrl.GetPlanDef( planName ) );
					guardedOp( () => _ctrl.Send( new Net.StartPlanMessage( _ctrl.Name, planName )  ) );
				}
			}
		}


	}
}
