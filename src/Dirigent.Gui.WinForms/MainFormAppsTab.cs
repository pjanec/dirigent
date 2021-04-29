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

using Dirigent.Common;

namespace Dirigent.Gui.WinForms
{
	public partial class frmMain : Form
	{
		const int appTabColName = 0;
		const int appTabColStatus = 1;
		const int appTabColIconStart = 2;
		const int appTabColIconKill = 3;
		const int appTabColIconRestart = 4;
		const int appTabColEnabled = 5;
		const int appTabColPlan = 6;
		const int appTabNumCols = appTabColPlan + 1;


		void refreshAppList()
		{
			var plan = _currentPlan;

			gridApps.Rows.Clear();

			if( plan != null )
			{
				foreach( AppDef ad in plan.AppDefs )
				{
					gridApps.Rows.Add(
						new object[]
					{
						ad.Id.ToString(),
						Tools.GetAppStateText( _ctrl.GetAppState( ad.Id ), _ctrl.GetPlanState(plan.Name), ad )
					}
					);
				}
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
		void refreshAppList_smart()
		{
			DataGridViewRow selected = null;

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
			Dictionary<string, AppIdTuple> newApps = new Dictionary<string, AppIdTuple>();

			foreach( AppIdTuple a in appStates.Keys )
			{
				newApps[a.ToString()] = a;
			}

			// remember apps from list
			Dictionary<string, DataGridViewRow> oldApps = new Dictionary<string, DataGridViewRow>();

			foreach( DataGridViewRow item in gridApps.Rows )
			{
				string id = item.Cells[appTabColName].Value as string;
				oldApps[id] = item;

				if( item.Selected )
				{
					if( selected == null )
					{
						selected = item;
					}
				}
			}

			// determine what to add and what to remove
			List<DataGridViewRow> toRemove = new List<DataGridViewRow>();
			List<object[]> toAdd = new List<object[]>();

			foreach( DataGridViewRow item in gridApps.Rows )
			{
				string id = item.Cells[0].Value as string;
				if( !newApps.ContainsKey( id ) )
				{
					toRemove.Add( item );
				}
			}

			foreach( var x in appStates )
			{
				var idStr = x.Key.ToString();
				if( !oldApps.ContainsKey( idStr ) )
				{
					var id = x.Key;
					var appState = x.Value;
					var item = new object[appTabNumCols];
					item[appTabColName] = idStr;
					//item[appTabColStatus] = getAppStatusCode( id, appState, planAppIdTuples.Contains( id ) );
					item[appTabColStatus] = Tools.GetAppStateText( appState, _ctrl.GetPlanState(appState.PlanName), _ctrl.GetAppDef(id) );
					item[appTabColIconStart] = ResizeImage( new Bitmap( Resource1.play ), new Size( 20, 20 ) );
					item[appTabColIconKill] = ResizeImage( new Bitmap( Resource1.delete ), new Size( 20, 20 ) );
					item[appTabColIconRestart] = ResizeImage( new Bitmap( Resource1.refresh ), new Size( 20, 20 ) );
					item[appTabColEnabled] = false;
					item[appTabColPlan] = GetPlanForApp( id );
					toAdd.Add( item );
				}
			}

			foreach( var i in toRemove )
			{
				gridApps.Rows.Remove( i );
			}

			foreach( var i in toAdd )
			{
				gridApps.Rows.Add( i );
			}

			Dictionary<DataGridViewRow, UPD> toUpdate = new Dictionary<DataGridViewRow, UPD>();
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
				var row = tu.Key;
				var upd = tu.Value;

				row.Cells[appTabColStatus].Value = upd.Status;

				if( upd.PlanName != null )
				{
					row.Cells[appTabColPlan].Value = upd.PlanName;
				}
			}

			// colorize the background of items from current plan
			List<string> planAppIds = ( from ad in planAppIdTuples select ad.ToString() ).ToList();

			foreach( DataGridViewRow item in gridApps.Rows )
			{
				string idStr = item.Cells[0].Value as string;
				var id = AppIdTuple.fromString( idStr, "" );

				if( planAppIds.Contains( idStr ) )
				{
					item.DefaultCellStyle.BackColor = Color.LightGoldenrodYellow;
				}
				else
				{
					item.DefaultCellStyle.BackColor = SystemColors.Control;
				}

				// set checkbox based on Enabled attribute od the appDef from current plan
				var appDef = planAppDefsDict.ContainsKey( id ) ? planAppDefsDict[id] : null;
				{
					var chkCell = item.Cells[appTabColEnabled] as DataGridViewCheckBoxCell;
					chkCell.Value = appDef != null ? !appDef.Disabled : false;
					// emulate "Disabled" grayed appearance
					chkCell.FlatStyle = appDef != null ? FlatStyle.Standard : FlatStyle.Flat;
					chkCell.Style.ForeColor = appDef != null ? Color.Black : Color.DarkGray;
					chkCell.ReadOnly = appDef == null;
				}
				// put app state into a tooltip
				{
					var appStatusCell = item.Cells[appTabColStatus]; // as DataGridViewCell;
					appStatusCell.ToolTipText = Tools.GetAppStateString( id, _ctrl.GetAppState( id ) );
				}

			}
		}

		private void gridApps_CellFormatting( object sender, DataGridViewCellFormattingEventArgs e )
		{
			var id = new AppIdTuple( ( string ) gridApps.Rows[e.RowIndex].Cells[appTabColName].Value );
			var cell = gridApps.Rows[e.RowIndex].Cells[e.ColumnIndex];
			var defst = gridApps.Rows[e.RowIndex].Cells[appTabColName].Style;
			if( e.ColumnIndex == appTabColStatus )
			{
				var txt = gridApps.Rows[e.RowIndex].Cells[e.ColumnIndex].Value as string;
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

		private void gridApps_MouseClick( object sender, MouseEventArgs e )
		{
			var hti = gridApps.HitTest( e.X, e.Y );
			int currentRow = hti.RowIndex;
			int currentCol = hti.ColumnIndex;
			var plan = _currentPlan;
			var planAppDefsDict = ( plan != null ) ? ( from ad in plan.AppDefs select ad ).ToDictionary( ad => ad.Id, ad => ad ) : new Dictionary<AppIdTuple, AppDef>();

			if( currentRow >= 0 ) // ignore header clicks
			{
				DataGridViewRow focused = gridApps.Rows[currentRow];
				var id = new AppIdTuple( focused.Cells[0].Value as string );
				var st = _ctrl.GetAppState( id );
				bool connected = IsConnected;
				//bool isLocalApp = id.MachineId == this._machineId;
				bool isAccessible = connected; // can we change its state?
				var appDef = planAppDefsDict.ContainsKey( id ) ? planAppDefsDict[id] : null;

				if( e.Button == MouseButtons.Right )
				{
					// build popup menu
					var popup = new System.Windows.Forms.ContextMenuStrip( this.components );
					popup.Enabled = connected || _allowLocalIfDisconnected;

					var launchItem = new System.Windows.Forms.ToolStripMenuItem( "&Launch" );
					launchItem.Click += ( s, a ) => guardedOp( () => _ctrl.Send( new Net.StartAppMessage( id, null ) ) );
					launchItem.Enabled = isAccessible && !st.Running;
					popup.Items.Add( launchItem );

					var killItem = new System.Windows.Forms.ToolStripMenuItem( "&Kill" );
					killItem.Click += ( s, a ) => guardedOp( () => _ctrl.Send( new Net.KillAppMessage( id ) ) );
					killItem.Enabled = isAccessible && ( st.Running || st.Restarting );
					popup.Items.Add( killItem );

					var restartItem = new System.Windows.Forms.ToolStripMenuItem( "&Restart" );
					restartItem.Click += ( s, a ) => guardedOp( () => _ctrl.Send( new Net.RestartAppMessage( id ) ) );
					restartItem.Enabled = isAccessible; // && st.Running;
					popup.Items.Add( restartItem );

					if( appDef != null && appDef.Disabled )
					{
						var setEnabledItem = new System.Windows.Forms.ToolStripMenuItem( "&Enable" );
						setEnabledItem.Click += ( s, a ) => guardedOp( () => _ctrl.Send( new Net.SetAppEnabledMessage(  plan.Name, id, true ) ) );
						popup.Items.Add( setEnabledItem );
					}

					if( appDef != null )
					{
						var setEnabledItem = new System.Windows.Forms.ToolStripMenuItem( "&Disable" );
						setEnabledItem.Click += ( s, a ) => guardedOp( () => _ctrl.Send( new Net.SetAppEnabledMessage(  plan.Name, id, false ) ) );
						popup.Items.Add( setEnabledItem );
					}

					//var propsItem = new System.Windows.Forms.ToolStripMenuItem( "&Properties" );
					//propsItem.Click += ( s, a ) => guardedOp( () =>
					//{
					//	try{
					//	var appDef = planAppDefsDict.ContainsKey( id ) ? planAppDefsDict[id] : null;
					//	var serializer = new System.Runtime.Serialization.DataContractSerializer(typeof(AppDef));
					//	using var sw = new StringWriter();
					//	using var writer = new System.Xml.XmlTextWriter(sw);
					//	writer.Formatting = System.Xml.Formatting.Indented; // indent the Xml so it's human readable
					//	serializer.WriteObject(writer, appDef);
					//	writer.Flush();
					//	var xmlString = sw.ToString();
					//	MessageBox.Show(xmlString); 
					//	} catch( Exception ex )
					//	{
					//		int i =1;
					//	}
					//});
					//propsItem.Enabled = true;
					//popup.Items.Add( propsItem );



					popup.Show( Cursor.Position );

				}
				else if( e.Button == MouseButtons.Left )
				{
					// icon clicks
					if( currentCol == appTabColIconStart )
					{
						if( isAccessible ) // && !st.Running )
						{
							guardedOp( () => _ctrl.Send( new Net.StartAppMessage( id, null ) ) );
						}
					}

					if( currentCol == appTabColIconKill )
					{
						if( isAccessible ) // && st.Running )
						{
							guardedOp( () => _ctrl.Send( new Net.KillAppMessage( id ) ) );
						}
					}

					if( currentCol == appTabColIconRestart )
					{
						if( isAccessible ) // && st.Running )
						{
							guardedOp( () => _ctrl.Send( new Net.RestartAppMessage( id ) ) );
						}
					}

					if( currentCol == appTabColEnabled )
					{
						var wasEnabled = ( bool ) focused.Cells[currentCol].Value;
						if( plan != null )
						{
							guardedOp( () => _ctrl.Send( new Net.SetAppEnabledMessage(  plan.Name, id, !wasEnabled ) ) );
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
					if( col == appTabColName || col == appTabColStatus || col == appTabColPlan )  // just Name and Status columns
					{
						DataGridViewRow focused = gridApps.Rows[row];
						var id = new AppIdTuple( focused.Cells[0].Value as string );
						var st = _ctrl.GetAppState( id );

						guardedOp( () => _ctrl.Send( new Net.StartAppMessage( id, null ) ) );
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
