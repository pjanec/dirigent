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

		ContextMenuStrip mnuPlanList;  // context menu for the 'Open' toolbar button


		private void aboutMenuItem_Click( object sender, EventArgs e )
		{
			var version = Assembly.GetExecutingAssembly().GetName().Version;

			// read the content of versionstamp file next to dirigent binaries
			var verStampPath = System.IO.Path.Combine( Tools.GetExeDir(), "VersionStamp.txt" );
			string verStampText;
			try
			{

				verStampText = File.ReadAllText( verStampPath );
			}
			catch( Exception )
			{
				verStampText = "Version info file not found:\n" + verStampPath;
			}

			MessageBox.Show(
				"Dirigent app launcher\nby pjanec\nMIT license\n\nver." + version + "\n\n" + verStampText,
				"About Dirigent",
				MessageBoxButtons.OK,
				MessageBoxIcon.Information );
		}

		private void ShowNoPlanSelectedError()
		{
			MessageBox.Show(
				"No plan selected. Select a plan first.",
				"Dirigent",
				MessageBoxButtons.OK,
				MessageBoxIcon.Information );
		}

		private void startPlanMenuItem_Click( object sender, EventArgs e )
		{
			if( _currentPlan is null )
			{
				ShowNoPlanSelectedError();
				return;
			}
			guardedOp( () => _ctrl.Send( new Net.StartPlanMessage( _currentPlan.Name ) ) );
		}

		private void stopPlanMenuItem_Click( object sender, EventArgs e )
		{
			if( _currentPlan is null )
			{
				ShowNoPlanSelectedError();
				return;
			}
			guardedOp( () => _ctrl.Send( new Net.StopPlanMessage( _currentPlan.Name ) ) );
		}

		private void killPlanMenuItem_Click( object sender, EventArgs e )
		{
			if( _currentPlan is null )
			{
				ShowNoPlanSelectedError();
				return;
			}
			guardedOp( () => _ctrl.Send( new Net.KillPlanMessage( _currentPlan.Name ) ) );
		}

		private void restartPlanMenuItem_Click( object sender, EventArgs e )
		{
			if( _currentPlan is null )
			{
				ShowNoPlanSelectedError();
				return;
			}
			guardedOp( () => _ctrl.Send( new Net.RestartPlanMessage( _currentPlan.Name ) ) );
		}

		private void selectPlanMenuItem_Click( object sender, EventArgs e )
		{
			//selectPlanToolStripMenuItem.ShowDropDown();
			if( mnuPlanList is not null )
			{
				mnuPlanList.Show( this, this.PointToClient( Cursor.Position ) );
			}
		}

		void populatePlanSelectionMenu()
		{
			mnuPlanList = new ContextMenuStrip();

			selectPlanToolStripMenuItem.DropDownItems.Clear();

			// fill the Plan -> Load menu with items
			int index = 0;
			foreach( var plan in _planRepo )
			{
				index++;

				var planName = plan.Name; // independent variable to be remebered by the lambda below
				EventHandler clickHandler = ( sender, args ) => guardedOp( () => { _currentPlan = _ctrl.GetPlanDef( planName ); } );

				var itemText = String.Format( "&{0}: {1}", index, plan.Name );
				var menuItem = new System.Windows.Forms.ToolStripMenuItem( itemText, null, clickHandler );
				selectPlanToolStripMenuItem.DropDownItems.Add( menuItem );

				mnuPlanList.Items.Add( itemText, null, clickHandler );
			}
		}


	}
}
