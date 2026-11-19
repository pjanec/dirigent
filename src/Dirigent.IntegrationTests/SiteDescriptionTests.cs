using Microsoft.VisualStudio.TestTools.UnitTesting;
using Dirigent;
using Dirigent.TestBed;
using Dirigent.TestBed.Scenarios;
using Dirigent.Web;
using EmbedIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Dirigent.IntegrationTests
{
	/// <summary>
	/// What describes the site to a tool - the master's status and its machines - and the rules the control routes follow
	/// (which failure is which status code). The routes themselves are exercised over HTTP in the system tests; here is
	/// the logic that needs the master and no web server.
	/// </summary>
	[TestClass()]
	public class SiteDescriptionTests
	{
		static readonly TimeSpan Timeout = TimeSpan.FromSeconds( 30 );

		[TestInitialize()]
		public void SetUp() => Diagnostics.ClearLog();

		static string SystemDriveRoot => Path.GetPathRoot( Path.GetTempPath() )!;

		static Scenario World()
			=> Scenario.TwoMachines()
				.Share( "m1", "C", SystemDriveRoot )
				.App( "m1.a", a => a.LongRunning() )
				.App( "m2.b", a => a.LongRunning() )
				.RawXml( "<File Id='machine.log' Title='Machine log' MachineId='{m1}' Path='{temp}\\machine.log'/>" )
				.Plan( "p", p => p.App( "m1.a" ) );

		static Task<TestBed.TestBed> StartBed() => TestBed.TestBed.StartAsync( new TestBedOptions() { Scenario = World() } );

		static async Task<T> OnMaster<T>( TestBed.TestBed bed, Func<T> fn )
		{
			T res = default!;
			var op = bed.Master.AddSynchronousOp( () => res = fn() );
			await op.WaitAsync();
			if( op.Exception != null ) throw op.Exception;
			return res;
		}

		[TestMethod()]
		public async Task AMachineIsDescribedWithWhatTheConfigAndTheConnectionSay()
		{
			using var bed = await StartBed();
			var api = new MachineApiController( bed.Master );

			var m1 = await OnMaster( bed, () => api.InfoOf( bed.Master.GetMachineDef( "m1" )! ) );

			Assert.AreEqual( "m1", m1.id );
			Assert.IsTrue( m1.connected );
			Assert.AreEqual( "127.0.0.1", m1.actualIp, "learned from the connection" );
			Assert.IsNotNull( m1.lastChange );
			Assert.IsNull( m1.mac, "none configured" );
			CollectionAssert.AreEqual( new[] { "C" }, m1.shares.Select( s => s.name ).ToList() );
			Assert.AreEqual( SystemDriveRoot, m1.shares[0].path );
			CollectionAssert.AreEqual( new[] { "m1.a" }, m1.apps, "the apps of this machine only" );
			CollectionAssert.AreEqual( new[] { "machine.log" }, m1.fileDefs, "the machine's own files, not the apps'" );
			Assert.IsNotNull( m1.macLearned );

			var m2 = await OnMaster( bed, () => api.InfoOf( bed.Master.GetMachineDef( "m2" )! ) );
			CollectionAssert.AreEqual( new[] { "m2.b" }, m2.apps );
			Assert.AreEqual( 0, m2.fileDefs.Count );
		}

		[TestMethod()]
		public async Task TheStatusCountsWhatTheConfigDefinesAndNamesTheMaster()
		{
			using var bed = await StartBed();
			var api = new MachineApiController( bed.Master );

			var s = await OnMaster( bed, () => api.StatusOf() );

			StringAssert.Contains( s.version, " ", "as --version prints it: name and version" );
			Assert.AreEqual( bed.Master.MachineId, s.masterMachineId );
			Assert.AreEqual( 2, s.sharedConfig.machines );
			Assert.AreEqual( 2, s.sharedConfig.apps );
			Assert.AreEqual( 1, s.sharedConfig.plans );
			Assert.IsTrue( s.sharedConfig.fileDefs >= 1 );
			Assert.IsTrue( File.Exists( s.sharedConfig.path ), s.sharedConfig.path );
			Assert.IsTrue( DateTime.UtcNow - s.sharedConfig.loadedAt < TimeSpan.FromMinutes( 5 ) );
			Assert.IsTrue( DateTime.UtcNow - s.startedAt < TimeSpan.FromMinutes( 5 ) );
			Assert.IsFalse( s.killAllInProgress );
			Assert.AreEqual( bed.Master.DownloadJobs.ArchiveFolder, s.archiveFolder );
			CollectionAssert.IsSubsetOf( new[] { "m1", "m2" }, s.connectedClients );
		}

		[TestMethod()]
		public void FailuresAreMappedToTheirStatusCodes()
		{
			static int Code( Exception e ) => ( (IHttpException) ApiOps.Map( e ) ).StatusCode;

			Assert.AreEqual( 404, Code( new UnknownPlanName( "x" ) ) );
			Assert.AreEqual( 404, Code( new AppFilesNotFoundException( "x" ) ) );
			Assert.AreEqual( 404, Code( HttpException.NotFound( "x" ) ) );
			Assert.AreEqual( 400, Code( new ArgumentException( "x" ) ) );
			Assert.AreEqual( 400, Code( new System.Text.Json.JsonException( "x" ) ) );
			Assert.AreEqual( 409, Code( new Exception( "the master declined" ) ), "whatever the master refuses is a conflict with the state of things" );
			Assert.AreEqual( 409, Code( new InvalidOperationException( "x" ) ) );
			Assert.AreEqual( "the master declined", ( (HttpException) ApiOps.Map( new Exception( "the master declined\nsecond line" ) ) ).Message, "its message, first line" );
		}

		[TestMethod()]
		public void VariablesAreCheckedBeforeTheyAreSent()
		{
			Assert.IsNull( ApiOps.CheckVars( null ) );
			Assert.IsNull( ApiOps.CheckVars( new() ) );

			var ok = ApiOps.CheckVars( new() { { "A", "1" }, { "B", "two words" } } )!;
			Assert.AreEqual( "two words", ok["B"] );

			foreach( var bad in new[] { ( "A=B", "x" ), ( "A::B", "x" ), ( "A", "x::y" ), ( " ", "x" ) } )
			{
				var e = Assert.ThrowsException<HttpException>( () => ApiOps.CheckVars( new() { { bad.Item1, bad.Item2 } } ), bad.ToString() );
				Assert.AreEqual( 400, e.StatusCode );
			}
		}

		[TestMethod()]
		public async Task AScriptCanBeStartedOnAMachineAndNotOnOneThatIsAbsent()
		{
			using var bed = await StartBed();

			var id = Guid.NewGuid();
			await OnMaster( bed, () => { bed.Master.StartScriptOn( "", "m2", id, Scripts.BuiltIn.ListVfsNodes._Name, "{}", "list" ); return 0; } );

			await bed.WaitUntilAsync( async () => ( await bed.Operator.GetScriptStateAsync( id ) )?.Status == EScriptStatus.Finished,
				Timeout, "the script ran on m2 and finished" );

			var e = await Assert.ThrowsExceptionAsync<Exception>( () => OnMaster( bed, () => { bed.Master.StartScriptOn( "", "ghost", Guid.NewGuid(), "x", null, "x" ); return 0; } ) );
			StringAssert.Contains( e.Message, "not connected" );
		}
	}
}
