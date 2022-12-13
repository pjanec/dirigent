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
			catch( Exception ex )
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

		/// <summary>
		///	Builds menu items from the children of given folder tree node.
		/// FolderTree payload needs to be a ToolStripMenuItem.
		/// </summary>
		public static ToolStripMenuItem[] GetMenuTreeItems( FolderTree parent )
		{
			var res = new List<ToolStripMenuItem>();
			
			//empty tree?
			if( parent.Children is null )
				return res.ToArray();
			
			foreach( var node in parent.Children )
			{
				if( node.IsFolder )
				{
					var item = new ToolStripMenuItem( node.Name );
					item.DropDownItems.AddRange( GetMenuTreeItems( node ) );
					res.Add( item );
				}
				else
				if(node.Payload is ToolStripMenuItem menuItem )
				{
					res.Add( menuItem );
				}
				else
				{
					throw new Exception( "GetMenuItems called with non-menuitem payload" );
				}
			}
			return res.ToArray();
		}

		public static ToolStripMenuItem ActionDefToMenuItem( ActionDef adef, Action<ActionDef> onClick )
		{
			var title = adef.Title;
			if (string.IsNullOrEmpty( title )) title = adef.Name;
			if (string.IsNullOrEmpty( title )) title = adef.Guid.ToString();

			return new ToolStripMenuItem(
				FolderTree.GetNamePart( title ),
				WFT.GetBitmapFromFile( adef.IconFile ),
				(sender, args ) => onClick( adef )
			);
		}


	}
}
