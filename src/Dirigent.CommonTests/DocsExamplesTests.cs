using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Dirigent.Tests
{
	/// <summary>
	/// The XML examples of the documentation that claim to be complete really load, and mean what the text around
	/// them says.
	/// </summary>
	/// <remarks>
	/// An example is marked by the line <c>&lt;!-- example: loads --&gt;</c> directly before its ```xml fence.
	/// Whatever is marked is put into a <c>&lt;Shared&gt;</c> (unless it is one already) and read by the
	/// real reader, and every <c>&lt;FileRef&gt;</c> in it has to find at least one definition - a package
	/// that collects nothing is the commonest way for an example to be wrong. Fragments that need a
	/// context (a machine, an app) or other examples are simply not marked.
	/// Failures name file and line, so that the page to fix is one click away.
	/// </remarks>
	[TestClass()]
	public class DocsExamplesTests
	{
		public const string Marker = "<!-- example: loads -->";

		static readonly string[] Pages =
		{
			"Files.md", "SharedConfig.md", "LogFileCollection.md", "MarkAndClear.md", "Plans.md", "Apps.md",
		};

		static string DocsFolder()
		{
			var dir = new DirectoryInfo( AppContext.BaseDirectory );
			while( dir is not null && !File.Exists( Path.Combine( dir.FullName, "version.txt" ) ) )
				dir = dir.Parent;

			Assert.IsNotNull( dir, "could not find the repository root above " + AppContext.BaseDirectory );
			return Path.Combine( dir!.FullName, "docs" );
		}

		/// <summary>The marked examples: page, line of the first line of XML, the XML.</summary>
		static List<(string Page, int Line, string Xml)> MarkedExamples()
		{
			var res = new List<(string, int, string)>();

			foreach( var page in Pages )
			{
				var path = Path.Combine( DocsFolder(), page );
				if( !File.Exists( path ) ) continue;

				var lines = File.ReadAllLines( path );
				for( int i = 0; i + 1 < lines.Length; i++ )
				{
					if( lines[i].Trim() != Marker || !lines[i + 1].TrimStart().StartsWith( "```xml" ) )
						continue;

					var body = new List<string>();
					int j = i + 2;
					while( j < lines.Length && !lines[j].TrimStart().StartsWith( "```" ) )
						body.Add( lines[j++] );

					res.Add( ( page, i + 3, string.Join( "\n", body ) ) );
				}
			}

			return res;
		}

		static SharedConfig Load( string xml )
		{
			var text = xml.TrimStart().StartsWith( "<Shared" ) ? xml : "<Shared>\n" + xml + "\n</Shared>";
			return new SharedConfigReader( new StringReader( text ) ).Config;
		}

		static IEnumerable<VfsNodeDef> Walk( VfsNodeDef n )
		{
			yield return n;
			foreach( var c in n.Children )
				foreach( var d in Walk( c ) )
					yield return d;
		}

		[TestMethod()]
		public void EveryExampleMarkedAsCompleteLoadsAndItsReferencesFindSomethingTest()
		{
			var examples = MarkedExamples();
			Assert.IsTrue( examples.Count >= 10, $"only {examples.Count} marked examples were found - did the marker change?" );

			var failures = new List<string>();

			foreach( var (page, line, xml) in examples )
			{
				try
				{
					var cfg = Load( xml );

					var all = cfg.VfsNodes.Concat( cfg.Machines.SelectMany( m => m.VfsNodes ) )
								.SelectMany( Walk ).Distinct().ToList();

					foreach( var fr in all.OfType<FileRef>() )
					{
						var hits = cfg.VfsNodes.Count( x => !ReferenceEquals( x, fr )
							&& Scripts.BuiltIn.ListVfsNodes.IsMatch( fr.Id, x.Id )
							&& Scripts.BuiltIn.ListVfsNodes.IsMatch( fr.MachineId, x.MachineId )
							&& Scripts.BuiltIn.ListVfsNodes.IsMatch( fr.AppId, x.AppId ) );

						if( hits == 0 )
							failures.Add( $"{page}:{line}: <FileRef Id=\"{fr.Id}\" MachineId=\"{fr.MachineId}\" AppId=\"{fr.AppId}\"> finds nothing" );
					}
				}
				catch( Exception e )
				{
					failures.Add( $"{page}:{line}: {Tools.JustFirstLine( e.Message )}" );
				}
			}

			Assert.AreEqual( 0, failures.Count, "examples that do not hold:\n" + string.Join( "\n", failures ) );
		}

		[TestMethod()]
		public void TheWalkthroughMeansWhatItSaysTest()
		{
			// the claims of "Setting up file collection, step by step" in Files.md that are not just "it loads"
			var examples = MarkedExamples().Where( e => e.Page == "Files.md" ).ToList();

			var step3 = Load( examples.Single( e => e.Xml.Contains( "pkg.logs" ) ).Xml );

			// log.* takes the logs of the apps of every machine, not the machine-level dirigent.log
			var logs = step3.VfsNodes.Where( n => Scripts.BuiltIn.ListVfsNodes.IsMatch( "log.*", n.Id ) ).ToList();
			CollectionAssert.AreEquivalent( new[] { "m1", "m2" }, logs.Select( n => n.MachineId ).ToList() );
			Assert.IsTrue( logs.All( n => n.AppId == "viewer" ), "kinds apart: the machine log is dirigent.*, not log.*" );

			// the trap: AppId="*" also matches a machine-level definition, "?*" only one that has an app
			var machineLevel = step3.VfsNodes.Single( n => n.Id == "dirigent.log" );
			Assert.IsTrue( Scripts.BuiltIn.ListVfsNodes.IsMatch( "*", machineLevel.AppId ) );
			Assert.IsFalse( Scripts.BuiltIn.ListVfsNodes.IsMatch( "?*", machineLevel.AppId ) );
			Assert.IsTrue( Scripts.BuiltIn.ListVfsNodes.IsMatch( "?*", logs[0].AppId ) );

			// step 4: the package of the template is on every machine with that machine's references
			var step4 = Load( examples.Single( e => e.Xml.Contains( "m.base" ) && e.Xml.Contains( "pkg.machine" ) ).Xml );
			foreach( var machine in new[] { "m1", "m2" } )
			{
				var pkg = step4.VfsNodes.Single( n => n.Id == "pkg.machine" && n.MachineId == machine );
				Assert.IsTrue( pkg.Children.OfType<FileRef>().All( r => r.MachineId == machine ), $"{machine}: the references take the machine of the template's user" );
			}

			// step 5: the app's own definition replaced the inherited one, and the new one was added
			var step5 = Load( examples.Single( e => e.Xml.Contains( "log.viewer.crash" ) ).Xml );
			var m2 = step5.AppDefaults.Single( a => a.Id.ToString() == "m2.viewer" ).VfsNodes;
			CollectionAssert.AreEqual( new[] { "log.viewer", "log.viewer.crash" }, m2.Select( n => n.Id ).ToList() );
			Assert.AreEqual( "Log/Viewer (last 50)", m2[0].Title );
			Assert.AreEqual( "Log/Viewer", step5.AppDefaults.Single( a => a.Id.ToString() == "m1.viewer" ).VfsNodes.Single().Title );

			// step 6: the titles
			var step6 = Load( examples.Single( e => e.Xml.Contains( "RenderHost-3801" ) ).Xml );
			string Title( string id, string app ) => step6.VfsNodes.Single( n => n.Id == id && n.AppId == app ).Title;
			Assert.AreEqual( "Log/RenderHost-3801", Title( "log.ig", "ig1" ) );
			Assert.AreEqual( "Log/RenderHost-3802", Title( "log.ig", "ig2" ) );
			Assert.AreEqual( "Config/primary (ig1)", Title( "cfg.app.ig", "ig1" ) );
			Assert.AreEqual( "Config/backup (ig2)", Title( "cfg.app.ig", "ig2" ) );
		}
	}
}
