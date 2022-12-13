
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
using System.Threading;

namespace Dirigent.Gui.WinForms
{
	public class MainExtension
	{
		protected frmMain _form;
		protected GuiCore _core;
		protected Net.Client Client => _core.Client;
		protected IDirig Ctrl => _core.Ctrl;
		protected IDirigAsync CtrlAsync => _core.CtrlAsync;
		protected ReflectedStateRepo ReflStates => _core.ReflStates;
		protected List<PlanDef> PlanRepo => _core.PlanRepo;
		protected PlanDef CurrentPlan { get { return _core.CurrentPlan; } set { _core.CurrentPlan = value; } }
		protected List<ScriptDef> ScriptRepo => _core.ScriptRepo;



		protected readonly Bitmap _iconStart = WFT.ResizeImage( new Bitmap( Resources.play ), new Size( 20, 20 ) );
		protected readonly Bitmap _iconStop = WFT.ResizeImage( new Bitmap( Resources.stop ), new Size( 20, 20 ) );
		protected readonly Bitmap _iconKill = WFT.ResizeImage( new Bitmap( Resources.delete ), new Size( 20, 20 ) );
		protected readonly Bitmap _iconRestart = WFT.ResizeImage( new Bitmap( Resources.refresh ), new Size( 20, 20 ) );


		public MainExtension( frmMain form, GuiCore core )
		{
			_form = form;
			_core = core;
		}

		// returns a menu tree constructed from given action defs (where action.Title is the slash separated path in the menu tree)
		public ToolStripMenuItem[] GetMenuItemsFromActions( IEnumerable<ActionDef> actions, Action<ActionDef> onClick )
		{
			var tree = new FolderTree();

			foreach( var a in actions )
			{
				var menuItem = WFT.ActionDefToMenuItem(a, (x) => onClick(x) );
				tree.InsertNode( a.Title, false, menuItem, null);
			}

			// convert the actions to menu items
			var menuItems = WFT.GetMenuTreeItems( tree );

			return menuItems;

		}

		public ToolStripMenuItem[] MenuVfsNodeActions( VfsNodeDef vfsNodeDef )
		{
			return GetMenuItemsFromActions(
				vfsNodeDef.Actions,
				async (action) => await WFT.GuardedOpAsync( async () => {
						var resolved = await ReflStates.FileRegistry.ResolveAsync( CtrlAsync, vfsNodeDef, null, CancellationToken.None );
						_core.ToolsRegistry.StartFileBoundAction( action, resolved ) ;
					}
				)
			);
		}

		public ToolStripMenuItem ContextMenuFilePackage( FilePackageDef fpack )
		{
			var toolsMenu = new System.Windows.Forms.ToolStripMenuItem(
				"&Tools",
				null,
				GetMenuItemsFromActions(
					fpack.Actions,
					async (action) => await WFT.GuardedOpAsync( async () => {
						var resolved = await ReflStates.FileRegistry.ResolveAsync( CtrlAsync, fpack, null, CancellationToken.None );
						_core.ToolsRegistry.StartFilePackageBoundAction( action, resolved );
						}
					)
				)
			);

			if( toolsMenu.DropDownItems.Count > 0 )
			{
				return toolsMenu;
			}
			return null;
		}

		public ToolStripMenuItem MenuVfsNode( VfsNodeDef vfsNodeDef )
		{
			var title = vfsNodeDef.Title;
			if (string.IsNullOrEmpty( title )) title = vfsNodeDef.Id;
			var fileMenu = new ToolStripMenuItem( title );
			var submenus = MenuVfsNodeActions( vfsNodeDef );
			if( submenus.Length > 0 )
			{
				//fileMenu.DropDownItems.Add ( toolsSubmenu );
				fileMenu.DropDownItems.AddRange( submenus );
				return fileMenu;
			}
			return null;
		}

		protected ToolStripMenuItem ContextMenuVfsNodes( IEnumerable<VfsNodeDef> vfsNodeDefs )
		{
			var filesMenu = new System.Windows.Forms.ToolStripMenuItem( "&Files/Folders" );
			foreach( var vfsNodeDef in vfsNodeDefs )
			{
				var item =  MenuVfsNode(vfsNodeDef);
				if( item != null )
				{
					filesMenu.DropDownItems.Add( item );
				}
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
		
		public ToolStripMenuItem AssocMenuItemDefToMenuItem( AssocMenuItemDef mitem, Action<ActionDef> onClick )
		{
			if( mitem is ActionDef action)
			{
				return WFT.ActionDefToMenuItem( action, onClick );
			}
			if( mitem is VfsNodeDef vsfNode )
			{
				return MenuVfsNode( vsfNode );
			}
			
			throw new Exception( $"Unsupported AssocMenuItem type {mitem.GetType().Name}" );
		}

	}
}
