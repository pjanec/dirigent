using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
using log4net;
using System.Xml.Linq;
using System.Xml.XPath;
using System.Windows.Forms;

namespace Dirigent.Gui.WinForms
{
	public class MenuBuilder
	{
		protected GuiCore _core;
		protected Net.Client Client => _core.Client;
		protected IDirig Ctrl => _core.Ctrl;
		protected IDirigAsync CtrlAsync => _core.CtrlAsync;
		protected ReflectedStateRepo ReflStates => _core.ReflStates;
		protected List<PlanDef> PlanRepo => _core.PlanRepo;
		protected List<ScriptDef> ScriptRepo => _core.ScriptRepo;

		public MenuBuilder( GuiCore core )
		{
			_core = core;
		}

		// returns a menu tree constructed from given action defs (where action.Title is the slash separated path in the menu tree)
		public ToolStripMenuItem[] GetMenuItemsFromActions( IEnumerable<ActionDef> actions, Action<ActionDef> onClick )
		{
			var tree = new TreeNode();

			foreach( var a in actions )
			{
				var menuItem = WFT.ActionDefToMenuItem(a, (x) => onClick(x) );
				tree.InsertNode( a.Title, false, menuItem, null);
			}

			// convert the actions to menu items
			var menuItems = WFT.GetMenuTreeItems( tree );

			return menuItems;

		}

		// including the default ones
		IEnumerable<ActionDef> GetAllVfsNodeActions( VfsNodeDef vfsNodeDef )
		{
			// first the default ones
			if( _core.LocalConfig is not null )
			{
				if (vfsNodeDef.IsContainer)
				{
					foreach( var a in _core.LocalConfig.DefaultFilePackageActions )
					{
						yield return a;
					}
				}
				else
				{
					foreach( var a in _core.LocalConfig.DefaultFileActions )
					{
						yield return a;
					}
				}
			}
			
			// then the ones from the shared config
			foreach( var a in vfsNodeDef.Actions )
			{
				yield return a;
			}
		}

		// including the default ones
		IEnumerable<ActionDef> GetAllAppActions( AppDef appDef )
		{
			// first the default ones
			if( _core.LocalConfig is not null )
			{
				foreach( var a in _core.LocalConfig.DefaultAppActions )
				{
					yield return a;
				}
			}
			
			// then the ones from the shared config
			foreach( var a in appDef.Actions )
			{
				yield return a;
			}
		}

		// including the default ones
		IEnumerable<ActionDef> GetAllMachineActions( MachineDef machDef )
		{
			// first the default ones
			if( _core.LocalConfig is not null )
			{
				foreach( var a in _core.LocalConfig.DefaultMachineActions )
				{
					yield return a;
				}
			}
			
			// then the ones from the shared config
			foreach( var a in machDef.Actions )
			{
				yield return a;
			}
		}

		public ToolStripMenuItem[] BuildVfsNodeActionsMenuItems( VfsNodeDef vfsNodeDef )
		{
			return GetMenuItemsFromActions(
				GetAllVfsNodeActions(vfsNodeDef),
				async (action) => await WFT.GuardedOpAsync( async () => {
						var resolved = await ReflStates.FileReg.ResolveAsync( CtrlAsync, vfsNodeDef, false, true, null );
						if( !vfsNodeDef.IsContainer )
						{
							_core.ToolsRegistry.StartFileBoundAction( Ctrl.Name, action, resolved ) ;
						}
						else
						{
							_core.ToolsRegistry.StartFilePackageBoundAction( Ctrl.Name, action, resolved ) ;
						}
					}
				)
			);
		}

		public ToolStripMenuItem[] BuildMachineActionsMenuItems( MachineDef machDef )
		{
			return GetMenuItemsFromActions(
				GetAllMachineActions(machDef),
					(action) => WFT.GuardedOp( () => {
						_core.ToolsRegistry.StartMachineBoundAction( Ctrl.Name, action, machDef ) ;
					}
				)
			);
		}

		public ToolStripMenuItem[] BuildAppActionsMenuItems( AppDef appDef )
		{
			return GetMenuItemsFromActions(
				GetAllAppActions(appDef),
					(action) => WFT.GuardedOp( () => {
						_core.ToolsRegistry.StartAppBoundAction( Ctrl.Name, action, appDef ) ;
					}
				)
			);
		}


		ToolStripMenuItem BuildVfsNodeMenuItem( VfsNodeDef vfsNodeDef )
		{
			var title = vfsNodeDef.Title;
			if (string.IsNullOrEmpty( title )) title = vfsNodeDef.Id;
			var fileMenu = new ToolStripMenuItem( title );
			var submenus = BuildVfsNodeActionsMenuItems( vfsNodeDef );
			if( submenus.Length > 0 )
			{
				//fileMenu.DropDownItems.Add ( toolsSubmenu );
				fileMenu.DropDownItems.AddRange( submenus );
				return fileMenu;
			}
			return null;
		}

		public ToolStripMenuItem[] BuildVfsNodesMenuItems( IEnumerable<VfsNodeDef> vfsNodeDefs )
		{
			List<ToolStripMenuItem> items = new();
			foreach( var vfsNodeDef in vfsNodeDefs )
			{
				var item = BuildVfsNodeMenuItem(vfsNodeDef);
				if( item != null )
				{
					items.Add( item );
				}
			}
			return items.ToArray();
		}
		
		public ToolStripMenuItem AssocMenuItemDefToMenuItem( AssocMenuItemDef mitem, Action<ActionDef> onClick )
		{
			if( mitem is ActionDef action)
			{
				return WFT.ActionDefToMenuItem( action, onClick );
			}
			if( mitem is VfsNodeDef vsfNode )
			{
				return BuildVfsNodeMenuItem( vsfNode );
			}
			
			throw new Exception( $"Unsupported AssocMenuItem type {mitem.GetType().Name}" );
		}
	}
}
