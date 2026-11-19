using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using Dirigent.Web;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// One node declared in an AppTemplate serves every app that uses it: the title takes the name of the
	/// app's working folder when the config is loaded, and the agent expands the path with the same
	/// variables the launcher uses.
	/// </summary>
	[TestClass()]
	public class AppVariablesInPathsTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 30 );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		static Scenario World()
			=> Scenario.OneMachine()
				.RawXml(
					"<AppTemplate Name='ig' ExeFullPath='{testapp}'>"
					+ "<Folder Id='log' Title='Log/%APP_STARTUPDIR_NAME%' Path='{temp}\\IG\\Logs\\%APP_STARTUPDIR_NAME%' Mask='*.log'/>"
					+ "</AppTemplate>" )
				.RawXml( "<App AppIdTuple='{m1}.igm1' Template='ig' StartupDir='..\\RenderHost-3801'/>" )
				.RawXml( "<App AppIdTuple='{m1}.igm2' Template='ig' StartupDir='..\\RenderHost-3802'/>" );

		[TestMethod()]
		public async Task EachAppOfATemplateGetsItsOwnTitleAndItsOwnFolder()
		{
			using var bed = await TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World() } );

			foreach( var n in new[] { "3801", "3802" } )
			{
				var dir = Path.Combine( bed.TempRoot, "IG", "Logs", "RenderHost-" + n );
				Directory.CreateDirectory( dir );
				File.WriteAllText( Path.Combine( dir, $"{n}.log" ), n );
			}

			foreach( var n in new[] { "3801", "3802" } )
			{
				var id = "m1.igm" + ( n == "3801" ? "1" : "2" );
				var lines = await bed.SendCliCommandAsync( $"GetAppFiles {id}", Timeout );
				var json = lines[0].Substring( $"APPFILES:{id}:".Length );
				var entry = JsonSerializer.Deserialize<AppFilesResult>( json )!.fileDefs.Single();

				Assert.IsNull( entry.error, entry.error );
				Assert.AreEqual( $"Log/RenderHost-{n}", entry.title );

				var file = entry.files.Single();
				Assert.AreEqual( Path.Combine( bed.TempRoot, "IG", "Logs", "RenderHost-" + n, $"{n}.log" ), file.localPath );
			}
		}
	}
}
