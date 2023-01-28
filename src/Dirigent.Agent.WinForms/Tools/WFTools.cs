using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Dirigent.Gui.WinForms
{
	public static class WFT
	{
		private static readonly log4net.ILog log = log4net.LogManager.GetLogger
				( System.Reflection.MethodBase.GetCurrentMethod().DeclaringType );

		public static DataRow GetDataRowFromGridRow( DataGridViewRow gridRow )
		{
			var drv = gridRow.DataBoundItem as DataRowView;
			var dataRow = drv.Row;
			return dataRow;
		}

		/// <summary>
		/// Executes a delegate and show exception window on exception
		/// </summary>
		public static void GuardedOp( MethodInvoker mi )
		{
			try
			{
				mi.Invoke();
			}
			catch( Exception ex )
			{
				log.Error( ex );
				ExceptionDialog.showExceptionWithStackTrace( ex, "Exception", "" );
			}
		}

		// this one properly handles exception in async code
		public async static Task GuardedOpAsync( Func<Task> asyncAction )
		{
			try
			{
				await asyncAction();
			}
			catch (TaskCanceledException) {} // ignore }
			//catch (TargetInvocationException ex )
			//{
			//	log.Error( ex.InnerException );
			//	ExceptionDialog.showExceptionWithStackTrace( ex.InnerException, "Exception", "" );
			//}
			catch ( Exception ex )
			{
				log.Error( ex );
				ExceptionDialog.showExceptionWithStackTrace( ex, "Exception", "" );
			}
		}

		public static Bitmap ResizeImage( Bitmap imgToResize, Size size )
		{
			try
			{
				Bitmap b = new Bitmap( size.Width, size.Height );
				using( Graphics g = Graphics.FromImage( b ) )
				{
					g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
					g.DrawImage( imgToResize, 0, 0, size.Width, size.Height );
				}
				return b;
			}
			catch { }
			return null;
		}

		public static void setDoubleBuffered( Control control, bool enable )
		{
			var doubleBufferPropertyInfo = control.GetType().GetProperty( "DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic );
			if( doubleBufferPropertyInfo != null )
			{
				doubleBufferPropertyInfo.SetValue( control, enable, null );
			}
		}

		public static Bitmap GetBitmapFromFile( string fileName )
		{
			if (string.IsNullOrEmpty( fileName ))
				return null;
				
			try
			{
				if (System.IO.Path.IsPathRooted( fileName ))
				{
					return new Bitmap( fileName );
				}
				else
				{
					return new Bitmap( System.IO.Path.Combine( Tools.GetExeDir(), fileName ) );
				}
			}
			catch (Exception)
			{
				return null;
			}
		}


		// Convert a tree of menu items into a tree of tool strips.
		// Note: if a menu item is both a leaf (having action) and a parent (having submenus), we create 2 separate strips
		public static ToolStripItem[] MenuItemToToolStrips( MenuTreeNode menuItem )
		{
			var res = new List<ToolStripItem>();

			if( menuItem.Title.StartsWith( "---" ) )
			{
				return new ToolStripSeparator[] { new ToolStripSeparator() };
			}

			if( menuItem.Action != null )
			{
				// action menu item first
				var stripItem = new ToolStripMenuItem(
					menuItem.Title
					, GetBitmapFromFile( menuItem.Icon )
					, ( s, e ) => menuItem.Action?.Invoke()
				);
				res.Add( stripItem );
				
			}
			
			if( menuItem.Children.Count > 0)
			{

				var stripItem = new ToolStripMenuItem(
					menuItem.Title
					, GetBitmapFromFile( menuItem.Icon )
					, ( s, e ) => menuItem.Action?.Invoke() // this is never called as this is a parent menu item with submenus
				);

				stripItem.DropDownItems.AddRange( MenuItemsToToolStrips( menuItem.Children ).ToArray() );

				res.Add( stripItem );
			}
			
			return res.ToArray();
		}
		

		public static List<ToolStripItem> MenuItemsToToolStrips( List<MenuTreeNode> menuItems )
		{
			var res = new List<ToolStripItem>();
			foreach( var item in menuItems )
			{
				res.AddRange( MenuItemToToolStrips( item ) );
			}
			return res;
		}
		

		public static MenuTreeNode ActionDefToMenuItem( ActionDef adef, Action<ActionDef> onClick )
		{
			var title = adef.Title;
			if (string.IsNullOrEmpty( title )) title = adef.Name;
			if (string.IsNullOrEmpty( title )) title = adef.Guid.ToString();

			var menuItemWithSegmentedTitle = new MenuTreeNode(
				title,
				adef.Icon,
				() => onClick( adef )
			);

			return MenuTreeNode.MakeTreeFromTitle( menuItemWithSegmentedTitle );
		}

	}
}
