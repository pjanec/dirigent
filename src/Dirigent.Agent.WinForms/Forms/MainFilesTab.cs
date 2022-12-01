
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
	public class MainFilesTab : MainExtension
	{
		const int colMachineId = 0;
		const int colAppId = 1;
		const int colId = 2;
		const int colPath = 3;
		const int colStatus = 4;
		const int colGuid = 5;
		const int colMAX = 6;

		private Zuby.ADGV.AdvancedDataGridView _grid;
        private BindingSource _bindingSource = null;
		private DataTable _dataTable = null;
        private DataSet _dataSet = null;

		public MainFilesTab(
			frmMain form,
			Zuby.ADGV.AdvancedDataGridView grid
			) : base( form )
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

	        _dataTable = _dataSet.Tables.Add("FilesTable");
			_dataTable.Columns.Add("MachineId", typeof(string));
			_dataTable.Columns.Add("AppId", typeof(string));
			_dataTable.Columns.Add("Id", typeof(string));
			_dataTable.Columns.Add("Path", typeof(string));
			_dataTable.Columns.Add("Status", typeof(string));
			_dataTable.Columns.Add("Guid", typeof(string));

			_bindingSource.DataMember = _dataSet.Tables[0].TableName;

			// fix columns appearance

			var _MachineId = _grid.Columns[colMachineId];
			_MachineId.HeaderText = "Machine";
			_MachineId.MinimumWidth = 9;
			_MachineId.ReadOnly = true;
			_MachineId.Width = 125;

			var _AppId = _grid.Columns[colAppId];
			_AppId.HeaderText = "App";
			_AppId.MinimumWidth = 9;
			_AppId.ReadOnly = true;
			_AppId.Width = 125;

			var _hdrScriptName = _grid.Columns[colId];
			_hdrScriptName.HeaderText = "Id";
			_hdrScriptName.MinimumWidth = 9;
			_hdrScriptName.ReadOnly = true;
			_hdrScriptName.Width = 175;

			var _Path = _grid.Columns[colPath];
			_Path.HeaderText = "Path";
			_Path.MinimumWidth = 9;
			_Path.ReadOnly = true;
			_Path.Width = 300;

			var _Status = _grid.Columns[colStatus];
			_Status.HeaderText = "Status";
			_Status.MinimumWidth = 9;
			_Status.ReadOnly = true;
			_Status.Width = 175;

			var _Guid = _grid.Columns[colGuid];
			_Guid.HeaderText = "Guid";
			_Guid.MinimumWidth = 9;
			_Guid.ReadOnly = true;
			_Guid.Width = 175;
			_Guid.Visible = false;
		}


		List<FileDef> _allFiles = new List<FileDef>();
		public void Refresh()
		{
			if( _bindingSource == null )
			{
				initGrid();
			}

			// check for new plans and update local copy/menu if they are different
			var newFiles = from x in Ctrl.GetAllVfsNodeDefs() where x is FileDef select x as FileDef;
			if( !newFiles.SequenceEqual( _allFiles ) )
			{
				_allFiles.Clear();
				_allFiles.AddRange( newFiles );

				_dataTable.Rows.Clear();
				foreach (var fd in _allFiles)
				{
					object[] newrow = new object[] {
						fd.MachineId,
						fd.AppId,
						fd.Id,
						fd.Path,
						"",
						fd.Guid,
					};
					_dataTable.Rows.Add(newrow);
				};
			}
		}

		public void CellFormatting( object sender, DataGridViewCellFormattingEventArgs e )
		{
		}

		private Guid getGuidFromMachsDataRow( DataRow dataRow )
		{
			var dataItems = dataRow.ItemArray;
			var guidStr = (string)dataItems[colGuid];
			return Guid.Parse(guidStr);
		}

		public void MouseClick( object sender, MouseEventArgs e )
		{
			var hti = _grid.HitTest( e.X, e.Y );
			int currentRow = hti.RowIndex;
			int currentCol = hti.ColumnIndex;

			if( currentRow >= 0 ) // ignore header clicks
			{
				DataGridViewRow focusedGridRow = _grid.Rows[currentRow];
				Guid guid = getGuidFromMachsDataRow( WFT.GetDataRowFromGridRow( focusedGridRow ) );

				if( e.Button == MouseButtons.Right )
				{
					// build popup menu
					var popup = new System.Windows.Forms.ContextMenuStrip( _form.Components );

					{
						var fileDef = ReflStates.GetVfsNodeDef( guid ) as FileDef;
						if ( fileDef != null )
						{
							var toolsMenu = ContextMenuFile( fileDef );
							if (toolsMenu != null)
							{
								popup.Items.Add( toolsMenu );
							}
						}
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
				DataGridViewRow focusedGridRow = _grid.Rows[currentRow];
			}
		}


	}
}
