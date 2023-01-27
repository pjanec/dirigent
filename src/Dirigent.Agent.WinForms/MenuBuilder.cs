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
		protected PathPerspectivizer _pathPerspectivizer;

		public MenuBuilder( GuiCore core )
		{
			_core = core;
			_pathPerspectivizer = _core.ReflStates.PathPerspectivizer;
		}

		// returns a menu tree constructed from given action defs (where action.Title is the slash separated path in the menu tree)
		public List<MenuTreeNode> GetMenuItemsFromActions( IEnumerable<ActionDef> actions, Action<ActionDef> onClick )
		{
			var menuItems = new List<MenuTreeNode>();
			foreach( var action in actions )
			{
				var menuItem = WFT.ActionDefToMenuItem(action, (x) => onClick(x) );
				SetDefaultIconIfEmpty( ref menuItem, action );
				menuItems.Add( menuItem );
			}
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

		public List<MenuTreeNode> BuildVfsNodeActionsMenuItems( VfsNodeDef vfsNodeDef )
		{
			return GetMenuItemsFromActions(
				GetAllVfsNodeActions(vfsNodeDef),
				async (action) => await WFT.GuardedOpAsync( async () => {
						var resolved = await ReflStates.FileRegistry.ExpandPathsAsync( CtrlAsync, vfsNodeDef, true, null );
						if( resolved is not null )
						{
							_pathPerspectivizer.PerspectivizePath( resolved, EPathType.Auto );
							
							if ( !resolved.IsContainer )
							{
								_core.ToolsRegistry.StartFileBoundAction( Ctrl.Name, action, resolved ) ;
							}
							else
							{
								_core.ToolsRegistry.StartFilePackageBoundAction( Ctrl.Name, action, resolved ) ;
							}
						}
					}
				)
			);
		}

		public List<MenuTreeNode> BuildMachineActionsMenuItems( MachineDef machDef )
		{
			return GetMenuItemsFromActions(
				GetAllMachineActions(machDef),
					(action) => WFT.GuardedOp( () => {
						_core.ToolsRegistry.StartMachineBoundAction( Ctrl.Name, action, machDef ) ;
					}
				)
			);
		}

		public List<MenuTreeNode> BuildAppActionsMenuItems( AppDef appDef )
		{
			return GetMenuItemsFromActions(
				GetAllAppActions(appDef),
					(action) => WFT.GuardedOp( () => {
						_core.ToolsRegistry.StartAppBoundAction( Ctrl.Name, action, appDef ) ;
					}
				)
			);
		}


		MenuTreeNode BuildVfsNodeMenuItem( VfsNodeDef vfsNodeDef )
		{
			var title = vfsNodeDef.Title;
			if (string.IsNullOrEmpty( title )) title = vfsNodeDef.Id;
			var fileMenu = new MenuTreeNode( title, icon: vfsNodeDef.Icon );
			var submenus = BuildVfsNodeActionsMenuItems( vfsNodeDef );
			if( submenus.Count > 0 )
			{
				fileMenu.Children.AddRange( submenus );
				return fileMenu;
			}
			return null;
		}

		public List<MenuTreeNode> BuildVfsNodesMenuItems( IEnumerable<VfsNodeDef> vfsNodeDefs )
		{
			List<MenuTreeNode> items = new();
			foreach( var vfsNodeDef in vfsNodeDefs )
			{
				var item = BuildVfsNodeMenuItem(vfsNodeDef);
				if( item != null )
				{
					items.Add( item );
				}
			}
			return items;
		}
		
		public MenuTreeNode AssocMenuItemDefToMenuItem( AssocMenuItemDef mitem, Action<ActionDef> onClick )
		{
			if( mitem is ActionDef action)
			{
				var menuItem = WFT.ActionDefToMenuItem( action, onClick );
				SetDefaultIconIfEmpty( ref menuItem, action );
				return menuItem;
			}
			if( mitem is VfsNodeDef vsfNode )
			{
				return BuildVfsNodeMenuItem( vsfNode );
			}
			
			throw new Exception( $"Unsupported AssocMenuItem type {mitem.GetType().Name}" );
		}

		void SetDefaultIconIfEmpty( ref MenuTreeNode mtn, ActionDef action )
		{
			// set default icon if none is set
			if ( string.IsNullOrEmpty( mtn.Icon ) )
			{
				if (action is ToolActionDef toolAction)
				{
					mtn.Icon = _core.ToolsRegistry.GetToolIcon( toolAction.Name );
				}
			}
		}

	}
}
