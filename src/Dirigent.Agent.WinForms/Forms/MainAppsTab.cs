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
	public class MainAppsTab : MainExtension
	{
		const int colMachineId = 0;
		const int colAppId = 1;
		const int colStatus = 2;
		const int colIconStart = 3;
		const int colIconKill = 4;
		const int colIconRestart = 5;
		const int colEnabled = 6;
		const int colPlan = 7;
		const int colMAX = 8;


		private Zuby.ADGV.AdvancedDataGridView _grid;
        private BindingSource _bindingSource = null;
		private DataTable _dataTable = null;
        private DataSet _dataSet = null;

		public MainAppsTab(
			frmMain form,
			Zuby.ADGV.AdvancedDataGridView grid
			) : base(form)
		{
			_grid = grid;
		}

		void initGrid()
		{
			_grid.SetDoubleBuffered();

			// when using DataTables the ADGV can properly filter rows
			_bindingSource = new BindingSource();
			_dataTable = new DataTable();
			_dataSet = new DataSet();

			_bindingSource.DataSource = _dataSet;

	        _dataTable = _dataSet.Tables.Add("AppsTable");
			_dataTable.Columns.Add("MachineId", typeof(string));
			_dataTable.Columns.Add("AppId", typeof(string));
			_dataTable.Columns.Add("Status", typeof(string));
			_dataTable.Columns.Add("IconStart", typeof(Bitmap));
			_dataTable.Columns.Add("IconKill", typeof(Bitmap));
			_dataTable.Columns.Add("IconRestart", typeof(Bitmap));
			_dataTable.Columns.Add("Enabled", typeof(bool));
			_dataTable.Columns.Add("Plan", typeof(string));

			_bindingSource.DataMember = _dataSet.Tables[0].TableName;

			_grid.DataSource = _bindingSource;

			// adjust columns

			var _hdrMachineId = _grid.Columns[colMachineId];
			_hdrMachineId.HeaderText = "Machine";
			_hdrMachineId.MinimumWidth = 9;
			_hdrMachineId.ReadOnly = true;
			_hdrMachineId.Width = 125;

			var _hdrAppId = _grid.Columns[colAppId];
			_hdrAppId.HeaderText = "App";
			_hdrAppId.MinimumWidth = 9;
			_hdrAppId.ReadOnly = true;
			_hdrAppId.Width = 125;

			var _hdrStatus = _grid.Columns[colStatus];
			_hdrStatus.HeaderText = "Status";
			_hdrStatus.MinimumWidth = 9;
			_hdrStatus.ReadOnly = true;
			_hdrStatus.Width = 175;

			var _hdrLaunchIcon = _grid.Columns[colIconStart];
			_hdrLaunchIcon.HeaderText = "";
			_hdrLaunchIcon.MinimumWidth = 9;
			_hdrLaunchIcon.ReadOnly = true;
			_hdrLaunchIcon.Width = 24;

			var _hdrKillIcon = _grid.Columns[colIconKill];
			_hdrKillIcon.HeaderText = "";
			_hdrKillIcon.MinimumWidth = 9;
			_hdrKillIcon.ReadOnly = true;
			_hdrKillIcon.Width = 24;

			var _hdrRestartIcon = _grid.Columns[colIconRestart];
			_hdrRestartIcon.HeaderText = "";
			_hdrRestartIcon.MinimumWidth = 9;
			_hdrRestartIcon.ReadOnly = true;
			_hdrRestartIcon.Width = 24;

			var _hdrEnabled = _grid.Columns[colEnabled];
			_hdrEnabled.HeaderText = "Enabled";
			_hdrEnabled.MinimumWidth = 9;
			_hdrEnabled.ReadOnly = true;
			_hdrEnabled.Width = 50;

			var _hdrPlan = _grid.Columns[colPlan];
			_hdrPlan.HeaderText = "Last Plan";
			_hdrPlan.MinimumWidth = 9;
			_hdrPlan.ReadOnly = true;
			_hdrPlan.Width = 175;

			if (Common.Properties.Settings.Default.GridButtonSpacing > 0)
			{
				_hdrLaunchIcon.Width = Common.Properties.Settings.Default.GridButtonSpacing;
				_hdrKillIcon.Width = Common.Properties.Settings.Default.GridButtonSpacing;
				_hdrRestartIcon.Width = Common.Properties.Settings.Default.GridButtonSpacing;
			}

		}


		struct UPD
		{
			public string Status;
			public string PlanName;
		}

		string GetPlanForApp( AppIdTuple id )
		{
			var x =
				( from p in Ctrl.GetAllPlanDefs()
				  from a in p.AppDefs
				  where a.Id == id
				  select p.Name ).ToList();
			if( x.Count > 1 )
				return "<multiple>";
			if( x.Count == 1 )
				return x[0];
			return String.Empty;
		}

		/// <summary>
		/// Update the list of apps by doing minimal changes to avoid losing focus.
		/// Adding what is not yet there and deleting what has disappeared.
		/// </summary>
		public void Refresh()
		{
			if( _bindingSource == null )
			{
				initGrid();
			}


			var plan = CurrentPlan;

			var planAppDefsDict = ( plan != null ) ? ( from ad in plan.AppDefs select ad ).ToDictionary( ad => ad.Id, ad => ad ) : new Dictionary<AppIdTuple, AppDef>();
			var planAppIdTuples = ( plan != null ) ? ( from ad in plan.AppDefs select ad.Id ).ToList() : new List<AppIdTuple>();

			Dictionary<AppIdTuple, AppState> appStates;
			if( _form.ShowJustAppFromCurrentPlan )
			{
				appStates = ( from i in Ctrl.GetAllAppStates() where planAppIdTuples.Contains( i.Key ) select i ).ToDictionary( mc => mc.Key, mc => mc.Value );
			}
			else // show from all plans
			{
				appStates = new Dictionary<AppIdTuple, AppState>( Ctrl.GetAllAppStates() );
			}

			// remember apps from plan
			Dictionary<AppIdTuple, AppIdTuple> newApps = new Dictionary<AppIdTuple, AppIdTuple>();

			foreach( AppIdTuple a in appStates.Keys )
			{
				newApps[a] = a;
			}

			// remember apps from list
			Dictionary<AppIdTuple, DataRow> oldApps = new Dictionary<AppIdTuple, DataRow>();

			foreach( DataRow dataRow in _dataTable.Rows )
			{
				var id = getAppTupleFromAppDataRow( dataRow );

				oldApps[id] = dataRow;
			}

			// determine what to add and what to remove
			List<DataRow> toRemove = new List<DataRow>();
			List<object[]> toAdd = new List<object[]>();

			foreach( DataRow dataRow in _dataTable.Rows )
			{
				var id = getAppTupleFromAppDataRow( dataRow );

				if( !newApps.ContainsKey( id ) )
				{
					toRemove.Add( dataRow );
				}
			}

			foreach( var x in appStates )
			{
				var id = x.Key;
				var appState = x.Value;

				if( !oldApps.ContainsKey( id ) )
				{

					var item = new object[colMAX];
					item[colMachineId] = id.MachineId;
					item[colAppId] = id.AppId;
					//item[colStatus] = getAppStatusCode( id, appState, planAppIdTuples.Contains( id ) );
					item[colStatus] = Tools.GetAppStateText( appState, Ctrl.GetPlanState(appState.PlanName), Ctrl.GetAppDef(id) );
					item[colIconStart] = _iconStart;
					item[colIconKill] = _iconKill;
					item[colIconRestart] = _iconRestart;
					item[colEnabled] = false;
					item[colPlan] = GetPlanForApp( id );
					toAdd.Add( item );
				}
			}

			foreach( var dataRow in toRemove )
			{
				_dataTable.Rows.Remove( dataRow );
			}

			foreach( var newrow in toAdd )
			{
				_dataTable.Rows.Add( newrow );
			}

			Dictionary<DataRow, UPD> toUpdate = new Dictionary<DataRow, UPD>();
			foreach( var o in oldApps )
			{
				if( !toRemove.Contains( o.Value ) )
				{
					var id = newApps[o.Key];
					var appState = Ctrl.GetAppState( id );
					var upd = new UPD()
					{
						//Status = getAppStatusCode( id, appState, planAppIdTuples.Contains( id ) ),
						Status = Tools.GetAppStateText( appState, Ctrl.GetPlanState( appState.PlanName ), Ctrl.GetAppDef( id ) ),
						PlanName = null
					};
					if( appState.PlanName != null )
					{
						upd.PlanName = appState.PlanName;
					}
					toUpdate[o.Value] = upd;

				}
			}

			foreach( var tu in toUpdate )
			{
				var dataRow = tu.Key;
				var upd = tu.Value;

				dataRow.SetField( colStatus, upd.Status );

				if( upd.PlanName != null )
				{
					dataRow.SetField( colStatus, upd.Status );
					dataRow.SetField( colPlan, upd.PlanName );
				}
			}

			// colorize the background of items from current plan
			List<AppIdTuple> planAppIds = ( from ad in planAppIdTuples select ad ).ToList();

			foreach( DataGridViewRow gridRow in _grid.Rows )
			{
				var id = getAppTupleFromAppGridRow( gridRow );

				if( planAppIds.Contains( id ) )
				{
					gridRow.DefaultCellStyle.BackColor = Color.LightGoldenrodYellow;
				}
				else
				{
					gridRow.DefaultCellStyle.BackColor = SystemColors.Control;
				}

				// set checkbox based on Enabled attribute od the appDef from current plan
				var appDef = planAppDefsDict.ContainsKey( id ) ? planAppDefsDict[id] : null;
				{
					var chkCell = gridRow.Cells[colEnabled] as DataGridViewCheckBoxCell;
					
					// could be set via the bindings to DataRow??
					chkCell.Value = appDef != null ? !appDef.Disabled : false;

					// emulate "Disabled" grayed appearance
					chkCell.FlatStyle = appDef != null ? FlatStyle.Standard : FlatStyle.Flat;
					chkCell.Style.ForeColor = appDef != null ? Color.Black : Color.DarkGray;
					chkCell.ReadOnly = appDef == null;
				}
				// put app state into a tooltip
				{
					var appStatusCell = gridRow.Cells[colStatus]; // as DataGridViewCell;
					appStatusCell.ToolTipText = Tools.GetAppStateString( id, Ctrl.GetAppState( id ) );
				}

			}

			if( toAdd.Count > 0 || toRemove.Count > 0 || toUpdate.Count > 0 )
			{
				_grid.Refresh();
			}
		}

		public void CellFormatting( object sender, DataGridViewCellFormattingEventArgs e )
		{
			var gridRow = _grid.Rows[e.RowIndex];
			var dataRow = WFT.GetDataRowFromGridRow( gridRow );
			var dataItems = dataRow.ItemArray;
			var id = getAppTupleFromAppDataRow( dataRow );

			var cell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
			var defst = _grid.Rows[e.RowIndex].Cells[colMachineId].Style;
			if( e.ColumnIndex == colStatus )
			{
				var txt = dataItems[colStatus] as string;
				if( txt.StartsWith( "Running" ) )
				{
					cell.Style = new DataGridViewCellStyle { ForeColor = Color.DarkGreen, SelectionForeColor = Color.LightGreen, BackColor = defst.BackColor };
				}
				else if( txt.StartsWith( "Planned" ) )
				{
					cell.Style = new DataGridViewCellStyle { ForeColor = Color.DarkViolet, SelectionForeColor = Color.Violet, BackColor = defst.BackColor };
				}
				else if( txt.StartsWith( "Initializing" ) )
				{
					cell.Style = new DataGridViewCellStyle { ForeColor = Color.DarkOrange, SelectionForeColor = Color.Orange, BackColor = defst.BackColor };
				}
				else if( txt.StartsWith( "Terminated" ) )
				{
					var appDef =
						( from p in Ctrl.GetAllPlanDefs()
						  from a in p.AppDefs
						  where a.Id == id
						  select a ).FirstOrDefault();
					if( appDef != null )
					{
						if( !appDef.Volatile ) // just non-volatile apps are not supposed to terminate on their own...
						{
							cell.Style = new DataGridViewCellStyle { ForeColor = Color.Red, SelectionForeColor = Color.Red, BackColor = defst.BackColor };
						}
						else
						{
							cell.Style = defst;
						}
					}
					else
					{
						cell.Style = defst;
					}
				}
				else if( txt.StartsWith( "Restarting" ) || txt.StartsWith( "Dying" ) )
				{
					cell.Style = new DataGridViewCellStyle { ForeColor = Color.Blue, SelectionForeColor = Color.Blue, BackColor = defst.BackColor };
				}
				else
				{
					cell.Style = defst;
				}
			}
		}


		private AppIdTuple getAppTupleFromAppDataRow( DataRow dataRow )
		{
			var dataItems = dataRow.ItemArray;
			var id = new AppIdTuple( (string)dataItems[colMachineId], (string)dataItems[colAppId] );
			return id;
		}

		private AppIdTuple getAppTupleFromAppGridRow( DataGridViewRow gridRow )
		{
			var dataRow = WFT.GetDataRowFromGridRow( gridRow );
			var id = getAppTupleFromAppDataRow( dataRow );
			return id;
		}

		public void MouseClick( object sender, MouseEventArgs e )
		{
			var hti = _grid.HitTest( e.X, e.Y );
			int currentRow = hti.RowIndex;
			int currentCol = hti.ColumnIndex;
			var plan = CurrentPlan;

			if( currentRow >= 0 ) // ignore header clicks
			{
				DataGridViewRow focused = _grid.Rows[currentRow];
				var id = getAppTupleFromAppGridRow( focused );

				var appDef = Ctrl.GetAppDef( id );
				var st = Ctrl.GetAppState( id );
				bool connected = Client.IsConnected;
				//bool isLocalApp = id.MachineId == this._machineId;
				bool isAccessible = connected; // can we change its state?

				if( e.Button == MouseButtons.Right )
				{
					// build popup menu
					var popup = new System.Windows.Forms.ContextMenuStrip( _form.Components );
					popup.Enabled = connected || _form.AllowLocalIfDisconnected;
					
					//{
					//	var item = new System.Windows.Forms.ToolStripMenuItem( "&Launch" );
					//	item.Click += ( s, a ) => WFT.GuardedOp( () => Ctrl.Send( new Net.StartAppMessage(
					//		Ctrl.Name,
					//		id,
					//		Tools.IsAppInPlan(Ctrl, id, CurrentPlan ) ? CurrentPlan.Name : null // prefer selected plan over others
					//	)));
					//	item.Enabled = isAccessible && !st.Running;
					//	popup.Items.Add( item );
					//}

					//{
					//	var item = new System.Windows.Forms.ToolStripMenuItem( "&Kill" );
					//	item.Click += ( s, a ) => WFT.GuardedOp( () => Ctrl.Send( new Net.KillAppMessage( Ctrl.Name, id ) ) );
					//	item.Enabled = isAccessible && ( st.Running || st.Restarting );
					//	popup.Items.Add( item );
					//}

					//{
					//	var item = new System.Windows.Forms.ToolStripMenuItem( "&Restart" );
					//	item.Click += ( s, a ) => WFT.GuardedOp( () => Ctrl.Send( new Net.RestartAppMessage( Ctrl.Name, id ) ) );
					//	item.Enabled = isAccessible; // && st.Running;
					//	popup.Items.Add( item );
					//}

					if( plan != null )
					{
						var item = new System.Windows.Forms.ToolStripMenuItem( "&Enable" );
						item.Click += ( s, a ) => WFT.GuardedOp( () => Ctrl.Send( new Net.SetAppEnabledMessage( Ctrl.Name, plan.Name, id, true ) ) );
						popup.Items.Add( item );
					}

					if( plan != null )
					{
						var item = new System.Windows.Forms.ToolStripMenuItem( "&Disable" );
						item.Click += ( s, a ) => WFT.GuardedOp( () => Ctrl.Send( new Net.SetAppEnabledMessage( Ctrl.Name, plan.Name, id, false ) ) );
						popup.Items.Add( item );
					}

					if( plan != null ) // if no plan then no items above and no need for a separator 
					{
						var item = new System.Windows.Forms.ToolStripSeparator();
						popup.Items.Add( item );
					}

					{
						var item = new System.Windows.Forms.ToolStripMenuItem( "&Show Window" );
						item.Click += ( s, a ) => WFT.GuardedOp( () => Ctrl.Send( new Net.SetWindowStyleMessage( id, EWindowStyle.Normal ) ) );
						item.Enabled = isAccessible && st.Running;
						popup.Items.Add( item );
					}

					{
						var item = new System.Windows.Forms.ToolStripMenuItem( "&Hide Window" );
						item.Click += ( s, a ) => WFT.GuardedOp( () => Ctrl.Send( new Net.SetWindowStyleMessage( id, EWindowStyle.Hidden ) ) );
						item.Enabled = isAccessible && st.Running;
						popup.Items.Add( item );
					}

					{
						var filesMenu = ContextMenuFiles( from x in appDef.VfsNodes where x is FileDef select x as FileDef );
						if ( filesMenu.DropDownItems.Count > 0 )
						{
							popup.Items.Add( new ToolStripSeparator() );
						//	popup.Items.Add( filesMenu );
						}
						var fileMenuItems = filesMenu.DropDownItems.Cast<ToolStripMenuItem>().ToArray();
						foreach ( ToolStripMenuItem item in fileMenuItems )
						{
							popup.Items.Add( item );
						}
					}

					{
						var fpackMenu = ContextMenuFilePackages( from x in appDef.VfsNodes where x is FilePackageDef select x as FilePackageDef );
						if( fpackMenu.DropDownItems.Count > 0 )
						{
							popup.Items.Add( new ToolStripSeparator() );
						//	popup.Items.Add( fpackMenu );
						}
						var fpackMenuItems = fpackMenu.DropDownItems.Cast<ToolStripMenuItem>().ToArray();
						foreach ( ToolStripMenuItem item in fpackMenuItems )
						{
							popup.Items.Add( item );
						}
					}

					{
						var toolsMenu = new System.Windows.Forms.ToolStripMenuItem( "&Tools" );
						foreach( var tool in appDef.Tools )
						{
							var title = tool.Title;
							if (string.IsNullOrEmpty( title )) title = tool.Id;
							var item = new ToolStripMenuItem( title );
							item.Click += ( s, a ) => WFT.GuardedOp( () => {
									_form.ToolsRegistry.StartAppBoundTool( tool, appDef ) ;
								}
							);
							toolsMenu.DropDownItems.Add( item );
						}

						if( toolsMenu.DropDownItems.Count > 0 )
						{
							//popup.Items.Add( toolsMenu );
							popup.Items.Add( new ToolStripSeparator() );
						}
						var toolsMenuItems = toolsMenu.DropDownItems.Cast<ToolStripMenuItem>().ToArray();
						foreach ( var item in toolsMenuItems )
						{
							popup.Items.Add( item );
						}
					}

					{
						popup.Items.Add( new ToolStripSeparator() );
						var item = new System.Windows.Forms.ToolStripMenuItem( "&Properties" );
						item.Click += ( s, a ) => WFT.GuardedOp( () => 
						{
							var appDef = Ctrl.GetAppDef( id );
							var frm = new frmAppProperties( appDef );
							frm.Show();
						});
						item.Enabled = true;
						popup.Items.Add( item );
					}


					popup.Show( Cursor.Position );

				}
				else if( e.Button == MouseButtons.Left )
				{
					// icon clicks
					if( currentCol == colIconStart )
					{
						if( isAccessible ) // && !st.Running )
						{
							WFT.GuardedOp( () => Ctrl.Send( new Net.StartAppMessage(
								Ctrl.Name,
								id,
								Tools.IsAppInPlan(Ctrl, id, CurrentPlan ) ? CurrentPlan.Name : null // prefer selected plan over others
							)));
						}
					}

					if( currentCol == colIconKill )
					{
						if( isAccessible ) // && st.Running )
						{
							WFT.GuardedOp( () => Ctrl.Send( new Net.KillAppMessage( Ctrl.Name, id ) ) );
						}
					}

					if( currentCol == colIconRestart )
					{
						if( isAccessible ) // && st.Running )
						{
							WFT.GuardedOp( () => Ctrl.Send( new Net.RestartAppMessage( Ctrl.Name, id ) ) );
						}
					}

					if( currentCol == colEnabled )
					{
						var wasEnabled = ( bool ) focused.Cells[currentCol].Value;
						if( plan != null )
						{
							WFT.GuardedOp( () => Ctrl.Send( new Net.SetAppEnabledMessage( Ctrl.Name, plan.Name, id, !wasEnabled ) ) );
						}
						else
						{
							//MessageBox.Show("Application is not part of selected plan. Select a different plan!", "Dirigent", MessageBoxButtons.OK, MessageBoxIcon.Information);
						}
					}
				}
			}
		}

		public void MouseDoubleClick( object sender, MouseEventArgs e )
		{
			// launch the app
			if( e.Button == MouseButtons.Left )
			{
				int row = _grid.HitTest( e.X, e.Y ).RowIndex;
				int col = _grid.HitTest( e.X, e.Y ).ColumnIndex;

				if( row >= 0 )
				{
					if( col == colMachineId || col == colAppId || col == colStatus || col == colPlan )
					{
						DataGridViewRow focused = _grid.Rows[row];
						var id = getAppTupleFromAppGridRow( focused );
						var st = Ctrl.GetAppState( id );

						WFT.GuardedOp( () => Ctrl.Send( new Net.StartAppMessage(
							Ctrl.Name,
							id,
							Tools.IsAppInPlan(Ctrl, id, CurrentPlan ) ? CurrentPlan.Name : null // prefer selected plan over others
						) ) );
					}
				}
			}
		}

	}
}
