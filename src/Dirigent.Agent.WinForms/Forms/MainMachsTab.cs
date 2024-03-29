﻿
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
	public class MainMachsTab : MainExtension
	{
		const int colName = 0;
		const int colStatus = 1;
		const int colAddress = 2;
		const int colCPU = 3;
		const int colMemory = 4;
		const int colMAX = 5;

		private Zuby.ADGV.AdvancedDataGridView _grid;
        private BindingSource _bindingSource = null;
		private DataTable _dataTable = null;
        private DataSet _dataSet = null;

		public MainMachsTab(
			frmMain form,
			GuiCore core,
			Zuby.ADGV.AdvancedDataGridView grid
			) : base( form, core )
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

	        _dataTable = _dataSet.Tables.Add("MachinesTable");
			_dataTable.Columns.Add("Name", typeof(string));
			_dataTable.Columns.Add("Status", typeof(string));
			_dataTable.Columns.Add("Address", typeof(string));
			_dataTable.Columns.Add("CPU", typeof(string));
			_dataTable.Columns.Add("Mem", typeof(string));

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

			var _Address = _grid.Columns[colAddress];
			_Address.HeaderText = "Address";
			_Address.MinimumWidth = 9;
			_Address.ReadOnly = true;
			_Address.Width = 100;

			var _CPU = _grid.Columns[colCPU];
			_CPU.HeaderText = "CPU";
			_CPU.MinimumWidth = 9;
			_CPU.ReadOnly = true;
			_CPU.Width = 40;

			var _MemAvail = _grid.Columns[colMemory];
			_MemAvail.HeaderText = "Memory";
			_MemAvail.MinimumWidth = 9;
			_MemAvail.ReadOnly = true;
			_MemAvail.Width = 120;

		}


		private string getClientIdFromMachsDataRow( DataRow dataRow )
		{
			var dataItems = dataRow.ItemArray;
			var id = (string)dataItems[colName];
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


		public void Refresh()
		{
			if( _bindingSource == null )
			{
				initGrid();
			}

			// we need to display shared-config defined machines (always no matter if connected or not) and also other machines that are connected to the agent

			// find what has changed
			// - list new client info
			// - find differences with current grid data table (added/remove/updated)
			// - aply differences

			var oldRows = new Dictionary<string, DataRow>();
			var toAdd = new List<object[]>();
			var toRemove = new List<DataRow>();

			foreach( DataRow dataRow in _dataTable.Rows )
			{
				var id = getClientIdFromMachsDataRow( dataRow );

				oldRows[id] = dataRow;
			}


			void SetStats( DataRow dr, string machineId )
			{
				var st = ReflStates.GetMachineState( machineId );
				
				if( st == null )
				{
					dr.SetField( colCPU, "" );
					dr.SetField( colMemory, "" );
				}
				else
				{
					dr.SetField( colCPU, $"{(st == null ? 0 : (int) st.CPU)}%" );

					var totalMB = st.MemoryTotalMB;
					var availMB = st.MemoryAvailMB;
					var usedMB = totalMB - availMB;
					dr.SetField( colMemory, $"{Tools.HumanReadableSizeOutOf( (ulong)((double)usedMB*1024*1024),  (ulong)((double)totalMB*1024*1024) )} ({(int)(usedMB/totalMB*100)}%)" );
				}
			}
			

			// add connected machines
			foreach (var (id, state) in ReflStates.GetAllClientStates())
			{
				if( oldRows.ContainsKey( id )) continue; // already existing
				if( !isMachineId( id ) ) continue; // ignore non-machine clients

				var item = new object[colMAX];
				item[colName] = id;
				item[colStatus] = Tools.GetClientStateText( state );
				item[colAddress] = $"{state.IP}";
				toAdd.Add( item );
			}

			// combine with predefined machines
			foreach (var mach in ReflStates.GetAllMachineDefs())
			{
				var id = mach.Id;
				if( oldRows.ContainsKey( id )) continue; // already existing
				if( toAdd.Find( x => (string)x[colName] == id ) != null ) continue; // already added as connected client

				// here we already know it is not connected
				var item = new object[colMAX];
				item[colName] = id;
				item[colStatus] = Tools.GetClientStateText( null );
				item[colAddress] = mach.IP;
				toAdd.Add( item );
			}



			foreach( var dataRow in toRemove )
			{
				_dataTable.Rows.Remove( dataRow );
			}

			foreach( var newrow in toAdd )
			{
				_dataTable.Rows.Add( newrow );
			}

			// update existing
			foreach( var (id, dataRow) in oldRows )
			{
				if( toRemove.Contains( dataRow ) ) continue;

				var state = Ctrl.GetClientState( id );
				dataRow.SetField( colStatus, Tools.GetClientStateText( state ) );
			}

			// update stats
			foreach( DataRow dataRow in _dataTable.Rows )
			{
				var id = getClientIdFromMachsDataRow( dataRow );
				SetStats( dataRow, id );
			}
			
		}

		public void CellFormatting( object sender, DataGridViewCellFormattingEventArgs e )
		{
			var gridRow = _grid.Rows[e.RowIndex];
			var dataRow = WFT.GetDataRowFromGridRow( gridRow );
			var dataItems = dataRow.ItemArray;
			//var id = getClientIdFromMachsDataRow( dataRow );

			var cell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
			var defst = _grid.Rows[e.RowIndex].Cells[colStatus].Style;
			if( e.ColumnIndex == colStatus )
			{
				var txt = dataItems[colStatus] as string;
				if( txt.StartsWith( "Online" ) )
				{
					cell.Style = new DataGridViewCellStyle { ForeColor = Color.DarkGreen, SelectionForeColor = Color.LightGreen, BackColor = defst.BackColor };
				}
				else if( txt.StartsWith( "Offline" ) )
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
				DataGridViewRow focusedGridRow = _grid.Rows[currentRow];
				string id = getClientIdFromMachsDataRow( WFT.GetDataRowFromGridRow( focusedGridRow ) );
				bool connected = Client.IsConnected;


				//if( e.Button == MouseButtons.Left )
				//{
				//}

				if( e.Button == MouseButtons.Right )
				{
					// build popup menu
					var popup = new ContextMenuStrip( _form.Components );

					{
						var powerMenu = new ToolStripMenuItem( "Power" );
						popup.Items.Add( powerMenu );

						var rebootMenu = new ToolStripMenuItem( "Reboot" );
						rebootMenu.Click += ( s, a ) => WFT.GuardedOp( () =>
						{
							if( MessageBox.Show( $"Reboot machine {id}?", "Dirigent", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning ) == DialogResult.OK )
							{
								var args = new ShutdownArgs() { Mode = EShutdownMode.Reboot };
								Ctrl.Send( new Net.ShutdownMessage( Ctrl.Name, args, id ) );
							}
						});
						powerMenu.DropDownItems.Add( rebootMenu );

						var shutdownMenu = new ToolStripMenuItem( "Shut down" );
						shutdownMenu.Click += ( s, a ) => WFT.GuardedOp( () =>
						{
							if( MessageBox.Show( $"Shut down machine {id}?", "Dirigent", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning ) == DialogResult.OK )
							{
								var args = new ShutdownArgs() { Mode = EShutdownMode.PowerOff };
								Ctrl.Send( new Net.ShutdownMessage( Ctrl.Name, args, id ) );
							}
						});
						powerMenu.DropDownItems.Add( shutdownMenu );

						var machDef = ReflStates.GetMachineDef( id );
						if( machDef != null && !string.IsNullOrEmpty(machDef.MAC) )
						{
							var wakeUpMenu = new ToolStripMenuItem( "Wake Up" );
							wakeUpMenu.Click += ( s, a ) => WFT.GuardedOp( () =>
							{
								Tools.SendWakeOnLanMagicPacket( machDef.MAC );
							} );
							powerMenu.DropDownItems.Add( wakeUpMenu );
						}
					}

					// File/Folder/Package menu items
					{
						if( isMachineId( id ) )
						{
							var machDef = ReflStates.GetMachineDef( id );
							if( machDef != null )
							{
								var vfsNodesMenu = _menuBuilder.BuildVfsNodesMenuItems( machDef.VfsNodes );
								if ( vfsNodesMenu.Count > 0 )
								{
									popup.Items.Add( new ToolStripSeparator() );
								}
								foreach( var item in vfsNodesMenu )
								{
									popup.Items.AddRange( WFT.MenuItemToToolStrips(item) );
								}
							}
						}
					}

					// tools menu items
					{
						if( isMachineId( id ) )
						{
							var machDef = ReflStates.GetMachineDef( id );
							if( machDef != null )
							{
								var actionMenuItems = _menuBuilder.BuildMachineActionsMenuItems( machDef );
								if ( actionMenuItems.Count > 0 )
								{
									popup.Items.Add( new ToolStripSeparator() );
								}
								foreach ( var item in actionMenuItems )
								{
									popup.Items.AddRange( WFT.MenuItemToToolStrips(item) );
								}
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
				string id = getClientIdFromMachsDataRow( WFT.GetDataRowFromGridRow( focusedGridRow ) );

				//if( e.Button == MouseButtons.Left )
				//{
				//}
			}
		}


	}
}
