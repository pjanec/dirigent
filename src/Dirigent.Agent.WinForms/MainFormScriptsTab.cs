
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
		const int scriptTabColName = 0;
		const int scriptTabColStatus = 1;
		const int scriptTabColIconStart = 2;
		const int scriptTabColIconKill = 3;

        private BindingSource _gridScriptsBindingSource = null;
		private DataTable _gridScriptsDataTable = null;
        private DataSet _gridScriptsDataSet = null;

		void initScriptGrid()
		{
			// when using DataTables the ADGV can properly filter rows
			_gridScriptsBindingSource = new BindingSource();
			_gridScriptsDataTable = new DataTable();
			_gridScriptsDataSet = new DataSet();

			_gridScriptsBindingSource.DataSource = _gridScriptsDataSet;
			gridScripts.DataSource = _gridScriptsBindingSource;

	        _gridScriptsDataTable = _gridScriptsDataSet.Tables.Add("ScriptsTable");
			_gridScriptsDataTable.Columns.Add("Name", typeof(string));
			_gridScriptsDataTable.Columns.Add("Status", typeof(string));
			_gridScriptsDataTable.Columns.Add("IconStart", typeof(Bitmap));
			_gridScriptsDataTable.Columns.Add("IconKill", typeof(Bitmap));

			_gridScriptsBindingSource.DataMember = _gridScriptsDataSet.Tables[0].TableName;

			// fix columns appearance

			var _hdrScriptName = gridScripts.Columns[scriptTabColName];
			_hdrScriptName.HeaderText = "Script Name";
			_hdrScriptName.MinimumWidth = 9;
			_hdrScriptName.ReadOnly = true;
			_hdrScriptName.Width = 250;

			var _Status = gridScripts.Columns[scriptTabColStatus];
			_Status.HeaderText = "Status";
			_Status.MinimumWidth = 9;
			_Status.ReadOnly = true;
			_Status.Width = 175;

			var _hdrScriptStart = gridScripts.Columns[scriptTabColIconStart];
			_hdrScriptStart.HeaderText = "";
			_hdrScriptStart.MinimumWidth = 9;
			_hdrScriptStart.ReadOnly = true;
			_hdrScriptStart.Width = 24;

			var _hdrScriptKill = gridScripts.Columns[scriptTabColIconKill];
			_hdrScriptKill.HeaderText = "";
			_hdrScriptKill.MinimumWidth = 9;
			_hdrScriptKill.ReadOnly = true;
			_hdrScriptKill.Width = 24;

			if( Common.Properties.Settings.Default.GridButtonSpacing > 0 ) 
			{
				_hdrScriptStart.Width = Common.Properties.Settings.Default.GridButtonSpacing;
				_hdrScriptKill.Width = Common.Properties.Settings.Default.GridButtonSpacing;
			}
		}


		void refreshScripts()
		{
			// check for new plans and update local copy/menu if they are different
			var newScriptRepo = _ctrl.GetAllScriptDefs();
			if( !newScriptRepo.SequenceEqual( _scriptRepo ) )
			{
				_scriptRepo = new List<ScriptDef>( newScriptRepo );
				populateScriptGrid();
			}
			updateScriptsStatus();
		}

		void populateScriptGrid()
		{
			if( _gridScriptsBindingSource == null )
			{
				initScriptGrid();
			}
			else
			{
				_gridScriptsDataTable.Rows.Clear();
			}

			foreach (var script in _scriptRepo)
			{
				object[] newrow = new object[] {
					script.Id,
					"",
					_iconStart,
					_iconKill,
				};
	            _gridScriptsDataTable.Rows.Add(newrow);
			};
		}

		void updateScriptsStatus()
		{
			for (int i = 0; i < gridScripts.RowCount; i++)
			{
				var r = gridScripts.Rows[i];
				var drv = r.DataBoundItem as DataRowView;
				var rowItems = drv.Row.ItemArray;

				string scriptId = (string)rowItems[scriptTabColName];

				var scriptState = _ctrl.GetScriptState( scriptId );
				if (scriptState != null)
				{
					drv.Row.SetField(scriptTabColStatus, scriptState.StatusText);

					//// mark currently running scripts with different background color
					//var color = scriptState.StatusText == "Running" ? Color.LightGoldenrodYellow : Color.White;
					//r.DefaultCellStyle.BackColor = color;

					//// put plan state into a tooltip
					//{
					//	var planStatusCell = r.Cells[scriptTabColStatus]; // as DataGridViewCell;
					//	planStatusCell.ToolTipText = Tools.GetPlanStateString( planName, _ctrl.GetPlanState( planName ) );
					//}
				}
				else
				{
					r.Cells[scriptTabColStatus].Value = string.Empty;
					r.DefaultCellStyle.BackColor = Color.White;
				}
			}
		}

		private void gridScripts_CellFormatting( object sender, DataGridViewCellFormattingEventArgs e )
		{
			//var cell = gridScripts.Rows[e.RowIndex].Cells[e.ColumnIndex];
			//var defst = gridScripts.Rows[e.RowIndex].Cells[scriptTabColName].Style;
			//if( e.ColumnIndex == scriptTabColStatus )
			//{
			//	var txt = gridScripts.Rows[e.RowIndex].Cells[e.ColumnIndex].Value;
			//	if( txt == "Success" )
			//	{
			//		cell.Style = new DataGridViewCellStyle { ForeColor = Color.DarkGreen, SelectionForeColor = Color.LightGreen, BackColor = defst.BackColor };
			//	}
			//	else if( txt == "Failure" )
			//	{
			//		cell.Style = new DataGridViewCellStyle { ForeColor = Color.Red, SelectionForeColor = Color.Red, BackColor = defst.BackColor };
			//	}
			//	else if( txt == "InProgress" || txt == "Killing" )
			//	{
			//		cell.Style = new DataGridViewCellStyle { ForeColor = Color.Blue, SelectionForeColor = Color.Blue, BackColor = defst.BackColor };
			//	}
			//	else
			//	{
			//		cell.Style = defst;
			//	}
			//}
		}

		private void gridScripts_MouseClick( object sender, MouseEventArgs e )
		{
			var hti = gridScripts.HitTest( e.X, e.Y );
			int currentRow = hti.RowIndex;
			int currentCol = hti.ColumnIndex;

			if( currentRow >= 0 ) // ignore header clicks
			{
				DataGridViewRow focused = gridScripts.Rows[currentRow];
				string scriptId = focused.Cells[0].Value as string;
				bool connected = IsConnected;

				var script = _scriptRepo.FirstOrDefault( p => p.Id == scriptId );

				if( e.Button == MouseButtons.Left )
				{

					// icon clicks
					if( currentCol == scriptTabColIconStart ) // start
					{
						guardedOp( () => _ctrl.Send( new Net.StartScriptMessage( _ctrl.Name, script.Id, script.Args )  ) );
					}
					else if( currentCol == scriptTabColIconKill ) // kill
					{
						guardedOp( () => _ctrl.Send( new Net.KillScriptMessage( _ctrl.Name, script.Id )  ) );
					}
				}

				if( e.Button == MouseButtons.Right )
				{
					// build popup menu
					var popup = new System.Windows.Forms.ContextMenuStrip( this.components );
					popup.Enabled = connected || _allowLocalIfDisconnected;

					{
						var launchItem = new System.Windows.Forms.ToolStripMenuItem( "&Launch" );
						launchItem.Click += ( s, a ) => guardedOp( () => _ctrl.Send( new Net.StartScriptMessage(
							_ctrl.Name,
							script.Id,
							script.Args
						)));
						//launchItem.Enabled = isAccessible && !st.Running;
						popup.Items.Add( launchItem );
					}

					{
						var killItem = new System.Windows.Forms.ToolStripMenuItem( "&Kill" );
						killItem.Click += ( s, a ) => guardedOp( () => _ctrl.Send( new Net.KillScriptMessage( _ctrl.Name, script.Id ) ) );
						//killItem.Enabled = isAccessible && ( st.Running || st.Restarting );
						popup.Items.Add( killItem );
					}

					{
						var separator = new System.Windows.Forms.ToolStripSeparator();
						popup.Items.Add( separator );
					}

					{
						var propsWindowItem = new System.Windows.Forms.ToolStripMenuItem( "&Properties" );
						propsWindowItem.Click += ( s, a ) => guardedOp( () => 
						{
							var scriptDef = _ctrl.GetScriptDef( script.Id );
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
		private void gridScripts_MouseDoubleClick( object sender, MouseEventArgs e )
		{
			var hti = gridScripts.HitTest( e.X, e.Y );
			int currentRow = hti.RowIndex;
			int currentCol = hti.ColumnIndex;

			if( currentRow >= 0 ) // ignore header clicks
			{
				DataGridViewRow focused = gridScripts.Rows[currentRow];
				string scriptId = focused.Cells[0].Value as string;
				bool connected = IsConnected;

				if( e.Button == MouseButtons.Left )
				{
					var script = _scriptRepo.FirstOrDefault( p => p.Id == scriptId );

					// start clicked script
					guardedOp( () => _ctrl.Send( new Net.StartScriptMessage( _ctrl.Name, script.Id, script.Args )  ) );
				}
			}
		}


	}
}
