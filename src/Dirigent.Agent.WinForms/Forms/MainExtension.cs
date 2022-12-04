
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
using Dirigent.Gui.WinForms.Properties;

namespace Dirigent.Gui.WinForms
{
	public class MainExtension
	{
		protected frmMain _form;
		protected Net.Client Client => _form.Client;
		protected IDirig Ctrl => _form.Ctrl;
		protected ReflectedStateRepo ReflStates => _form.ReflStates;
		protected List<PlanDef> PlanRepo => _form.PlanRepo;
		protected PlanDef CurrentPlan { get { return _form.CurrentPlan; } set { _form.CurrentPlan = value; } }
		protected List<ScriptDef> ScriptRepo => _form.ScriptRepo;


		protected readonly Bitmap _iconStart = WFT.ResizeImage( new Bitmap( Resources.play ), new Size( 20, 20 ) );
		protected readonly Bitmap _iconStop = WFT.ResizeImage( new Bitmap( Resources.stop ), new Size( 20, 20 ) );
		protected readonly Bitmap _iconKill = WFT.ResizeImage( new Bitmap( Resources.delete ), new Size( 20, 20 ) );
		protected readonly Bitmap _iconRestart = WFT.ResizeImage( new Bitmap( Resources.refresh ), new Size( 20, 20 ) );


		public MainExtension( frmMain form )
		{
			_form = form;
		}

		protected ToolStripMenuItem ContextMenuFile( FileDef fileDef )
		{
			var toolsMenu = new System.Windows.Forms.ToolStripMenuItem( "&Tools" );
			foreach( var tool in fileDef.Tools )
			{
				var title = tool.Title;
				if (string.IsNullOrEmpty( title )) title = tool.Id;
				var item = new System.Windows.Forms.ToolStripMenuItem( title );
				item.Click += ( s, a ) => WFT.GuardedOp( () => {
						var resolved = ReflStates.FileRegistry.Resolve( fileDef, null );
						_form.ToolsRegistry.StartFileBoundTool( tool, resolved ) ;
					}
				);
				toolsMenu.DropDownItems.Add( item );
			}
			if( toolsMenu.DropDownItems.Count > 0 )
			{
				return toolsMenu;
			}
			return null;
		}

		protected ToolStripMenuItem ContextMenuFilePackage( FilePackageDef fpack )
		{
			var toolsMenu = new System.Windows.Forms.ToolStripMenuItem( "&Tools" );
			foreach( var tool in fpack.Tools )
			{
				var title = tool.Title;
				if (string.IsNullOrEmpty( title )) title = tool.Id;
				var item = new System.Windows.Forms.ToolStripMenuItem( title );
				item.Click += ( s, a ) => WFT.GuardedOp( () => {
					var resolved = ReflStates.FileRegistry.Resolve( fpack, null );
					_form.ToolsRegistry.StartFilePackageBoundTool( tool, resolved );
					//try{
					//	var result = await _form.ReflStates.Scripts.RunScriptAndWait<DemoScript1.Result>( "m1", "Scripts/DemoScript1.cs", null, null, "Demo1", System.Threading.CancellationToken.None, -1 );
					//	MessageBox.Show( result.ToString() );
					//}
					//catch (Exception e)
					//{
					//	MessageBox.Show( e.ToString() );
					//}
				}
				);
				toolsMenu.DropDownItems.Add( item );
			}
			if( toolsMenu.DropDownItems.Count > 0 )
			{
				return toolsMenu;
			}
			return null;
		}

		protected ToolStripMenuItem ContextMenuFiles( IEnumerable<FileDef> fileDefs )
		{
			var filesMenu = new System.Windows.Forms.ToolStripMenuItem( "&Files" );
			foreach( var fileDef in fileDefs )
			{
				var title = fileDef.Title;
				if (string.IsNullOrEmpty( title )) title = fileDef.Id;
				var fileMenu = new ToolStripMenuItem( title );
				var toolsSubmenu = ContextMenuFile( fileDef );
				if( toolsSubmenu != null )
				{
					//fileMenu.DropDownItems.Add ( toolsSubmenu );
					fileMenu.DropDownItems.AddRange( toolsSubmenu.DropDownItems );
				}
				filesMenu.DropDownItems.Add( fileMenu );
			}
			return filesMenu;
		}
		
		protected ToolStripMenuItem ContextMenuFilePackages( IEnumerable<FilePackageDef> fpackDefs )
		{
			var fpacksMenu = new System.Windows.Forms.ToolStripMenuItem( "&Packages" );
			foreach( var fpack in fpackDefs )
			{
				var title = fpack.Title;
				if (string.IsNullOrEmpty( title )) title = fpack.Id;
				var fpackMenu = new ToolStripMenuItem( title );
				var toolsSubmenu = ContextMenuFilePackage( fpack );
				if( toolsSubmenu != null )
				{
					//fileMenu.DropDownItems.Add ( toolsSubmenu );
					fpackMenu.DropDownItems.AddRange( toolsSubmenu.DropDownItems );
				}
				fpacksMenu.DropDownItems.Add( fpackMenu );
			}
			return fpacksMenu;
		}
		

	}
}
