using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dirigent.Tests
{
	[TestClass()]
	public class MenuTreeNodeTests
	{
		[TestMethod()]
		public void CombineMenuItemTest()
		{
			var nodes = new List<MenuTreeNode>()
			{
				new MenuTreeNode( "a/X/1" ),
				new MenuTreeNode( "///a\\/X/2///" ),
			};
			var tree = MenuTreeNode.CombineMenuItems( nodes );
			Assert.AreEqual( 1, tree.Children.Count );
			Assert.AreEqual( "a", tree.Children[0].Title );
			Assert.AreEqual( 1, tree.Children[0].Children.Count );
			Assert.AreEqual( "X", tree.Children[0].Children[0].Title );
			Assert.AreEqual( 2, tree.Children[0].Children[0].Children.Count );
			Assert.AreEqual( "1", tree.Children[0].Children[0].Children[0].Title );
			Assert.AreEqual( "2", tree.Children[0].Children[0].Children[1].Title );

		}
	}
}