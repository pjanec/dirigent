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

		/// <summary>
		/// Raised when a menu action started a script worth following - the instance and what to call
		/// it. Only actions started here, in this GUI, so that a progress indicator shows the
		/// operations of the person sitting in front of it and nobody else's.
		/// </summary>
		public event Action<Guid, string>? OperationStarted;

		public MenuBuilder( GuiCore core )
		{
			_core = core;
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

		/// <summary>
		/// Scripts that take a node DEFINITION and resolve it themselves, so the resolve happens
		/// inside the tracked operation instead of in front of it.
		/// </summary>
		static bool ResolvesItsOwnNode( ActionDef action )
			=> action is ScriptActionDef s && s.Name == Scripts.BuiltIn.DownloadZipped._Name;


		/// <summary>
		/// Shows the node's description and takes a note from the operator. Null means they cancelled,
		/// in which case nothing is started at all.
		/// </summary>
		/// <remarks>
		/// A menu click, not the message pump, so a modal dialog here holds nothing up. Asking before
		/// the start also means the answer can be handed to the script rather than sent after it.
		/// </remarks>
		string? AskForComment( VfsNodeDef vfsNodeDef, string title )
		{
			using var dlg = new frmCollectionComment( title, vfsNodeDef.Description );
			return dlg.ShowDialog() == DialogResult.OK ? dlg.Comment : null;
		}

		public List<MenuTreeNode> BuildVfsNodeActionsMenuItems( VfsNodeDef vfsNodeDef )
		{
			// Name the operation after the NODE, not the action: every download shares the action
			// title "Download zipped package", so with two running at once the status bar showed
			// two identical entries and neither said what it was.
			string OperationName( ActionDef action )
				=> !string.IsNullOrEmpty( vfsNodeDef.Title ) ? vfsNodeDef.Title
				 : !string.IsNullOrEmpty( vfsNodeDef.Id ) ? vfsNodeDef.Id
				 : action.Title;

			return GetMenuItemsFromActions(
				GetAllVfsNodeActions(vfsNodeDef),
				async (action) => await WFT.GuardedOpAsync( async () => {

						// Hand the definition straight over where the script can resolve it. This
						// is what keeps the operation whole: resolving a package that spans many
						// apps on two machines is one round trip per node, and doing it here first
						// left the status bar empty for seconds before the operation existed at all.
						if( ResolvesItsOwnNode( action ) && action is ScriptActionDef selfResolving )
						{
							string? comment = null;
							if( selfResolving.AskComment )
							{
								comment = AskForComment( vfsNodeDef, OperationName( action ) );
								if( comment is null ) return; // cancelled: nothing starts, nothing is shown
							}

							var own = _core.ToolsRegistry.StartSelfResolvingScriptAction(
											Ctrl.Name, selfResolving, vfsNodeDef, comment );
							OperationStarted?.Invoke( own, OperationName( action ) );
							return;
						}

						// everything else needs the resolved tree up front - a tool action builds
						// its FILE_PATH by walking it
						var resolved = await ReflStates.FileReg.ResolveAsync( CtrlAsync, vfsNodeDef, false, true, null );
						if( resolved is not null )
						{
							var script = !resolved.IsContainer
								? _core.ToolsRegistry.StartFileBoundAction( Ctrl.Name, action, resolved )
								: _core.ToolsRegistry.StartFilePackageBoundAction( Ctrl.Name, action, resolved );

							// a file action can take minutes (collecting logs from every machine),
							// so the operation gets a place in the status bar of the GUI that asked
							OperationStarted?.Invoke( script, OperationName( action ) );
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
