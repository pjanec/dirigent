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
	public class MainPlansTab : MainExtension
	{
		const int colName = 0;
		const int colStatus = 1;
		const int colIconStart = 2;
		const int colIconStop = 3;
		const int colIconKill = 4;
		const int colIconRestart = 5;
		const int colMAX = 6;

		private Zuby.ADGV.AdvancedDataGridView _grid;
        private BindingSource _bindingSource = null;
		private DataTable _dataTable = null;
        private DataSet _dataSet = null;

		public MainPlansTab(
			frmMain form,
			GuiCore core,
			Zuby.ADGV.AdvancedDataGridView grid
			) : base(form, core )
		{
			_grid = grid;
		}

		void initGrid()
		{
			// when using DataTables the ADGV can properly filter rows
			_bindingSource = new BindingSource();
			_dataTable = new DataTable();
			_dataSet = new DataSet();

			_bindingSource.DataSource = _dataSet;
			_grid.DataSource = _bindingSource;

	        _dataTable = _dataSet.Tables.Add("PlansTable");
			_dataTable.Columns.Add("Name", typeof(string));
			_dataTable.Columns.Add("Status", typeof(PlanState.EOpStatus));
			_dataTable.Columns.Add("IconStart", typeof(Bitmap));
			_dataTable.Columns.Add("IconStop", typeof(Bitmap));
			_dataTable.Columns.Add("IconKill", typeof(Bitmap));
			_dataTable.Columns.Add("IconRestart", typeof(Bitmap));

			_bindingSource.DataMember = _dataSet.Tables[0].TableName;

			// fix columns appearance

			var _hdrPlanName = _grid.Columns[colName];
			_hdrPlanName.HeaderText = "Plan Name";
			_hdrPlanName.MinimumWidth = 9;
			_hdrPlanName.ReadOnly = true;
			_hdrPlanName.Width = 250;

			var _Status = _grid.Columns[colStatus];
			_Status.HeaderText = "Status";
			_Status.MinimumWidth = 9;
			_Status.ReadOnly = true;
			_Status.Width = 175;

			var _hdrPlanStart = _grid.Columns[colIconStart];
			_hdrPlanStart.HeaderText = "";
			_hdrPlanStart.MinimumWidth = 9;
			_hdrPlanStart.ReadOnly = true;
			_hdrPlanStart.Width = 24;
			_hdrPlanStart.ToolTipText = "Start";

			var _hdrPlanStop = _grid.Columns[colIconStop];
			_hdrPlanStop.HeaderText = "";
			_hdrPlanStop.MinimumWidth = 9;
			_hdrPlanStop.ReadOnly = true;
			_hdrPlanStop.Width = 24;
			_hdrPlanStop.ToolTipText = "Stop (leave apps)";

			var _hdrPlanKill = _grid.Columns[colIconKill];
			_hdrPlanKill.HeaderText = "";
			_hdrPlanKill.MinimumWidth = 9;
			_hdrPlanKill.ReadOnly = true;
			_hdrPlanKill.Width = 24;
			_hdrPlanKill.ToolTipText = "Kill Apps";

			var _hdrPlanRestart = _grid.Columns[colIconRestart];
			_hdrPlanRestart.HeaderText = "";
			_hdrPlanRestart.MinimumWidth = 9;
			_hdrPlanRestart.ReadOnly = true;
			_hdrPlanRestart.Width = 24;
			_hdrPlanRestart.ToolTipText = "Restart";

			if( Common.Properties.Settings.Default.GridButtonSpacing > 0 ) 
			{
				_hdrPlanStart.Width = Common.Properties.Settings.Default.GridButtonSpacing;
				_hdrPlanStop.Width = Common.Properties.Settings.Default.GridButtonSpacing;
				_hdrPlanKill.Width = Common.Properties.Settings.Default.GridButtonSpacing;
				_hdrPlanRestart.Width = Common.Properties.Settings.Default.GridButtonSpacing;
			}
		}


		public void Refresh()
		{
			// check for new plans and update local copy/menu if they are different
			var newPlanRepo = Ctrl.GetAllPlanDefs();
			if( !newPlanRepo.SequenceEqual( PlanRepo ) )
			{
				PlanRepo.Clear();
				PlanRepo.AddRange( newPlanRepo );

				populatePlanLists();
			}
			updatePlansStatus();
			UpdateToolTips();
		}

		void populatePlanLists()
		{
			_form.PopulatePlanSelectionMenu();
			populatePlanGrid();
		}

		void populatePlanGrid()
		{
			if( _bindingSource == null )
			{
				initGrid();
			}
			else
			{
				_dataTable.Rows.Clear();
			}

			foreach (var plan in PlanRepo)
			{
				object[] newrow = new object[] {
					plan.Name,
					PlanState.EOpStatus.None,
					_iconStart,
					_iconStop,
					_iconKill,
					_iconRestart
				};
	            _dataTable.Rows.Add(newrow);
			};
		}

		void updatePlansStatus()
		{
			for (int i = 0; i < _grid.RowCount; i++)
			{
				var r = _grid.Rows[i];
				var drv = r.DataBoundItem as DataRowView;
				var rowItems = drv.Row.ItemArray;

				string planName = (string)rowItems[colName];

				var planState = Ctrl.GetPlanState( planName );
				if (planState != null)
				{
					drv.Row.SetField(colStatus, planState.OpStatus);

					// note: origRow found in table is the same object as the drv.Row => no need to look it up
					//int inTableIndex = _gridPlansDataTable.Rows.IndexOf(drv.Row);
					//var origRow = _gridPlansDataTable.Rows[inTableIndex];
					//origRow.SetField(colStatus, planState.OpStatus);

					// mark currently running plans with different background color
					var color = planState.Running ? Color.LightGoldenrodYellow : Color.White;
					r.DefaultCellStyle.BackColor = color;

					// put plan state into a tooltip
					{
						var planStatusCell = r.Cells[colStatus]; // as DataGridViewCell;
						planStatusCell.ToolTipText = Tools.GetPlanStateString( planName, Ctrl.GetPlanState( planName ) );
					}
				}
				else
				{
					r.Cells[colStatus].Value = PlanState.EOpStatus.None;
					r.DefaultCellStyle.BackColor = Color.White;
				}
			}
		}

		void UpdateToolTips()
		{
			for (int i = 0; i < _grid.RowCount; i++)
			{
				var r = _grid.Rows[i];
				r.Cells[colIconStart].ToolTipText = _grid.Columns[colIconStart].ToolTipText;
				r.Cells[colIconStop].ToolTipText = _grid.Columns[colIconStop].ToolTipText;
				r.Cells[colIconKill].ToolTipText = _grid.Columns[colIconKill].ToolTipText;
				r.Cells[colIconRestart].ToolTipText = _grid.Columns[colIconRestart].ToolTipText;
			}
		}

		public void CellFormatting( object sender, DataGridViewCellFormattingEventArgs e )
		{
			var cell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
			var defst = _grid.Rows[e.RowIndex].Cells[colName].Style;
			if( e.ColumnIndex == colStatus )
			{
				var txt = (PlanState.EOpStatus) _grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
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

		public void MouseClick( object sender, MouseEventArgs e )
		{
			var hti = _grid.HitTest( e.X, e.Y );
			int currentRow = hti.RowIndex;
			int currentCol = hti.ColumnIndex;

			if( currentRow >= 0 ) // ignore header clicks
			{
				DataGridViewRow focused = _grid.Rows[currentRow];
				string planName = focused.Cells[0].Value as string;
				bool connected = Client.IsConnected;

				if( e.Button == MouseButtons.Left )
				{
					// find the plan (by name)
					var plan = PlanRepo.FirstOrDefault( p => p.Name == planName );

					// icon clicks
					if( currentCol == colIconStart ) // start
					{
						WFT.GuardedOp( () => CurrentPlan = Ctrl.GetPlanDef( plan.Name ) );
						WFT.GuardedOp( () => Ctrl.Send( new Net.StartPlanMessage( Ctrl.Name, plan.Name )  ) );
					}
					else if( currentCol == colIconStop ) // stop
					{
						WFT.GuardedOp( () => CurrentPlan = Ctrl.GetPlanDef( plan.Name ) );
						WFT.GuardedOp( () => Ctrl.Send( new Net.StopPlanMessage( Ctrl.Name, plan.Name )  ) );
					}
					else if( currentCol == colIconKill ) // kill
					{
						WFT.GuardedOp( () => CurrentPlan = Ctrl.GetPlanDef( plan.Name ) );
						WFT.GuardedOp( () => Ctrl.Send( new Net.KillPlanMessage( Ctrl.Name, plan.Name )  ) );
					}
					else if( currentCol == colIconRestart ) // restart
					{
						WFT.GuardedOp( () => CurrentPlan = Ctrl.GetPlanDef( plan.Name ) );
						WFT.GuardedOp( () => Ctrl.Send( new Net.RestartPlanMessage( Ctrl.Name, plan.Name )  ) );
					}


				}
			}
		}

		// starts the doubleclicked plan
		public void MouseDoubleClick( object sender, MouseEventArgs e )
		{
			var hti = _grid.HitTest( e.X, e.Y );
			int currentRow = hti.RowIndex;
			int currentCol = hti.ColumnIndex;

			if( currentRow >= 0 ) // ignore header clicks
			{
				DataGridViewRow focused = _grid.Rows[currentRow];
				string planName = focused.Cells[0].Value as string;
				bool connected = Client.IsConnected;

				if( e.Button == MouseButtons.Left )
				{
					// start the selected plan
					WFT.GuardedOp( () => CurrentPlan = Ctrl.GetPlanDef( planName ) );
					WFT.GuardedOp( () => Ctrl.Send( new Net.StartPlanMessage( Ctrl.Name, planName )  ) );
				}
			}
		}


	}
}
