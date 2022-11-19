
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
		const int machTabColName = 0;
		const int machTabColStatus = 1;
		const int machTabNumCols = 2;

        private BindingSource _gridMachsBindingSource = null;
		private DataTable _gridMachsDataTable = null;
        private DataSet _gridMachsDataSet = null;

		void initMachsGrid()
		{
			// when using DataTables the ADGV can properly filter rows
			_gridMachsBindingSource = new BindingSource();
			_gridMachsDataTable = new DataTable();
			_gridMachsDataSet = new DataSet();

			_gridMachsBindingSource.DataSource = _gridMachsDataSet;
			gridMachs.DataSource = _gridMachsBindingSource;

	        _gridMachsDataTable = _gridMachsDataSet.Tables.Add("MachinesTable");
			_gridMachsDataTable.Columns.Add("Name", typeof(string));
			_gridMachsDataTable.Columns.Add("Status", typeof(string));

			_gridMachsBindingSource.DataMember = _gridMachsDataSet.Tables[0].TableName;

			// fix columns appearance

			var _hdrScriptName = gridMachs.Columns[machTabColName];
			_hdrScriptName.HeaderText = "Name";
			_hdrScriptName.MinimumWidth = 9;
			_hdrScriptName.ReadOnly = true;
			_hdrScriptName.Width = 250;

			var _Status = gridMachs.Columns[machTabColStatus];
			_Status.HeaderText = "Status";
			_Status.MinimumWidth = 9;
			_Status.ReadOnly = true;
			_Status.Width = 175;

		}


		private string getClientIdFromMachsDataRow( DataRow dataRow )
		{
			var dataItems = dataRow.ItemArray;
			var id = (string)dataItems[machTabColName];
			return id;
		}
		
		private bool isMachineId( string clientId )
		{
			// guid strings are used for non-machine clients
			if( Guid.TryParse(clientId, out var parsedGuid) )
			{
				return false;
			}
			return true;
		}


		void refreshMachs()
		{
			if( _gridMachsBindingSource == null )
			{
				initMachsGrid();
			}

			// find what has changed
			// - list new client info
			// - find differences with current grid data table (added/remove/updated)
			// - aply differences

			var oldRows = new Dictionary<string, DataRow>();
			var toAdd = new List<object[]>();
			var toRemove = new List<DataRow>();

			foreach( DataRow dataRow in _gridMachsDataTable.Rows )
			{
				var id = getClientIdFromMachsDataRow( dataRow );

				oldRows[id] = dataRow;
			}


			foreach (var (id, state) in _reflStates.GetAllClientStates())
			{
				if( oldRows.ContainsKey( id )) continue; // already existing
				if( !isMachineId( id ) ) continue; // ignore non-machine clients

				var item = new object[machTabNumCols];
				item[machTabColName] = id;
				item[machTabColStatus] = Tools.GetClientStateText( state );
				toAdd.Add( item );
			}

			foreach( var dataRow in toRemove )
			{
				_gridMachsDataTable.Rows.Remove( dataRow );
			}

			foreach( var newrow in toAdd )
			{
				_gridMachsDataTable.Rows.Add( newrow );
			}

			// update existing
			foreach( var (id, dataRow) in oldRows )
			{
				if( toRemove.Contains( dataRow ) ) continue;

				var state = _ctrl.GetClientState( id );
				dataRow.SetField( machTabColStatus, Tools.GetClientStateText( state ) );
			}
		}

		private void gridMachs_CellFormatting( object sender, DataGridViewCellFormattingEventArgs e )
		{
		}

		private void gridMachs_MouseClick( object sender, MouseEventArgs e )
		{
			var hti = gridMachs.HitTest( e.X, e.Y );
			int currentRow = hti.RowIndex;
			int currentCol = hti.ColumnIndex;

			if( currentRow >= 0 ) // ignore header clicks
			{
				DataGridViewRow focusedGridRow = gridMachs.Rows[currentRow];
				string id = getClientIdFromMachsDataRow( getDataRowFromGridRow( focusedGridRow ) );
				bool connected = IsConnected;


				//if( e.Button == MouseButtons.Left )
				//{
				//}

				if( e.Button == MouseButtons.Right )
				{
					// build popup menu
					var popup = new System.Windows.Forms.ContextMenuStrip( this.components );
					popup.Enabled = connected || _allowLocalIfDisconnected;

					{
						var item = new System.Windows.Forms.ToolStripMenuItem( "&Folders" );
						// TODO: generate subitems
						popup.Items.Add( item );
					}

					{
						var item = new System.Windows.Forms.ToolStripMenuItem( "&Files" );
						// TODO: generate subitems
						popup.Items.Add( item );
					}

					popup.Show( Cursor.Position );
				}
			}
		}

		// starts the doubleclicked plan
		private void gridMachs_MouseDoubleClick( object sender, MouseEventArgs e )
		{
			var hti = gridMachs.HitTest( e.X, e.Y );
			int currentRow = hti.RowIndex;
			int currentCol = hti.ColumnIndex;

			if( currentRow >= 0 ) // ignore header clicks
			{
				DataGridViewRow focusedGridRow = gridMachs.Rows[currentRow];
				string id = getClientIdFromMachsDataRow( getDataRowFromGridRow( focusedGridRow ) );
				bool connected = IsConnected;

				//if( e.Button == MouseButtons.Left )
				//{
				//}
			}
		}


	}
}
