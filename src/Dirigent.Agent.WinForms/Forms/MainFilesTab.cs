
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
		const int colName = 0;
		const int colStatus = 1;
		const int colMAX = 2;

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
			_dataTable.Columns.Add("Name", typeof(string));
			_dataTable.Columns.Add("Status", typeof(string));

			_bindingSource.DataMember = _dataSet.Tables[0].TableName;

			// fix columns appearance

			var _hdrScriptName = _grid.Columns[colName];
			_hdrScriptName.HeaderText = "Name";
			_hdrScriptName.MinimumWidth = 9;
			_hdrScriptName.ReadOnly = true;
			_hdrScriptName.Width = 250;

			var _Status = _grid.Columns[colStatus];
			_Status.HeaderText = "Status";
			_Status.MinimumWidth = 9;
			_Status.ReadOnly = true;
			_Status.Width = 175;

		}



		public void Refresh()
		{
			if( _bindingSource == null )
			{
				initGrid();
			}

		}

		public void CellFormatting( object sender, DataGridViewCellFormattingEventArgs e )
		{
		}

		public void MouseClick( object sender, MouseEventArgs e )
		{
			var hti = _grid.HitTest( e.X, e.Y );
			int currentRow = hti.RowIndex;
			int currentCol = hti.ColumnIndex;

			if( currentRow >= 0 ) // ignore header clicks
			{
				DataGridViewRow focusedGridRow = _grid.Rows[currentRow];
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
