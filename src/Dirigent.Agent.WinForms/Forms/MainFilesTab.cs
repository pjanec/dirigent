
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
		const int colType = 3;
		const int colPath = 4;
		const int colStatus = 5;
		const int colGuid = 6;
		const int colMAX = 7;

		private Zuby.ADGV.AdvancedDataGridView _grid;
        private BindingSource _bindingSource = null;
		private DataTable _dataTable = null;
        private DataSet _dataSet = null;

		public MainFilesTab(
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

	        _dataTable = _dataSet.Tables.Add("FilesTable");
			_dataTable.Columns.Add("MachineId", typeof(string));
			_dataTable.Columns.Add("AppId", typeof(string));
			_dataTable.Columns.Add("Id", typeof(string));
			_dataTable.Columns.Add("Type", typeof(string));
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

			var _Type = _grid.Columns[colType];
			_Type.HeaderText = "Type";
			_Type.MinimumWidth = 9;
			_Type.ReadOnly = true;
			_Type.Width = 90;

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

		static string GetNodeTypeName( VfsNodeDef def ) => def switch
		{
			FilePackageDef => "Package",
			VFolderDef => "VFolder",
			FolderDef => "Folder",
			FileRef => "FileRef",
			FileDef => "File",
			_ => "Node"
		};

		// what the node definition says, before any resolution
		static string GetDefinedPath( VfsNodeDef def )
		{
			if( !string.IsNullOrEmpty( def.Path ) )
			{
				var mask = (def as FolderDef)?.Mask;
				// just for display, so plain concatenation - the mask is no valid path component
				return string.IsNullOrEmpty( mask ) ? def.Path : $"{def.Path.TrimEnd('/','\\')}\\{mask}";
			}

			// containers with no physical path just hold other nodes
			if( def.IsContainer )
				return $"({def.Children.Count} items)";

			return string.Empty;
		}

		// the availability of the machine the node lives on; the file itself is checked on demand only
		string GetNodeStatus( VfsNodeDef def )
		{
			if( string.IsNullOrEmpty( def.MachineId ) )
				return string.Empty; // global node, no machine to be online

			var state = Ctrl.GetClientState( def.MachineId );
			if( state is null || !state.Connected )
				return $"{def.MachineId} offline";

			return string.Empty;
		}

		List<VfsNodeDef> _allNodes = new List<VfsNodeDef>();

		public void Refresh()
		{
			if( _bindingSource == null )
			{
				initGrid();
			}

			// rebuild the rows if the definitions have changed (config reload, master reconnect...)
			var newNodes = Ctrl.GetAllVfsNodesDef()
							.OrderBy( x => x.MachineId ).ThenBy( x => x.AppId ).ThenBy( x => x.Id )
							.ToList();

			if( !newNodes.SequenceEqual( _allNodes ) )
			{
				_allNodes.Clear();
				_allNodes.AddRange( newNodes );

				_dataTable.Rows.Clear();
				foreach (var def in _allNodes)
				{
					object[] newrow = new object[colMAX];
					newrow[colMachineId] = def.MachineId ?? "";
					newrow[colAppId] = def.AppId ?? "";
					newrow[colId] = def.Id;
					newrow[colType] = GetNodeTypeName( def );
					newrow[colPath] = GetDefinedPath( def );
					newrow[colStatus] = "";
					newrow[colGuid] = def.Guid.ToString();
					_dataTable.Rows.Add(newrow);
				};
			}

			// refresh the machine availability of the rows not showing a resolution result
			foreach( DataRow dataRow in _dataTable.Rows )
			{
				if( _resolvedGuids.Contains( getGuidFromMachsDataRow( dataRow ) ) )
					continue;

				var def = ReflStates.GetVfsNodeDef( getGuidFromMachsDataRow( dataRow ) );
				if( def is null ) continue;

				var status = GetNodeStatus( def );
				if( (string)dataRow[colStatus] != status )
				{
					dataRow.SetField( colStatus, status );
				}
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

		// guids of the nodes whose Status shows the result of an explicit Resolve, not the machine availability
		private HashSet<Guid> _resolvedGuids = new HashSet<Guid>();

		private DataRow FindDataRow( Guid guid )
		{
			foreach( DataRow dataRow in _dataTable.Rows )
			{
				if( getGuidFromMachsDataRow( dataRow ) == guid )
					return dataRow;
			}
			return null;
		}

		/// <summary>
		/// Resolves the node and shows what it currently points to, so that the user can tell
		/// whether the file is really there without opening or downloading it.
		/// </summary>
		async void ResolveAndShow( Guid guid )
		{
			var dataRow = FindDataRow( guid );
			if( dataRow is null ) return;

			var def = ReflStates.GetVfsNodeDef( guid );
			if( def is null ) return;

			_resolvedGuids.Add( guid );
			SetField( dataRow, colStatus, "Resolving..." );

			try
			{
				var resolved = await ReflStates.FileReg.ResolveAsync( CtrlAsync, def, false, true, null );

				if( resolved is null )
				{
					SetField( dataRow, colStatus, "Not found" );
				}
				else
				if( resolved.IsContainer )
				{
					var files = new List<string>();
					CollectFiles( resolved, files );
					SetField( dataRow, colStatus, $"{files.Count} file(s)" );
					if( files.Count > 0 )
					{
						SetField( dataRow, colPath, string.Join( "; ", files ) );
					}
				}
				else
				{
					SetField( dataRow, colStatus, File.Exists( resolved.Path ) ? "Found" : "Missing" );
					SetField( dataRow, colPath, resolved.Path ?? "" );
				}
			}
			catch( Exception ex )
			{
				SetField( dataRow, colStatus, ex.Message );
			}
		}

		/// <summary>
		/// The rows may get rebuilt (config reload) while an asynchronous resolution is in progress,
		/// leaving us with a row no longer belonging to the table. Such a result is simply dropped.
		/// </summary>
		static void SetField( DataRow dataRow, int column, string value )
		{
			try
			{
				if( dataRow.RowState == DataRowState.Detached ) return;
				dataRow.SetField( column, value );
			}
			catch( Exception )
			{
			}
		}

		static void CollectFiles( VfsNodeDef node, List<string> files )
		{
			foreach( var child in node.Children )
			{
				if( child.IsContainer )
				{
					CollectFiles( child, files );
				}
				else
				if( !string.IsNullOrEmpty( child.Path ) )
				{
					files.Add( child.Path );
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
				Guid guid = getGuidFromMachsDataRow( WFT.GetDataRowFromGridRow( focusedGridRow ) );

				if( e.Button == MouseButtons.Right )
				{
					// build popup menu
					var popup = new System.Windows.Forms.ContextMenuStrip( _form.Components );
					{
						var vfsNodeDef = ReflStates.GetVfsNodeDef( guid );
						if ( vfsNodeDef != null )
						{
							{
								var item = new System.Windows.Forms.ToolStripMenuItem( "&Resolve" );
								item.Click += ( s, a ) => WFT.GuardedOp( () => ResolveAndShow( guid ) );
								popup.Items.Add( item );
							}

							var submenus = _menuBuilder.BuildVfsNodeActionsMenuItems( vfsNodeDef );
							if( submenus.Count > 0 )
							{
								popup.Items.Add( new ToolStripSeparator() );
							}
							foreach (var submenu in submenus)
							{
								popup.Items.AddRange( WFT.MenuItemToToolStrips(submenu) );
							}
						}
					}

					popup.Show( Cursor.Position );
				}
			}
		}

		public void MouseDoubleClick( object sender, MouseEventArgs e )
		{
			var hti = _grid.HitTest( e.X, e.Y );
			int currentRow = hti.RowIndex;

			if( currentRow >= 0 ) // ignore header clicks
			{
				DataGridViewRow focusedGridRow = _grid.Rows[currentRow];
				Guid guid = getGuidFromMachsDataRow( WFT.GetDataRowFromGridRow( focusedGridRow ) );
				WFT.GuardedOp( () => ResolveAndShow( guid ) );
			}
		}


	}
}
