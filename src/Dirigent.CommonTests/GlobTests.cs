using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Dirigent.Tests
{
	[TestClass()]
	public class GlobTests
	{
		[TestMethod()]
		public void ExpandAlternativesTest()
		{
			CollectionAssert.AreEquivalent(
				new List<string>() { "*.log" },
				Glob.ExpandAlternatives( "*.log" ) );

			CollectionAssert.AreEquivalent(
				new List<string>() { "*.log", "*.txt" },
				Glob.ExpandAlternatives( "*.{log,txt}" ) );

			CollectionAssert.AreEquivalent(
				new List<string>() { "a/x.log", "a/x.txt", "b/x.log", "b/x.txt" },
				Glob.ExpandAlternatives( "{a,b}/x.{log,txt}" ) );

			// nested
			CollectionAssert.AreEquivalent(
				new List<string>() { "x.log", "x.txt", "x.gz" },
				Glob.ExpandAlternatives( "x.{{log,txt},gz}" ) );

			// unbalanced braces are taken literally
			CollectionAssert.AreEquivalent(
				new List<string>() { "x.{log" },
				Glob.ExpandAlternatives( "x.{log" ) );
		}

		[TestMethod()]
		public void IsMatchSingleSegmentTest()
		{
			Assert.IsTrue( Glob.IsMatch( "app.log", "*.log" ) );
			Assert.IsTrue( Glob.IsMatch( "app.LOG", "*.log" ) ); // case insensitive
			Assert.IsTrue( Glob.IsMatch( "a1c.log", "a?c.log" ) );
			Assert.IsFalse( Glob.IsMatch( "app.txt", "*.log" ) );

			// a single '*' does not cross the segment boundary
			Assert.IsFalse( Glob.IsMatch( "sub/app.log", "*.log" ) );
		}

		[TestMethod()]
		public void IsMatchDoubleStarTest()
		{
			Assert.IsTrue( Glob.IsMatch( "app.log", "**/*.log" ) );          // zero segments
			Assert.IsTrue( Glob.IsMatch( "sub/app.log", "**/*.log" ) );
			Assert.IsTrue( Glob.IsMatch( "a/b/c/app.log", "**/*.log" ) );
			Assert.IsFalse( Glob.IsMatch( "a/b/c/app.txt", "**/*.log" ) );

			// '**' in the middle
			Assert.IsTrue( Glob.IsMatch( "logs/app.log", "logs/**/*.log" ) );
			Assert.IsTrue( Glob.IsMatch( "logs/2026/08/app.log", "logs/**/*.log" ) );
			Assert.IsFalse( Glob.IsMatch( "other/app.log", "logs/**/*.log" ) );

			// trailing '**' matches everything below
			Assert.IsTrue( Glob.IsMatch( "logs/a/b/whatever.bin", "logs/**" ) );

			// backslashes work as separators too
			Assert.IsTrue( Glob.IsMatch( @"logs\2026\app.log", "logs/**/*.log" ) );
		}

		[TestMethod()]
		public void IsMatchWholePathTest()
		{
			// the pattern has to match the whole relative path, not just its beginning
			Assert.IsFalse( Glob.IsMatch( "logs/app.log", "logs" ) );
			Assert.IsTrue( Glob.IsMatch( "logs/app.log", "logs/app.log" ) );
		}

		[TestMethod()]
		public void ParseMaskTest()
		{
			// empty mask = anything, at any depth
			Assert.IsTrue( Glob.IsMatchAny( "a/b/noextension", Glob.ParseMask( "" ) ) );
			Assert.IsTrue( Glob.IsMatchAny( "a/b/noextension", Glob.ParseMask( null ) ) );

			// "*.*" keeps its Win32 meaning of "anything", including the files with no extension
			Assert.IsTrue( Glob.IsMatchAny( "noextension", Glob.ParseMask( "*.*" ) ) );

			// a plain file name mask applies at any depth (backward compatibility)
			var mask = Glob.ParseMask( "*.log" );
			Assert.IsTrue( Glob.IsMatchAny( "app.log", mask ) );
			Assert.IsTrue( Glob.IsMatchAny( "sub/dir/app.log", mask ) );
			Assert.IsFalse( Glob.IsMatchAny( "sub/dir/app.txt", mask ) );

			// a mask with a separator is matched against the whole relative path
			var pathMask = Glob.ParseMask( "logs/*.log" );
			Assert.IsTrue( Glob.IsMatchAny( "logs/app.log", pathMask ) );
			Assert.IsFalse( Glob.IsMatchAny( "other/app.log", pathMask ) );

			// alternatives
			var altMask = Glob.ParseMask( "**/*.{log,txt}" );
			Assert.IsTrue( Glob.IsMatchAny( "a/app.log", altMask ) );
			Assert.IsTrue( Glob.IsMatchAny( "a/app.txt", altMask ) );
			Assert.IsFalse( Glob.IsMatchAny( "a/app.bin", altMask ) );
		}
	}
}
