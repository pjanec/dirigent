using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using System;
using System.IO;
using System.Linq;

namespace Dirigent.Tests
{
	/// <summary>
	/// The Component attribute: the software component an application or a file definition belongs to, named explicitly -
	/// what tools group versions and collected files by, instead of guessing from Groups (which is UI grouping).
	/// </summary>
	[TestClass()]
	public class ComponentAttributeTests
	{
		static SharedConfig Parse( string xml )
			=> new SharedConfigReader( new StringReader( xml ) ).Config;

		const string Config = @"
			<Shared Name='Test site'>
				<Machine Name='m1' IP='127.0.0.1'/>
				<AppTemplate Name='t.viewer' Component='Viewer' Groups='Back end' ExeFullPath='a.exe'/>
				<AppTemplate Name='t.viewer.tool' Template='t.viewer' Groups='Tools'/>
				<Plan Name='p'>
					<App AppIdTuple='m1.sim' Template='t.viewer'/>
					<App AppIdTuple='m1.tool' Template='t.viewer.tool'/>
					<App AppIdTuple='m1.other' Template='t.viewer' Component='Broker'/>
					<App AppIdTuple='m1.none' ExeFullPath='b.exe'/>
				</Plan>
				<FilePackage Id='pkg.dds' Title='Components/CycloneDDS' Component='CycloneDDS'>
					<File Id='dds.log' MachineId='m1' Path='C:\dds.log'/>
				</FilePackage>
				<FilePackage Id='pkg.all' Title='Logs/All'/>
			</Shared>";

		static AppDef App( SharedConfig cfg, string app ) => cfg.Plans.Single().AppDefs.Single( a => a.Id.AppId == app );

		[TestMethod()]
		public void AnAppHasItsTemplatesComponentUnlessItNamesItsOwnTest()
		{
			var cfg = Parse( Config );

			Assert.AreEqual( "Viewer", App( cfg, "sim" ).Component, "from the template" );
			Assert.AreEqual( "Viewer", App( cfg, "tool" ).Component, "through a template of a template" );
			Assert.AreEqual( "Broker", App( cfg, "other" ).Component, "the app's own wins - one name, not a list" );
			Assert.AreEqual( "", App( cfg, "none" ).Component, "optional" );

			// Groups stay what they were: UI grouping, accumulated through the templates, independent of the component
			Assert.AreEqual( "Back end;Tools", App( cfg, "tool" ).Groups );
		}

		[TestMethod()]
		public void AFileDefinitionNamesTheComponentItStandsForTest()
		{
			var cfg = Parse( Config );

			Assert.AreEqual( "CycloneDDS", cfg.VfsNodes.Single( n => n.Id == "pkg.dds" ).Component );
			Assert.AreEqual( "", cfg.VfsNodes.Single( n => n.Id == "pkg.all" ).Component );
		}

		[TestMethod()]
		public void TheSiteHasANameTest()
		{
			Assert.AreEqual( "Test site", Parse( Config ).SiteName );
			Assert.AreEqual( "", Parse( "<Shared/>" ).SiteName );
		}
	}
}
