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
		const int appTabColMachineId = 0;
		const int appTabColAppId = 1;
		const int appTabColStatus = 2;
		const int appTabColIconStart = 3;
		const int appTabColIconKill = 4;
		const int appTabColIconRestart = 5;
		const int appTabColEnabled = 6;
		const int appTabColPlan = 7;
		const int appTabNumCols = appTabColPlan + 1;


        private BindingSource _gridAppsBindingSource = null;
		private DataTable _gridAppsDataTable = null;
        private DataSet _gridAppsDataSet = null;

		void initAppGrid()
		{
			gridApps.SetDoubleBuffered();

			// when using DataTables the ADGV can properly filter rows
			_gridAppsBindingSource = new BindingSource();
			_gridAppsDataTable = new DataTable();
			_gridAppsDataSet = new DataSet();

			_gridAppsBindingSource.DataSource = _gridAppsDataSet;

	        _gridAppsDataTable = _gridAppsDataSet.Tables.Add("AppsTable");
			_gridAppsDataTable.Columns.Add("MachineId", typeof(string));
			_gridAppsDataTable.Columns.Add("AppId", typeof(string));
			_gridAppsDataTable.Columns.Add("Status", typeof(string));
			_gridAppsDataTable.Columns.Add("IconStart", typeof(Bitmap));
			_gridAppsDataTable.Columns.Add("IconKill", typeof(Bitmap));
			_gridAppsDataTable.Columns.Add("IconRestart", typeof(Bitmap));
			_gridAppsDataTable.Columns.Add("Enabled", typeof(bool));
			_gridAppsDataTable.Columns.Add("Plan", typeof(string));

			_gridAppsBindingSource.DataMember = _gridAppsDataSet.Tables[0].TableName;

			gridApps.DataSource = _gridAppsBindingSource;

			// adjust columns

			var _hdrMachineId = gridApps.Columns[appTabColMachineId];
			_hdrMachineId.HeaderText = "Machine";
			_hdrMachineId.MinimumWidth = 9;
			_hdrMachineId.ReadOnly = true;
			_hdrMachineId.Width = 125;

			var _hdrAppId = gridApps.Columns[appTabColAppId];
			_hdrAppId.HeaderText = "App";
			_hdrAppId.MinimumWidth = 9;
			_hdrAppId.ReadOnly = true;
			_hdrAppId.Width = 125;

			var _hdrStatus = gridApps.Columns[appTabColStatus];
			_hdrStatus.HeaderText = "Status";
			_hdrStatus.MinimumWidth = 9;
			_hdrStatus.ReadOnly = true;
			_hdrStatus.Width = 175;

			var _hdrLaunchIcon = gridApps.Columns[appTabColIconStart];
			_hdrLaunchIcon.HeaderText = "";
			_hdrLaunchIcon.MinimumWidth = 9;
			_hdrLaunchIcon.ReadOnly = true;
			_hdrLaunchIcon.Width = 24;

			var _hdrKillIcon = gridApps.Columns[appTabColIconKill];
			_hdrKillIcon.HeaderText = "";
			_hdrKillIcon.MinimumWidth = 9;
			_hdrKillIcon.ReadOnly = true;
			_hdrKillIcon.Width = 24;

			var _hdrRestartIcon = gridApps.Columns[appTabColIconRestart];
			_hdrRestartIcon.HeaderText = "";
			_hdrRestartIcon.MinimumWidth = 9;
			_hdrRestartIcon.ReadOnly = true;
			_hdrRestartIcon.Width = 24;

			var _hdrEnabled = gridApps.Columns[appTabColEnabled];
			_hdrEnabled.HeaderText = "Enabled";
			_hdrEnabled.MinimumWidth = 9;
			_hdrEnabled.ReadOnly = true;
			_hdrEnabled.Width = 50;

			var _hdrPlan = gridApps.Columns[appTabColPlan];
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
				( from p in _ctrl.GetAllPlanDefs()
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
		void refreshApps()
		{
			if( _gridAppsBindingSource == null )
			{
				initAppGrid();
			}


			var plan = _currentPlan;

			var planAppDefsDict = ( plan != null ) ? ( from ad in plan.AppDefs select ad ).ToDictionary( ad => ad.Id, ad => ad ) : new Dictionary<AppIdTuple, AppDef>();
			var planAppIdTuples = ( plan != null ) ? ( from ad in plan.AppDefs select ad.Id ).ToList() : new List<AppIdTuple>();

			Dictionary<AppIdTuple, AppState> appStates;
			if( ShowJustAppFromCurrentPlan )
			{
				appStates = ( from i in _ctrl.GetAllAppStates() where planAppIdTuples.Contains( i.Key ) select i ).ToDictionary( mc => mc.Key, mc => mc.Value );
			}
			else // show from all plans
			{
				appStates = new Dictionary<AppIdTuple, AppState>( _ctrl.GetAllAppStates() );
			}

			// remember apps from plan
			Dictionary<AppIdTuple, AppIdTuple> newApps = new Dictionary<AppIdTuple, AppIdTuple>();

			foreach( AppIdTuple a in appStates.Keys )
			{
				newApps[a] = a;
			}

			// remember apps from list
			Dictionary<AppIdTuple, DataRow> oldApps = new Dictionary<AppIdTuple, DataRow>();

			foreach( DataRow dataRow in _gridAppsDataTable.Rows )
			{
				var id = getAppTupleFromAppDataRow( dataRow );

				oldApps[id] = dataRow;
			}

			// determine what to add and what to remove
			List<DataRow> toRemove = new List<DataRow>();
			List<object[]> toAdd = new List<object[]>();

			foreach( DataRow dataRow in _gridAppsDataTable.Rows )
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

					var item = new object[appTabNumCols];
					item[appTabColMachineId] = id.MachineId;
					item[appTabColAppId] = id.AppId;
					//item[appTabColStatus] = getAppStatusCode( id, appState, planAppIdTuples.Contains( id ) );
					item[appTabColStatus] = Tools.GetAppStateText( appState, _ctrl.GetPlanState(appState.PlanName), _ctrl.GetAppDef(id) );
					item[appTabColIconStart] = _iconStart;
					item[appTabColIconKill] = _iconKill;
					item[appTabColIconRestart] = _iconRestart;
					item[appTabColEnabled] = false;
					item[appTabColPlan] = GetPlanForApp( id );
					toAdd.Add( item );
				}
			}

			foreach( var dataRow in toRemove )
			{
				_gridAppsDataTable.Rows.Remove( dataRow );
			}

			foreach( var newrow in toAdd )
			{
				_gridAppsDataTable.Rows.Add( newrow );
			}

			Dictionary<DataRow, UPD> toUpdate = new Dictionary<DataRow, UPD>();
			foreach( var o in oldApps )
			{
				if( !toRemove.Contains( o.Value ) )
				{
					var id = newApps[o.Key];
					var appState = _ctrl.GetAppState( id );
					var upd = new UPD()
					{
						//Status = getAppStatusCode( id, appState, planAppIdTuples.Contains( id ) ),
						Status = Tools.GetAppStateText( appState, _ctrl.GetPlanState( appState.PlanName ), _ctrl.GetAppDef( id ) ),
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

				dataRow.SetField( appTabColStatus, upd.Status );

				if( upd.PlanName != null )
				{
					dataRow.SetField( appTabColStatus, upd.Status );
					dataRow.SetField( appTabColPlan, upd.PlanName );
				}
			}

			// colorize the background of items from current plan
			List<AppIdTuple> planAppIds = ( from ad in planAppIdTuples select ad ).ToList();

			foreach( DataGridViewRow gridRow in gridApps.Rows )
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
					var chkCell = gridRow.Cells[appTabColEnabled] as DataGridViewCheckBoxCell;
					
					// could be set via the bindings to DataRow??
					chkCell.Value = appDef != null ? !appDef.Disabled : false;

					// emulate "Disabled" grayed appearance
					chkCell.FlatStyle = appDef != null ? FlatStyle.Standard : FlatStyle.Flat;
					chkCell.Style.ForeColor = appDef != null ? Color.Black : Color.DarkGray;
					chkCell.ReadOnly = appDef == null;
				}
				// put app state into a tooltip
				{
					var appStatusCell = gridRow.Cells[appTabColStatus]; // as DataGridViewCell;
					appStatusCell.ToolTipText = Tools.GetAppStateString( id, _ctrl.GetAppState( id ) );
				}

			}

			if( toAdd.Count > 0 || toRemove.Count > 0 || toUpdate.Count > 0 )
			{
				gridApps.Refresh();
			}
		}

		private void gridApps_CellFormatting( object sender, DataGridViewCellFormattingEventArgs e )
		{
			var gridRow = gridApps.Rows[e.RowIndex];
			var dataRow = getDataRowFromGridRow( gridRow );
			var dataItems = dataRow.ItemArray;
			var id = getAppTupleFromAppDataRow( dataRow );

			var cell = gridApps.Rows[e.RowIndex].Cells[e.ColumnIndex];
			var defst = gridApps.Rows[e.RowIndex].Cells[appTabColMachineId].Style;
			if( e.ColumnIndex == appTabColStatus )
			{
				var txt = dataItems[appTabColStatus] as string;
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
						( from p in _ctrl.GetAllPlanDefs()
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

		private DataRow getDataRowFromGridRow( DataGridViewRow gridRow )
		{
			var drv = gridRow.DataBoundItem as DataRowView;
			var dataRow = drv.Row;
			return dataRow;
		}

		private AppIdTuple getAppTupleFromAppDataRow( DataRow dataRow )
		{
			var dataItems = dataRow.ItemArray;
			var id = new AppIdTuple( (string)dataItems[appTabColMachineId], (string)dataItems[appTabColAppId] );
			return id;
		}

		private AppIdTuple getAppTupleFromAppGridRow( DataGridViewRow gridRow )
		{
			var dataRow = getDataRowFromGridRow( gridRow );
			var id = getAppTupleFromAppDataRow( dataRow );
			return id;
		}

		private void gridApps_MouseClick( object sender, MouseEventArgs e )
		{
			var hti = gridApps.HitTest( e.X, e.Y );
			int currentRow = hti.RowIndex;
			int currentCol = hti.ColumnIndex;
			var plan = _currentPlan;

			if( currentRow >= 0 ) // ignore header clicks
			{
				DataGridViewRow focused = gridApps.Rows[currentRow];
				var id = getAppTupleFromAppGridRow( focused );

				var st = _ctrl.GetAppState( id );
				bool connected = IsConnected;
				//bool isLocalApp = id.MachineId == this._machineId;
				bool isAccessible = connected; // can we change its state?

				if( e.Button == MouseButtons.Right )
				{
					// build popup menu
					var popup = new System.Windows.Forms.ContextMenuStrip( this.components );
					popup.Enabled = connected || _allowLocalIfDisconnected;

					{
						var item = new System.Windows.Forms.ToolStripMenuItem( "&Launch" );
						item.Click += ( s, a ) => guardedOp( () => _ctrl.Send( new Net.StartAppMessage(
							_ctrl.Name,
							id,
							Tools.IsAppInPlan(_ctrl, id, _currentPlan ) ? _currentPlan.Name : null // prefer selected plan over others
						)));
						item.Enabled = isAccessible && !st.Running;
						popup.Items.Add( item );
					}

					{
						var item = new System.Windows.Forms.ToolStripMenuItem( "&Kill" );
						item.Click += ( s, a ) => guardedOp( () => _ctrl.Send( new Net.KillAppMessage( _ctrl.Name, id ) ) );
						item.Enabled = isAccessible && ( st.Running || st.Restarting );
						popup.Items.Add( item );
					}

					{
						var item = new System.Windows.Forms.ToolStripMenuItem( "&Restart" );
						item.Click += ( s, a ) => guardedOp( () => _ctrl.Send( new Net.RestartAppMessage( _ctrl.Name, id ) ) );
						item.Enabled = isAccessible; // && st.Running;
						popup.Items.Add( item );
					}

					if( plan != null )
					{
						var item = new System.Windows.Forms.ToolStripMenuItem( "&Enable" );
						item.Click += ( s, a ) => guardedOp( () => _ctrl.Send( new Net.SetAppEnabledMessage( _ctrl.Name, plan.Name, id, true ) ) );
						popup.Items.Add( item );
					}

					if( plan != null )
					{
						var item = new System.Windows.Forms.ToolStripMenuItem( "&Disable" );
						item.Click += ( s, a ) => guardedOp( () => _ctrl.Send( new Net.SetAppEnabledMessage( _ctrl.Name, plan.Name, id, false ) ) );
						popup.Items.Add( item );
					}

					{
						var item = new System.Windows.Forms.ToolStripSeparator();
						popup.Items.Add( item );
					}

					{
						var item = new System.Windows.Forms.ToolStripMenuItem( "&Show Window" );
						item.Click += ( s, a ) => guardedOp( () => _ctrl.Send( new Net.SetWindowStyleMessage( id, EWindowStyle.Normal ) ) );
						item.Enabled = isAccessible && st.Running;
						popup.Items.Add( item );
					}

					{
						var item = new System.Windows.Forms.ToolStripMenuItem( "&Hide Window" );
						item.Click += ( s, a ) => guardedOp( () => _ctrl.Send( new Net.SetWindowStyleMessage( id, EWindowStyle.Hidden ) ) );
						item.Enabled = isAccessible && st.Running;
						popup.Items.Add( item );
					}

					{
						var item = new System.Windows.Forms.ToolStripSeparator();
						popup.Items.Add( item );
					}

					{
						var item = new System.Windows.Forms.ToolStripMenuItem( "&Properties" );
						item.Click += ( s, a ) => guardedOp( () => 
						{
							var appDef = _ctrl.GetAppDef( id );
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
					if( currentCol == appTabColIconStart )
					{
						if( isAccessible ) // && !st.Running )
						{
							guardedOp( () => _ctrl.Send( new Net.StartAppMessage(
								_ctrl.Name,
								id,
								Tools.IsAppInPlan(_ctrl, id, _currentPlan ) ? _currentPlan.Name : null // prefer selected plan over others
							)));
						}
					}

					if( currentCol == appTabColIconKill )
					{
						if( isAccessible ) // && st.Running )
						{
							guardedOp( () => _ctrl.Send( new Net.KillAppMessage( _ctrl.Name, id ) ) );
						}
					}

					if( currentCol == appTabColIconRestart )
					{
						if( isAccessible ) // && st.Running )
						{
							guardedOp( () => _ctrl.Send( new Net.RestartAppMessage( _ctrl.Name, id ) ) );
						}
					}

					if( currentCol == appTabColEnabled )
					{
						var wasEnabled = ( bool ) focused.Cells[currentCol].Value;
						if( plan != null )
						{
							guardedOp( () => _ctrl.Send( new Net.SetAppEnabledMessage( _ctrl.Name, plan.Name, id, !wasEnabled ) ) );
						}
						else
						{
							//MessageBox.Show("Application is not part of selected plan. Select a different plan!", "Dirigent", MessageBoxButtons.OK, MessageBoxIcon.Information);
						}
					}
				}
			}
		}

		private void gridApps_MouseDoubleClick( object sender, MouseEventArgs e )
		{
			// launch the app
			if( e.Button == MouseButtons.Left )
			{
				int row = gridApps.HitTest( e.X, e.Y ).RowIndex;
				int col = gridApps.HitTest( e.X, e.Y ).ColumnIndex;

				if( row >= 0 )
				{
					if( col == appTabColMachineId || col == appTabColAppId || col == appTabColStatus || col == appTabColPlan )
					{
						DataGridViewRow focused = gridApps.Rows[row];
						var id = getAppTupleFromAppGridRow( focused );
						var st = _ctrl.GetAppState( id );

						guardedOp( () => _ctrl.Send( new Net.StartAppMessage(
							_ctrl.Name,
							id,
							Tools.IsAppInPlan(_ctrl, id, _currentPlan ) ? _currentPlan.Name : null // prefer selected plan over others
						) ) );
					}
				}
			}
		}

		//string getAppStatusCode( AppIdTuple id, AppState st, AppDef ad )
		//{
		//	string stCode = "Not running";

		//	bool connected = IsConnected;
		//	var currTime = DateTime.UtcNow;
		//	bool isRemoteApp = id.MachineId != this._machineId;

		//	if( isRemoteApp && !connected )
		//	{
		//		stCode = "??? (discon.)";
		//		return stCode;
		//	}

		//	var currPlan = _currentPlan;
		//	if( currPlan != null )
		//	{
		//		var planState = _ctrl.GetPlanState( currPlan.Name );
		//		bool isPartOfPlan = !string.IsNullOrEmpty(st.PlanName) && (currPlan.Name == st.PlanName);
		//		bool planRunning = ( currPlan != null ) && planState.Running && isPartOfPlan;
		//		if( planRunning && !st.PlanApplied && !ad.Disabled )
		//		{
		//			stCode = "Planned";
		//		}
		//	}

		//	if( st.Started )
		//	{
		//		if( st.Running )
		//		{
		//			if( st.Dying )
		//			{
		//				stCode = "Dying";
		//			}
		//			else if( !st.Initialized )
		//			{
		//				stCode = "Initializing";
		//			}
		//			else
		//			{
		//				stCode = "Running";
		//			}
		//		}
		//		else
		//			// !st.Running
		//		{
		//			if( st.Restarting )
		//			{
		//				stCode = "Restarting";
		//				if( st.RestartsRemaining >= 0 ) stCode += String.Format( " ({0} remaining)", st.RestartsRemaining );
		//			}
		//			else if( st.Killed )
		//			{
		//				stCode = "Killed";
		//			}
		//			else
		//			{
		//				stCode = string.Format( "Terminated ({0})", st.ExitCode );
		//			}
		//		}
		//	}
		//	else if( st.StartFailed )
		//	{
		//		stCode = "Failed to start";
		//	}

		//	var statusInfoAge = currTime - st.LastChange;
		//	if( isRemoteApp && statusInfoAge > TimeSpan.FromSeconds( 3 ) )
		//	{
		//		stCode += string.Format( " (Offline for {0:0} sec)", statusInfoAge.TotalSeconds );
		//	}


		//	return stCode;
		//}

		private void gridApps_CellToolTipTextNeeded( object sender, DataGridViewCellToolTipTextNeededEventArgs e )
		{
		}

	}
}
