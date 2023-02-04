
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
	public class MainScriptsTab : MainExtension
	{
		const int colName = 0;
		const int colStatus = 1;
		const int colDetails = 2;
		const int colIconStart = 3;
		const int colIconKill = 4;
		const int colId = 5;
		const int colMAX = 6;

		private Zuby.ADGV.AdvancedDataGridView _grid;
        private BindingSource _bindingSource = null;
		private DataTable _dataTable = null;
        private DataSet _dataSet = null;

		public MainScriptsTab(
			frmMain form,
			GuiCore core,
			Zuby.ADGV.AdvancedDataGridView grid
			) : base(form, core)
		{
			_grid = grid;
		}

		public override void Reset()
		{
			base.Reset();
			if( _dataTable != null )
			{
				_dataTable.Rows.Clear();
			}
		}

		void initGrid()
		{
			// when using DataTables the ADGV can properly filter rows
			_bindingSource = new BindingSource();
			_dataTable = new DataTable();
			_dataSet = new DataSet();

			_bindingSource.DataSource = _dataSet;
			_grid.DataSource = _bindingSource;

	        _dataTable = _dataSet.Tables.Add("ScriptsTable");
			_dataTable.Columns.Add("Name", typeof(string));
			_dataTable.Columns.Add("Status", typeof(string));
			_dataTable.Columns.Add("Description", typeof(string));
			_dataTable.Columns.Add("IconStart", typeof(Bitmap));
			_dataTable.Columns.Add("IconKill", typeof(Bitmap));
			_dataTable.Columns.Add("Id", typeof(Guid));

			_bindingSource.DataMember = _dataSet.Tables[0].TableName;

			// fix columns appearance

			var _hdrScriptName = _grid.Columns[colName];
			_hdrScriptName.HeaderText = "Script Name";
			_hdrScriptName.MinimumWidth = 9;
			_hdrScriptName.ReadOnly = true;
			_hdrScriptName.Width = 250;

			var _Status = _grid.Columns[colStatus];
			_Status.HeaderText = "Status";
			_Status.MinimumWidth = 9;
			_Status.ReadOnly = true;
			_Status.Width = 80;

			var _Details = _grid.Columns[colDetails];
			_Details.HeaderText = "Details";
			_Details.MinimumWidth = 9;
			_Details.ReadOnly = true;
			_Details.Width = 350;

			var _hdrScriptStart = _grid.Columns[colIconStart];
			_hdrScriptStart.HeaderText = "";
			_hdrScriptStart.MinimumWidth = 9;
			_hdrScriptStart.ReadOnly = true;
			_hdrScriptStart.Width = 24;
			_hdrScriptStart.ToolTipText = "Start";

			var _hdrScriptKill = _grid.Columns[colIconKill];
			_hdrScriptKill.HeaderText = "";
			_hdrScriptKill.MinimumWidth = 9;
			_hdrScriptKill.ReadOnly = true;
			_hdrScriptKill.Width = 24;
			_hdrScriptKill.ToolTipText = "Kill";

			var _hdrScriptId = _grid.Columns[colId];
			_hdrScriptId.HeaderText = "Script Id";
			_hdrScriptId.MinimumWidth = 9;
			_hdrScriptId.ReadOnly = true;
			_hdrScriptId.Width = 100;
			_hdrScriptId.Visible = false;

			if( Common.Properties.Settings.Default.GridButtonSpacing > 0 ) 
			{
				_hdrScriptStart.Width = Common.Properties.Settings.Default.GridButtonSpacing;
				_hdrScriptKill.Width = Common.Properties.Settings.Default.GridButtonSpacing;
			}
		}


		public void Refresh()
		{
			// check for new plans and update local copy/menu if they are different
			var newScriptRepo = Ctrl.GetAllScriptDefs();
			if( !newScriptRepo.SequenceEqual( ScriptRepo ) )
			{
				ScriptRepo.Clear();
				ScriptRepo.AddRange( newScriptRepo );

				populateScriptGrid();
			}
			updateScriptsStatus();
			UpdateToolTips();
		}

		void populateScriptGrid()
		{
			if( _bindingSource == null )
			{
				initGrid();
			}
			else
			{
				_dataTable.Rows.Clear();
			}

			foreach (var script in ScriptRepo)
			{
				object[] newrow = new object[colMAX];
				
				newrow[colName] = script.Title;
				newrow[colStatus] = "";
				newrow[colDetails] = "";
				newrow[colIconStart] = _iconStart;
				newrow[colIconKill] = _iconKill;
				newrow[colId] = script.Id;
				
	            _dataTable.Rows.Add(newrow);
			};

		}

		void UpdateToolTips()
		{
			for (int i = 0; i < _grid.RowCount; i++)
			{
				var r = _grid.Rows[i];
				r.Cells[colIconStart].ToolTipText = _grid.Columns[colIconStart].ToolTipText;
				r.Cells[colIconKill].ToolTipText = _grid.Columns[colIconKill].ToolTipText;
			}
		}

		void updateScriptsStatus()
		{
			for (int i = 0; i < _grid.RowCount; i++)
			{
				var r = _grid.Rows[i];
				var drv = r.DataBoundItem as DataRowView;
				var rowItems = drv.Row.ItemArray;

				var scriptId = (Guid)rowItems[colId];
					
				var scriptState = Ctrl.GetScriptState( scriptId );
				if (scriptState != null)
				{
					var statusStr = scriptState.Status.ToString();
					var textStr = "";
					if (scriptState.Status == EScriptStatus.Running)
					{
						textStr = scriptState.Text;
					}
					else
					if (scriptState.Status == EScriptStatus.Failed)
					{
						var scriptExcept = Tools.Deserialize<SerializedException>( scriptState.Data );
						textStr = scriptExcept.Message;
					}
					

					drv.Row.SetField(colStatus, statusStr);
					drv.Row.SetField(colDetails, textStr);

					//// mark currently running scripts with different background color
					//var color = scriptState.StatusText == "Running" ? Color.LightGoldenrodYellow : Color.White;
					//r.DefaultCellStyle.BackColor = color;

					//// put plan state into a tooltip
					//{
					//	var planStatusCell = r.Cells[colStatus]; // as DataGridViewCell;
					//	planStatusCell.ToolTipText = Tools.GetPlanStateString( planName, _ctrl.GetPlanState( planName ) );
					//}
				}
				else
				{
					drv.Row.SetField(colStatus, string.Empty);
					r.DefaultCellStyle.BackColor = Color.White;
				}
			}
		}

		public void CellFormatting( object sender, DataGridViewCellFormattingEventArgs e )
		{
			var gridRow = _grid.Rows[e.RowIndex];
			var dataRow = WFT.GetDataRowFromGridRow( gridRow );
			var dataItems = dataRow.ItemArray;

			var cell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
			var defst = _grid.Rows[e.RowIndex].Cells[colStatus].Style;
			if( e.ColumnIndex == colStatus )
			{
				var txt = dataItems[colStatus] as string;
				if( txt.StartsWith( "Run" ) )
				{
					cell.Style = new DataGridViewCellStyle { ForeColor = Color.DarkGreen, SelectionForeColor = Color.LightGreen, BackColor = defst.BackColor };
				}
				else if( txt.StartsWith( "Cancel" ) )
				{
					cell.Style = new DataGridViewCellStyle { ForeColor = Color.DarkViolet, SelectionForeColor = Color.DarkViolet, BackColor = defst.BackColor };
				}
				else if( txt.StartsWith( "Fail" ) )
				{
					cell.Style = new DataGridViewCellStyle { ForeColor = Color.Red, SelectionForeColor = Color.Red, BackColor = defst.BackColor };
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
				Guid scriptId = (Guid)focused.Cells[colId].Value;
				bool connected = Client.IsConnected;

				var script = ScriptRepo.FirstOrDefault( p => p.Guid == scriptId );

				if( e.Button == MouseButtons.Left )
				{

					// icon clicks
					if( currentCol == colIconStart ) // start
					{
						WFT.GuardedOp( () => Ctrl.Send( new Net.StartScriptMessage( Ctrl.Name, script.Guid, script.Args )  ) );
					}
					else if( currentCol == colIconKill ) // kill
					{
						WFT.GuardedOp( () => Ctrl.Send( new Net.KillScriptMessage( Ctrl.Name, script.Guid )  ) );
					}
				}

				if( e.Button == MouseButtons.Right )
				{
					// build popup menu
					var popup = new System.Windows.Forms.ContextMenuStrip( _form.Components );
					popup.Enabled = connected || _core.AllowLocalIfDisconnected;

					{
						var launchItem = new System.Windows.Forms.ToolStripMenuItem( "&Launch" );
						launchItem.Click += ( s, a ) => WFT.GuardedOp( () => Ctrl.Send( new Net.StartScriptMessage(
							Ctrl.Name,
							script.Guid,
							script.Args
						)));
						//launchItem.Enabled = isAccessible && !st.Running;
						popup.Items.Add( launchItem );
					}

					{
						var killItem = new System.Windows.Forms.ToolStripMenuItem( "&Kill" );
						killItem.Click += ( s, a ) => WFT.GuardedOp( () => Ctrl.Send( new Net.KillScriptMessage( Ctrl.Name, script.Guid ) ) );
						//killItem.Enabled = isAccessible && ( st.Running || st.Restarting );
						popup.Items.Add( killItem );
					}

					{
						var separator = new System.Windows.Forms.ToolStripSeparator();
						popup.Items.Add( separator );
					}

					{
						var propsWindowItem = new System.Windows.Forms.ToolStripMenuItem( "&Properties" );
						propsWindowItem.Click += ( s, a ) => WFT.GuardedOp( () => 
						{
							var scriptDef = Ctrl.GetScriptDef( script.Guid );
							var frm = new frmScriptProperties( scriptDef );
							frm.Show();
						});
						//propsWindowItem.Enabled = true;
						popup.Items.Add( propsWindowItem );
					}

					popup.Show( Cursor.Position );
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
				Guid scriptId = (Guid)focused.Cells[colId].Value;
				bool connected = Client.IsConnected;

				if( e.Button == MouseButtons.Left )
				{
					var script = ScriptRepo.FirstOrDefault( p => p.Guid == scriptId );

					// start clicked script
					WFT.GuardedOp( () => Ctrl.Send( new Net.StartScriptMessage( Ctrl.Name, script.Guid, script.Args )  ) );
				}
			}
		}


	}
}
