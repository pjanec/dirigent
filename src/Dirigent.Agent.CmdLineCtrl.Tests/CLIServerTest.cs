using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using Dirigent.Common;
using Dirigent.Agent.Core;
using System.Net.Sockets;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Dirigent.Agent.CmdLineCtrl.Tests
{
	[TestClass]
	public class CLIServerTest
	{

		private void SendReq( TcpClient cli, string id, string req )
		{
			var stream = cli.GetStream();
			var message = System.Text.Encoding.UTF8.GetBytes( String.Format( "[{0}] {1}\n", id, req ) );
			stream.Write( message, 0, message.Length );
			stream.Flush();
		}

		private string ReadResp( TcpClient cli, int timeOutMs )
		{
			string resp = null;
			var stream = cli.GetStream();
			stream.ReadTimeout = timeOutMs;
			byte[] buf = new byte[10000];
			int numRead = stream.Read( buf, 0, buf.Length );
			if( numRead > 0 )
			{
				resp = Encoding.UTF8.GetString( buf, 0, numRead );
			}

			return resp;
		}

		[TestMethod]
		public void Read1()
		{
			AppInitializedDetectorFactory appInitializedDetectorFactory = new AppInitializedDetectorFactory();
			var localOps = new LocalOperations( "m1", appInitializedDetectorFactory, null, null, 0, false );
			localOps.SetPlanRepo( TestPlanRepo.plans.Values );

			var server = new CLIServer( "127.0.0.1", 6001, localOps );

			server.Start();


			var client = new TcpClient( "localhost", 6001 );

			SendReq( client, "000", "SetVars FERDA=BUH::JANA=COOL" );
			server.Tick();
			var resp000 = ReadResp( client, 1000 );

			SendReq( client, "001", "StartPlan p1" );
			server.Tick();
			var resp001 = ReadResp( client, 1000 );


			SendReq( client, "002", "GetAppState m1.a" );
			server.Tick();
			var resp002 = ReadResp( client, 1000 );

			SendReq( client, "003", "GetPlanState p1" );
			server.Tick();
			var resp003 = ReadResp( client, 1000 );

			SendReq( client, "004", "GetAllPlansState" );
			server.Tick();
			var resp004 = ReadResp( client, 1000 );

			SendReq( client, "005", "GetAllAppsState" );
			server.Tick();
			var resp005 = ReadResp( client, 1000 );


			client.Close();


			//Assert.IsNotNull(cfg.Plans[0].getAppDefs());
			//Assert.AreEqual( "m1.a", cfg.Plans[0].getAppDefs().First().AppIdTuple.ToString() );

			server.Stop();

		}
	}

	public class TestPlanRepo
	{
		public static Dictionary<string, AppDef> ads = new Dictionary<string, AppDef>()
		{
			{ "a", new AppDef() { AppIdTuple = new AppIdTuple("m1", "a"), StartupOrder = -1, SeparationInterval = 1.0, Dependencies=new List<string>() {"b"} } },
			{ "b", new AppDef() { AppIdTuple = new AppIdTuple("m1", "b"), StartupOrder = -1, SeparationInterval = 2.0, } },
			{ "c", new AppDef() { AppIdTuple = new AppIdTuple("m1", "c"), StartupOrder = -1, SeparationInterval = 1.0, Dependencies=new List<string>() {"b"} } },
			{ "d", new AppDef() { AppIdTuple = new AppIdTuple("m1", "d"), StartupOrder = -1, SeparationInterval = 1.0, Dependencies=new List<string>() {"a"} } },
		};

		public static Dictionary<string, ILaunchPlan> plans = new Dictionary<string, ILaunchPlan>()
		{
			{ "p1", new LaunchPlan("p1", new List<AppDef>() { ads["a"], ads["b"], ads["c"], ads["d"] } ) }
		};

	}

	public class TestAppStateRepo
	{
		public Dictionary<AppIdTuple, AppState> appsState;

		public void init( List<AppDef> appDefs )
		{
			appsState = new Dictionary<AppIdTuple, AppState>();
			foreach( var ad in appDefs )
			{
				appsState[ad.AppIdTuple] = new AppState();
			}
		}

		public void makeLaunched( AppIdTuple id )
		{
			appsState[id].PlanApplied = true;
			appsState[id].Started = true;
			appsState[id].Running = true;
		}

		public void makeInitialized( AppIdTuple id )
		{
			makeLaunched( id );
			appsState[id].Initialized = true;
		}
	}

}
